using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using PdfBuilder.Writer.Imaging;
using Xunit;

namespace PdfBuilder.Tests
{
    public class ImageRendererTests
    {
        static ImageRendererTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private static byte[] RenderSingleImage(byte[] imageBytes, Action<ImageBuilder>? configure = null)
        {
            var doc = new PdfDocument();
            var page = PdfPage.Letter();

            new PdfPageBuilder(page)
                .Margin(40)
                .Content(col =>
                {
                    var builder = col.Image(imageBytes, 40, col.GetCurrentY(), 48, 48);
                    configure?.Invoke(builder);
                    builder.Add();
                })
                .Build();

            doc = new PdfDocument(new List<PdfPage> { page });

            var writer = new PdfWriter();
            return writer.GenerateBytes(doc);
        }

        private static byte[] LoadAsset(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            return File.ReadAllBytes(path);
        }

        [Fact]
        public void ImageRenderer_PngRgb8_WithFilteredScanlines_WritesPredictor15AndParams()
        {
            var png = LoadAsset("TestLogo.png");
            var dec = PngDecoder.Decode(png);
            var expected = $"/DecodeParms <</Predictor 15 /Colors {dec.ColorComponents} /BitsPerComponent {dec.BitsPerComponent} /Columns {dec.Width}>>";
            var pdf = Encoding.ASCII.GetString(RenderSingleImage(png));

            pdf.Should().Contain(expected);
        }

        [Fact]
        public void ImageRenderer_PngGray8_SMask_WritesPredictor15AndParams()
        {
            var png = LoadAsset("TestLogo.png");
            var dec = PngDecoder.Decode(png);
            var expected = $"/DecodeParms <</Predictor 15 /Colors 1 /BitsPerComponent {dec.BitsPerComponent} /Columns {dec.Width}>>";
            var pdf = Encoding.ASCII.GetString(RenderSingleImage(png));

            pdf.Should().Contain(expected);
            pdf.Should().Contain("/Decode [0 1]");
        }

        [Fact]
        public void ImageRenderer_Jpeg_NoPredictorParams()
        {
            var jpeg = LoadAsset("fish.jpeg");
            var pdf = Encoding.ASCII.GetString(RenderSingleImage(jpeg));

            pdf.Should().NotContain("/Predictor 15");
        }

        [Fact]
        public void ImageRenderer_PngRawPixels_NoPredictorParams()
        {
            var rawRgb = new byte[]
            {
                255, 0, 0,
                0, 255, 0,
                0, 0, 255,
                255, 255, 0
            };

            using var stream = new MemoryStream();
            using (var writer = new PdfStreamWriter(stream))
            {
                var resourceManager = new PdfResourceManager();
                var method = typeof(PdfResourceManager).GetMethod(
                    "WriteRawRgbWithOptionalAlpha",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                method.Should().NotBeNull("the raw RGB helper is expected to exist for internal use");
                _ = method!.Invoke(resourceManager, new object?[] { writer, 2, 2, rawRgb, null });
            }

            var ascii = Encoding.ASCII.GetString(stream.ToArray());
            ascii.Should().NotContain("/DecodeParms");
        }

        [Fact]
        public void PdfStreamWriter_UsesZlib_ForImageStreams()
        {
            var rawRgb = new byte[] { 10, 20, 30 };

            byte[] pdfBytes;
            using (var stream = new MemoryStream())
            using (var writer = new PdfStreamWriter(stream))
            {
                var resourceManager = new PdfResourceManager();
                var method = typeof(PdfResourceManager).GetMethod(
                    "WriteRawRgbWithOptionalAlpha",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                method.Should().NotBeNull("the raw RGB helper is expected to exist for internal use");
                _ = method!.Invoke(resourceManager, new object?[] { writer, 1, 1, rawRgb, null });
                writer.Flush();
                pdfBytes = stream.ToArray();
            }

            var marker = Encoding.ASCII.GetBytes("stream\n");
            int streamOffset = pdfBytes.AsSpan().IndexOf(marker);
            streamOffset.Should().BeGreaterThanOrEqualTo(0, "the stream marker should be present in the PDF object");

            int dataOffset = streamOffset + marker.Length;
            pdfBytes.Length.Should().BeGreaterThan(dataOffset + 2, "compressed data should contain at least two bytes");
            pdfBytes[dataOffset].Should().Be(0x78);
            pdfBytes[dataOffset + 1].Should().BeOneOf((byte)0x01, (byte)0x5E, (byte)0x9C, (byte)0xDA);

            var endMarker = Encoding.ASCII.GetBytes("\nendstream");
            int endIndex = pdfBytes.AsSpan(dataOffset).IndexOf(endMarker);
            endIndex.Should().BeGreaterThan(0, "compressed segment must terminate with endstream");

            var compressed = new byte[endIndex];
            Array.Copy(pdfBytes, dataOffset, compressed, 0, endIndex);

            using var compressedStream = new MemoryStream(compressed);
            using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            zlib.CopyTo(decompressed);

            decompressed.ToArray().Should().Equal(rawRgb);
        }
    }
}
