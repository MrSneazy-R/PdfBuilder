using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests
{
    public class EmbeddedFontTests
    {
        [Fact]
        public void PdfWriter_Writes_Type0_FontResources()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();

            new PdfPageBuilder(page)
                .Margin(36)
                .Content(col =>
                {
                    col.Text("Hello caf\u00E9 world").FontSize(14).Add();
                });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var ascii = Encoding.ASCII.GetString(pdfBytes);

            ascii.Should().Contain("/Subtype /Type0");
            ascii.Should().Contain("/FontFile2");
            ascii.Should().Contain("/ToUnicode");

            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);
            blocks.Should().Contain("Hello caf\u00E9 world");
        }

        [Fact]
        public void PdfWriter_AsciiText_UsesBaseFont()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();

            new PdfPageBuilder(page)
                .Margin(36)
                .Content(col =>
                {
                    col.Text("Simple ascii content").FontSize(12).Add();
                });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var ascii = Encoding.ASCII.GetString(pdfBytes);

            ascii.Should().Contain("/Subtype /Type1");
            ascii.Should().NotContain("/FontFile2");

            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);
            blocks.Should().Contain("Simple ascii content");
        }

        [Fact]
        public void Watermark_OnlyDocument_StillRegistersEmbeddedFontResource()
        {
            var doc = new PdfDocument
            {
                Master = new MasterPageSpec
                {
                    Watermark = new WatermarkSpec
                    {
                        Text = "CONFIDENTIAL \u03A9",
                        FontFamily = "Helvetica",
                        FontSize = 48,
                        Opacity = 0.4f
                    }
                }
            };

            var page = doc.AddPage();
            new PdfPageBuilder(page)
                .Margin(36)
                .Content(col =>
                {
                    col.Text("Placeholder body").FontSize(10).Add();
                });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var ascii = Encoding.ASCII.GetString(pdfBytes);

            ascii.Should().Contain("/Font <<").And.Contain("/Ff");

            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);
            blocks.Should().Contain("CONFIDENTIAL \u03A9");
        }
    }
}
