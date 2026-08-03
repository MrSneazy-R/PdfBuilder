using System.IO;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests
{
    public class PdfWriterTests
    {
        [Fact]
        public void GenerateStream_WritesToProvidedStreamWithoutClosing()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.AddElement(new TextElement("Hello world", 72, 720) { MaxWidth = 200f });

            var writer = new PdfWriter();
            using var stream = new MemoryStream();

            writer.GenerateStream(doc, stream);

            stream.Length.Should().BeGreaterThan(0);
            stream.CanWrite.Should().BeTrue();

            long originalLength = stream.Length;
            stream.WriteByte(0x2A);
            stream.Length.Should().Be(originalLength + 1);
        }

        [Fact]
        public void GeneratePreviewImages_ReturnsPngBytes()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.AddElement(new TextElement("Preview sample", 72, 720) { MaxWidth = 240f });

            var previews = new PdfPreviewGenerator().Generate(doc, dpi: 72);

            previews.Should().HaveCount(1);
            previews[0].ImageData.Should().NotBeNullOrEmpty();
            previews[0].ImageData[0].Should().Be(0x89);
            previews[0].ImageData[1].Should().Be(0x50); // 'P'
            previews[0].ImageData[2].Should().Be(0x4E); // 'N'
            previews[0].ImageData[3].Should().Be(0x47); // 'G'
        }

        [Fact]
        public void GeneratePreviewImages_RepeatedNativeResourceUse_RemainsStable()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            page.AddElement(new TextElement("Native resource disposal smoke test", 72, 720) { MaxWidth = 260f });
            var previewGenerator = new PdfPreviewGenerator();

            for (var iteration = 0; iteration < 10; iteration++)
            {
                var previews = previewGenerator.Generate(document, dpi: 72);
                previews.Should().ContainSingle();
                previews[0].ImageData.Should().NotBeNullOrEmpty();
            }
        }
    }
}
