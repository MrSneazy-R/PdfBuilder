using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests
{
    public class HarfbuzzIntegrationTests
    {
        static HarfbuzzIntegrationTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void TableCaption_UsesEmbeddedFonts()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();

            var table = new TableElement(page.MarginLeft, page.Height - page.MarginTop - 40)
            {
                TableWidth = 200,
                ColumnWidths = new List<float> { 200 },
                CaptionText = "Café Data"
            };

            table.Rows.Add(new TableRow
            {
                Cells = { new TableCell { Text = "Hello world" } }
            });

            page.Elements.Add(table);

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);

            stream.Should().Contain("/Ff");
            stream.Should().NotContain("/F1");
            stream.Should().Contain("] TJ");
        }

        [Fact]
        public void TableCell_WithInternationalText_RoundTripsThroughExtractor()
        {
            const string text = "Café 東京 مرحبا";

            var doc = new PdfDocument();
            var page = doc.AddPage();
            var table = new TableElement(page.MarginLeft, page.Height - page.MarginTop - 40)
            {
                TableWidth = 220,
                ColumnWidths = new List<float> { 220 },
                CellPadding = 4
            };

            table.Rows.Add(new TableRow
            {
                Cells = { new TableCell { Text = text } }
            });

            page.Elements.Add(table);

            var pdfBytes = PdfContentHelper.Generate(doc);
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);

            blocks.Should().Contain(text);
        }
    }
}
