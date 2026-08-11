using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using SkiaSharp;
using Xunit;

namespace PdfBuilder.Tests;

[Collection("Image source serial")]
public sealed class ImageSourceTests
{
    [Fact]
    public void ImageSource_AllSupportedSourceForms_AreContentHashDeduplicated()
    {
        byte[] image = Load("TestLogo.png");
        using var stream = new MemoryStream(image);
        string file = Path.Combine(AppContext.BaseDirectory, "TestLogo.png");
        Assembly assembly = typeof(ImageSourceTests).Assembly;
        int lazyCalls = 0;
        ImageSource[] sources =
        [
            ImageSource.FromBytes(image),
            ImageSource.FromMemory(image.AsMemory()),
            ImageSource.FromStream(stream),
            ImageSource.FromFile(file),
            ImageSource.FromEmbeddedResource(assembly, "PdfBuilder.Tests.EmbeddedTestLogo.png"),
            ImageSource.FromLazy(() => { Interlocked.Increment(ref lazyCalls); return image; }).Preload()
        ];
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Column(column =>
        {
            foreach (ImageSource source in sources)
                column.Item().Image(source, 24, 16);
        })));
        document.OutputOptions.CompressContentStreams = false;

        string pdf = Encoding.ASCII.GetString(document.GenerateBytes());

        stream.CanRead.Should().BeTrue("ImageSource does not own caller streams");
        lazyCalls.Should().Be(1);
        Regex.Matches(pdf, @"/Im\d+\s+\d+\s+0\s+R")
            .Select(match => Regex.Match(match.Value, @"/Im(\d+)").Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Should().ContainSingle();
    }

    [Fact]
    public void ImageSource_LazyFactory_IsSnapshottedOnceUnderConcurrentGeneration()
    {
        byte[] image = Load("TestLogo.png");
        int calls = 0;
        ImageSource source = ImageSource.FromLazy(() => { Interlocked.Increment(ref calls); return image; });
        PdfDocument document = PdfDocument.Create(document => document.Page(page => page.Content().Image(source, 40, 30)));
        document.GenerationOptions.Deterministic = true;
        document.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;

        byte[][] outputs = Enumerable.Range(0, 8).AsParallel().Select(_ => document.GenerateBytes()).ToArray();

        calls.Should().Be(1);
        outputs.Should().OnlyContain(output => output.SequenceEqual(outputs[0]));
    }

    [Fact]
    public void ImageSource_IntrinsicSize_UsesPixelsAndDpi()
    {
        ImageSource source = ImageSource.FromBytes(Load("fish.jpeg"));
        ImageSourceInfo info = source.Inspect();
        PdfDocument document = PdfDocument.Create(document => document.Page(page => page.Content().Image(source)));

        ImageElement image = document.Pages.SelectMany(page => page.Elements).OfType<ImageElement>().Single();
        info.PixelWidth.Should().BeGreaterThan(0);
        info.PixelHeight.Should().BeGreaterThan(0);
        image.Width.Should().BeApproximately(info.OriginalWidthPoints, 0.01f);
        image.Height.Should().BeApproximately(info.OriginalHeightPoints, 0.01f);
    }

    [Fact]
    public void ImageOptimisation_DownsamplesAndHonoursJpegQuality()
    {
        byte[] source = CreatePng(400, 200, withAlpha: false);
        byte[] low = GenerateOptimised(source, quality: 20);
        byte[] high = GenerateOptimised(source, quality: 95);

        string lowText = Encoding.ASCII.GetString(low);
        lowText.Should().Contain("/Width 36 /Height 18");
        lowText.Should().Contain("/DCTDecode");
        high.Length.Should().BeGreaterThan(low.Length);
    }

    [Fact]
    public void ImageOptimisation_UsesAlphaCapableEncodingOnlyWhenRequired()
    {
        byte[] source = CreatePng(200, 100, withAlpha: true);
        byte[] preserving = GenerateOptimised(source, quality: 80, alphaAware: true);
        byte[] flattened = GenerateOptimised(source, quality: 80, alphaAware: false);

        Encoding.ASCII.GetString(preserving).Should().Contain("/SMask");
        Encoding.ASCII.GetString(flattened).Should().Contain("/DCTDecode").And.NotContain("/SMask");
    }

    [Fact]
    public void ImageSource_ExifOrientation_SwapsIntrinsicAndEmbeddedDimensions()
    {
        byte[] orientedJpeg = AddExifOrientation(CreateJpeg(4, 2), orientation: 6);
        ImageSource source = ImageSource.FromBytes(orientedJpeg);
        ImageSourceInfo info = source.Inspect();
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Image(source, 20, 40)));

        string pdf = Encoding.ASCII.GetString(document.GenerateBytes());

        info.PixelWidth.Should().Be(2);
        info.PixelHeight.Should().Be(4);
        pdf.Should().Contain("/Width 2 /Height 4");
    }

    [Fact]
    public void ImageDescriptor_FitCropAndOptimisationOptions_AreApplied()
    {
        ImageSource source = ImageSource.FromBytes(Load("TestLogo.png"));
        PdfDocument document = PdfDocument.Create(document => document.Page(page => page.Content().Image(source, 80, 40)
            .Cover()
            .CropAlignment(ImageCropAlignment.BottomRight)
            .Quality(ImageQuality.Medium)
            .MaximumEffectiveDpi(144)
            .JpegQuality(72)
            .AlphaAwareEncoding()));

        ImageElement image = document.Pages.SelectMany(page => page.Elements).OfType<ImageElement>().Single();
        image.Fit.Should().Be(ImageFit.Cover);
        image.Alignment.Should().Be(ImageAlignment.BottomRight);
        image.Quality.Should().Be(ImageQuality.Medium);
        image.Downsample.Should().BeTrue();
        image.MaximumEffectiveDpi.Should().Be(144);
        image.JpegQuality.Should().Be(72);
        image.AlphaAwareEncoding.Should().BeTrue();
    }

    [Fact]
    public void ImageSource_RemoteUrl_IsRejected()
    {
        Action create = () => ImageSource.FromFile("https://example.test/image.png");

        create.Should().Throw<ArgumentException>().WithMessage("*Remote image URLs*");
    }

    private static byte[] GenerateOptimised(byte[] source, int quality, bool alphaAware = true)
    {
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Image(ImageSource.FromBytes(source), 72, 36)
            .MaximumEffectiveDpi(36)
            .Quality(ImageQuality.High)
            .JpegQuality(quality)
            .AlphaAwareEncoding(alphaAware)));
        return document.GenerateBytes();
    }

    private static byte[] CreatePng(int width, int height, bool withAlpha)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256), withAlpha && x < width / 2 ? (byte)128 : (byte)255));
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)!;
        return data.ToArray();
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 90)!;
        return data.ToArray();
    }

    private static byte[] AddExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] payload =
        [
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8,
            0, 1,
            0x01, 0x12, 0, 3, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0,
            0, 0, 0, 0
        ];
        int segmentLength = payload.Length + 2;
        byte[] result = new byte[jpeg.Length + payload.Length + 4];
        result[0] = 0xFF;
        result[1] = 0xD8;
        result[2] = 0xFF;
        result[3] = 0xE1;
        result[4] = (byte)(segmentLength >> 8);
        result[5] = (byte)segmentLength;
        payload.CopyTo(result, 6);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(6 + payload.Length));
        return result;
    }

    private static byte[] Load(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name));
}

[CollectionDefinition("Image source serial", DisableParallelization = true)]
public sealed class ImageSourceSerialCollection;
