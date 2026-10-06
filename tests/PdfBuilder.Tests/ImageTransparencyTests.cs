using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using SkiaSharp;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class ImageTransparencyTests
{
    [Fact]
    public void EmptySvg_RemainsFullyTransparentWhenEmbedded()
    {
        var svg = new SvgElement("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 2 2'></svg>", 0, 0, 1.5f, 1.5f);
        using var stream = new MemoryStream();
        using var writer = new PdfStreamWriter(stream);
        var resources = new PdfResourceManager();

        var result = resources.EnsureImageXObject(writer, svg);
        writer.Flush();

        AssertSoftMask(stream.ToArray(), result.imageObjId, result.smaskObjId, [0, 0, 0, 0], true);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Png_TransparentAndMixedPixels_PreserveTheirSoftMask(bool mixed, bool predictor)
    {
        byte[] alpha = mixed ? [0, 128, 255, 64] : [0, 0, 0, 0];
        using var bitmap = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var index = 0; index < alpha.Length; index++)
            bitmap.SetPixel(index % 2, index / 2, new SKColor(40, 80, 120, alpha[index]));
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        using var writer = new PdfStreamWriter(stream);
        var resources = new PdfResourceManager(new PdfOutputOptions { UsePngPredictor = predictor });

        var result = resources.EnsureImageXObject(writer, new ImageElement(png.ToArray(), 0, 0, 2, 2));
        writer.Flush();

        AssertSoftMask(stream.ToArray(), result.imageObjId, result.smaskObjId, alpha, predictor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawRgb_TransparentAndMixedPixels_ReferenceTheirSoftMask(bool mixed)
    {
        byte[] alpha = mixed ? [0, 128, 255, 64] : [0, 0, 0, 0];
        using var stream = new MemoryStream();
        using var writer = new PdfStreamWriter(stream);
        var resources = new PdfResourceManager();
        var method = typeof(PdfResourceManager).GetMethod("WriteRawRgbWithOptionalAlpha", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var result = ((int image, int? mask))method.Invoke(resources, [writer, 2, 2, new byte[12], alpha])!;
        writer.Flush();

        AssertSoftMask(stream.ToArray(), result.image, result.mask, alpha, false);
    }

    [Fact]
    public void Png_FullyOpaquePixels_DoNotNeedASoftMask()
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Black);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        using var writer = new PdfStreamWriter(stream);
        var resources = new PdfResourceManager();

        var result = resources.EnsureImageXObject(writer, new ImageElement(png.ToArray(), 0, 0, 2, 2));
        writer.Flush();

        result.smaskObjId.Should().BeNull();
        Encoding.Latin1.GetString(stream.ToArray()).Should().NotContain("/SMask");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawRgb_OpaqueOrAbsentAlpha_DoNotNeedASoftMask(bool hasAlpha)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfStreamWriter(stream);
        var resources = new PdfResourceManager();
        var method = typeof(PdfResourceManager).GetMethod("WriteRawRgbWithOptionalAlpha", BindingFlags.NonPublic | BindingFlags.Instance)!;
        byte[]? alpha = hasAlpha ? [255, 255, 255, 255] : null;

        var result = ((int image, int? mask))method.Invoke(resources, [writer, 2, 2, new byte[12], alpha])!;
        writer.Flush();

        result.mask.Should().BeNull();
        ObjectBody(Encoding.Latin1.GetString(stream.ToArray()), result.image).Should().NotContain("/SMask");
    }

    private static void AssertSoftMask(byte[] bytes, int imageId, int? maskId, byte[] alpha, bool predictor)
    {
        maskId.Should().NotBeNull("fully transparent pixels still require a transparency mask");
        var pdf = Encoding.Latin1.GetString(bytes);
        var image = ObjectBody(pdf, imageId);
        image.Should().Contain($"/SMask {maskId} 0 R");
        var mask = ObjectBody(pdf, maskId!.Value);
        mask.Should().Contain("/ColorSpace /DeviceGray").And.Contain("/Decode [0 1]");
        var length = int.Parse(Regex.Match(mask, @"/Length\s+(\d+)").Groups[1].Value);
        var offset = mask.IndexOf("stream\n", StringComparison.Ordinal) + "stream\n".Length;
        using var compressed = new MemoryStream(Encoding.Latin1.GetBytes(mask.Substring(offset, length)));
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        zlib.CopyTo(decoded);
        byte[] expected = predictor ? [0, alpha[0], alpha[1], 0, alpha[2], alpha[3]] : alpha;
        decoded.ToArray().Should().Equal(expected);
    }

    private static string ObjectBody(string pdf, int id)
        => Regex.Match(pdf, $@"(?ms)^{id} 0 obj\s*(.*?)endobj").Groups[1].Value;
}
