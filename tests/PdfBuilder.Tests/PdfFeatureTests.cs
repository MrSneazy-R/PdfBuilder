using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests
{
    public class PdfFeatureTests
    {
        static PdfFeatureTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private static byte[] Generate(PdfDocument doc) => new PdfWriter().GenerateBytes(doc);

        [Fact]
        public void HeadersAndFooters_RenderWithTokens()
        {
            var doc = new PdfDocument();

            new PdfDocumentBuilder(doc)
                .Title("Header Demo")
                .HeaderFooter(hf =>
                {
                    hf.HeaderTemplate = "Page {page} of {pages}";
                    hf.FooterTemplate = "Generated {date:yyyy-MM-dd}";
                    hf.HeaderAlign = TextAlignment.Center;
                    hf.FooterAlign = TextAlignment.Center;
                });

            var page = doc.AddPage();
            new PdfPageBuilder(page)
                .Margin(40)
                .Content(col =>
                {
                    col.Text("Body copy to verify header/footer rendering.").Add();
                });

            doc.HeaderFooter.HeaderTemplate.Should().Be("Page {page} of {pages}");

            var pdfBytes = Generate(doc);
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);

            blocks.Should().Contain("Page 1 of 1");
            blocks.Any(t => t.StartsWith("Generated ", StringComparison.Ordinal)).Should().BeTrue();
        }

        [Fact]
        public void TableBuilder_RendersCaptionAndCells()
        {
            var doc = new PdfDocument { Title = "Table Example" };
            var page = doc.AddPage();

            new PdfPageBuilder(page)
                .Margin(36)
                .Content(col =>
                {
                    float columnWidth = page.Width - page.MarginLeft - page.MarginRight;
                    col.Table(page.MarginLeft, col.GetCurrentY(), columnWidth, 0)
                       .Caption("Inventory Table")
                       .HeaderRow(
                           c => c.Text("Item").Bold(),
                           c => c.Text("Qty").AlignRight())
                       .Row(
                           c => c.Text("Coffee Beans"),
                           c => c.Text("42").AlignRight())
                       .Row(
                           c => c.Text("Cups"),
                           c => c.Text("128").AlignRight())
                       .Add();
                });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);

            string CaptionHex(string text)
            {
                var bytes = Encoding.GetEncoding(1252).GetBytes(text);
                return "<" + BitConverter.ToString(bytes).Replace("-", string.Empty) + ">";
            }

            stream.Should().Contain(CaptionHex("Inventory Table"));
            blocks.Should().Contain("Item");
            blocks.Should().Contain("Qty");
            blocks.Should().Contain("Coffee");
            blocks.Should().Contain("Beans");
            blocks.Should().Contain("42");
        }

        [Fact]
        public void Lists_CreateOutlinesAndLinkAnnotations()
        {
            var doc = new PdfDocument { Title = "Outline Demo" };
            var page = doc.AddPage();

            new PdfPageBuilder(page)
                .Margin(40)
                .Content(col =>
                {
                    col.Anchor("intro").Title("Introduction").Level(1).Add();
                    col.Text("Intro paragraph").Add();

                    float columnWidth = page.Width - page.MarginLeft - page.MarginRight;
                    new ListBuilder(col, page.MarginLeft, col.GetCurrentY(), columnWidth)
                        .Marker(ListMarker.Decimal)
                        .Item(new RichRun { Text = "Jump to intro", LinkAnchor = "intro", Underline = true })
                        .Item(new RichRun { Text = "Visit OpenAI", LinkUrl = "https://openai.com", Underline = true })
                        .Add();
                });

            var pdf = Encoding.ASCII.GetString(Generate(doc));

            pdf.Should().Contain("/Outlines");
            pdf.Should().Contain("Introduction");
            pdf.Should().Contain("/S /URI");
        }
    }
}
