using System;
using System.IO.Compression;
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
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);

            blocks.Should().Contain(block => block.Contains("Inventory Table"));
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

        [Fact]
        public void HeaderDsl_RendersCustomLayout()
        {
            var doc = new PdfDocument();

            new PdfDocumentBuilder(doc)
                .Header(content => content.Text("DSL Header"))
                .Compose(c => c.Page(page => page.Content(col => col.Text("Body content"))));

            var pdfBytes = PdfContentHelper.Generate(doc);
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);

            blocks.Should().Contain(block => block.Contains("DSL Header"));
        }

        [Fact]
        public void Metadata_WritesInfoDictionary()
        {
            var doc = new PdfDocument();

            new PdfDocumentBuilder(doc)
                .Metadata(meta =>
                {
                    meta.Author = "Alice";
                    meta.Subject = "Test Subject";
                    meta.Producer = "PdfBuilder-Tests";
                    meta.CreatedUtc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                    meta.ModifiedUtc = meta.CreatedUtc.Value.AddHours(2);
                })
                .Compose(c => c.Page(page => page.Content(col => col.Text("Metadata body"))));

            var pdf = Encoding.ASCII.GetString(PdfContentHelper.Generate(doc));

            pdf.Should().Contain("/Author (Alice)");
            pdf.Should().Contain("/Subject (Test Subject)");
            pdf.Should().Contain("/Producer (PdfBuilder-Tests)");
            pdf.Should().Contain("/CreationDate (D:20250102030405Z)");
            pdf.Should().Contain("/ModDate (D:20250102050405Z)");
        }

        [Fact]
        public void OutputOptions_CompressContentStream()
        {
            var doc = new PdfDocument();

            new PdfDocumentBuilder(doc)
                .OutputOptions(opt =>
                {
                    opt.CompressContentStreams = true;
                    opt.ContentCompressionLevel = CompressionLevel.Fastest;
                })
                .Compose(c => c.Page(page => page.Content(col => col.Text("Compress me"))));

            var pdf = Encoding.ASCII.GetString(PdfContentHelper.Generate(doc));

            pdf.Should().Contain("/Filter /FlateDecode");
        }

        [Fact]
        public void Canvas_RendersRawCommands()
        {
            var doc = new PdfDocument();

            new PdfDocumentBuilder(doc)
                .Compose(c => c.Page(page => page.Content(col =>
                    col.Canvas(20, 20, canvas => canvas.Raw("0 0 20 20 re S")))));

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));

            stream.Should().Contain("0 0 20 20 re");
        }

        [Fact]
        public void SizeOperators_RenderNestedContent()
        {
            var doc = new PdfDocument();

            new PdfDocumentBuilder(doc)
                .Compose(c => c.Page(page => page.Content(col =>
                    col.MinHeight(72, inner => inner.Text("Sized block"))
                       .Text("Following block"))));

            var blocks = PdfTextExtractor.ExtractTextBlocks(PdfContentHelper.Generate(doc));

            blocks.Should().Contain(block => block.Contains("Sized block"));
            blocks.Should().Contain(block => block.Contains("Following block"));
        }
    }
}
