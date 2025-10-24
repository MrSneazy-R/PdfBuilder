using FluentAssertions;
using PdfBuilder.Elements;
using Xunit;

namespace PdfBuilder.Tests
{
    public class VectorElementTests
    {
        [Fact]
        public void BarcodeElement_GeneratesCommands()
        {
            var element = new BarcodeElement("HELLO", BarcodeKind.QrCode);
            element.Commands.Should().NotBeEmpty();
            element.Width.Should().BeGreaterThan(0);
            element.Height.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SvgElement_RendersPngData()
        {
            const string markup = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><circle cx='5' cy='5' r='5' fill='#0000FF'/></svg>";
            var element = new SvgElement(markup, 0, 0, 100, 100);
            element.ImageData.Should().NotBeNullOrEmpty();
            element.MimeType.Should().Be("image/png");
        }
    }
}
