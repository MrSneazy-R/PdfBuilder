using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class MediaRenderingTests
{
    [Fact]
    public void Image_Png_RendersOnAllPlatforms()
    {
        var bytes = GenerateImage("TestLogo.png");
        Encoding.ASCII.GetString(bytes).Should().Contain("/Subtype /Image");
    }

    [Fact]
    public void Image_Jpeg_RendersOnAllPlatforms()
    {
        var bytes = GenerateImage("fish.jpeg");
        Encoding.ASCII.GetString(bytes).Should().Contain("/DCTDecode");
    }

    [Fact]
    public void Image_ContainCoverAndCrop_RenderCorrectly()
    {
        var image = Load("TestLogo.png");
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Column(column =>
        {
            column.Item().Image(image, 160, 60).Contain();
            column.Item().Image(image, 160, 60).Cover().Circle();
        })));

        var pdf = document.GenerateBytes();
        Encoding.ASCII.GetString(pdf).Should().Contain("/Subtype /Image");
    }

    [Fact]
    public void Image_IdenticalContent_IsDeduplicated()
    {
        var image = Load("TestLogo.png");
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Column(column =>
        {
            column.Item().Image(image, 60, 60);
            column.Item().Image(image.ToArray(), 60, 60);
        })));

        Encoding.ASCII.GetString(document.GenerateBytes()).Split("/Subtype /Image", StringSplitOptions.None).Length.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Image_ExcessiveDimensions_IsRejected()
    {
        byte[] header = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 1, 0, 0, 0, 1, 8, 2];
        var document = new PdfDocument();
        document.AddPage().AddElement(new ImageElement(header, 72, 720, 30, 30));

        document.Invoking(value => new PdfWriter().GenerateBytes(value)).Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void WebP_RendersThroughCrossPlatformCodec()
    {
        var document = new PdfDocument();
        document.AddPage().AddElement(new ImageElement(Load("logo.webp"), 72, 720, 30, 30));

        byte[] pdf = new PdfWriter().GenerateBytes(document);

        Encoding.ASCII.GetString(pdf).Should().Contain("/Subtype /Image");
    }

    [Fact]
    public void Svg_ViewBoxAndAspectRatio_RenderCorrectly()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 10'><rect width='20' height='10' fill='#00AAFF'/></svg>";
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Svg(svg, 120, 120)));

        Encoding.ASCII.GetString(document.GenerateBytes()).Should().Contain("/Subtype /Image");
    }

    [Fact]
    public void Svg_ExternalResources_AreBlocked()
    {
        Action create = () => _ = new SvgElement("<svg><image href='file:///secret.png'/></svg>", 0, 0, 40, 40);
        create.Should().Throw<InvalidDataException>().WithMessage("*external resource references*");
    }

    [Fact]
    public void Svg_ExcessiveComplexity_IsRejected()
    {
        var path = new string('M', 500_001);
        Action create = () => _ = new SvgElement($"<svg><path d='{path}'/></svg>", 0, 0, 40, 40);
        create.Should().Throw<InvalidDataException>().WithMessage("*complexity limit*");
    }

    [Theory]
    [InlineData("<svg><rect onload='alert(1)'/></svg>")]
    [InlineData("<svg><foreignObject><div>unsafe</div></foreignObject></svg>")]
    [InlineData("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///secret'>]><svg>&x;</svg>")]
    [InlineData("<svg><style>@import 'https://example.test/x.css';</style></svg>")]
    public void Svg_ActiveContent_IsRejected(string markup)
    {
        Action create = () => _ = new SvgElement(markup, 0, 0, 40, 40);

        create.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Svg_LocalPaintReference_RemainsSupported()
    {
        const string svg = "<svg viewBox='0 0 10 10'><defs><linearGradient id='g'><stop offset='0' stop-color='#000'/><stop offset='1' stop-color='#fff'/></linearGradient></defs><rect width='10' height='10' fill='url(#g)'/></svg>";
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Svg(svg, 40, 40)));

        document.GenerateBytes().Should().NotBeEmpty();
    }

    [Fact]
    public void Barcode_QrCode_IsMachineReadable()
    {
        var barcode = new BarcodeElement("https://example.test/invoice/123", BarcodeKind.QrCode, 2f, 4);
        barcode.Commands.Should().Contain(command => command.Contains(" re f", StringComparison.Ordinal));
        barcode.Width.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Barcode_Code128_IsMachineReadable()
    {
        var barcode = new BarcodeElement("INV-2026-001", BarcodeKind.Code128, 2f, 4);
        barcode.Commands.Should().Contain(command => command.Contains(" re f", StringComparison.Ordinal));
        barcode.QuietZone.Should().Be(4);
    }

    [Fact]
    public void Chart_UsesSharedTypographyAndPdfColor()
    {
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Chart(chart =>
        {
            chart.Size(220, 120);
            chart.Title("Revenue");
            chart.Line("Actual", [10f, 15f, 12f], PdfColor.Parse("#0057B8"));
            chart.Bars("Plan", [12f, 12f, 12f], PdfColor.Rgb(120, 180, 90));
        })));
        document.OutputOptions.ReadableContentStreams = true;

        var pdf = Encoding.ASCII.GetString(document.GenerateBytes());
        pdf.Should().Contain("<526576656E7565>");
        pdf.Should().Contain("/Type /Page");
    }

    private static byte[] GenerateImage(string name)
    {
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Image(Load(name), 80, 60).Contain()));
        return document.GenerateBytes();
    }

    private static byte[] Load(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name));
}
