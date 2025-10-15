using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using PdfBuilder.Writer.Imaging;
using Xunit;

namespace PdfBuilder.Tests
{
    public class PdfSmokeTests
    {
        private static byte[] BuildPdfBytes(Action<ColumnBuilder> draw)
        {
            var doc = new PdfDocument();
            var page = PdfPage.Letter();

            // Use page builder to layout into a single column with 40pt margin
            new PdfPageBuilder(page)
                .Margin(40)
                .Content(col => draw(col))
                .Build();

            doc.Pages.Add(page);

            var writer = new PdfWriter();
            return writer.GenerateBytes(doc);
        }

        [Fact]
        public void GeneratesValidPdfHeader_And_TextContent()
        {
            var bytes = BuildPdfBytes(col =>
            {
                col.Text("Hello World!")
                   .FontFamily("Helvetica")
                   .FontSize(16)
                   .Color("#000000")
                   .Add();
            });

            bytes.Should().NotBeNull().And.HaveCountGreaterThan(100);

            // Header and trailer markers
            var ascii = Encoding.ASCII.GetString(bytes);
            ascii.Should().StartWith("%PDF-1.");
            ascii.Should().Contain("%%EOF");
            ascii.Should().Contain("/Type /Catalog");
            ascii.Should().Contain("/Type /Pages");
            ascii.Should().Contain("/Type /Page");

            // Expect the text to be present in decoded content
            var blocks = PdfTextExtractor.ExtractTextBlocks(bytes);
            blocks.Should().Contain("Hello World!");
        }

        [Fact]
        public void Embeds_Png_Jpeg_Webp_Images_As_XObjects()
        {
            // Load sample images copied to the test output directory
            var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestLogo.png"));
            var jpg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fish.jpeg"));
            var webp = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "logo.webp"));

            bool webpEmbedded;
            try
            {
                WebpWicDecoder.Decode(webp);
                webpEmbedded = true;
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException || ex is InvalidDataException || ex is COMException)
            {
                webpEmbedded = false;
            }

            var bytes = BuildPdfBytes(col =>
            {
                // Place three images stacked with fixed sizes
                col.Image(png, 40, col.GetCurrentY(), 128, 64).Border("#00AAFF", 1).Add();
                col.Image(jpg, 40, col.GetCurrentY(), 128, 96).Opacity(0.85f).Add();
                if (webpEmbedded)
                {
                    col.Image(webp, 40, col.GetCurrentY(), 128, 64).CornerRadius(8).Add();
                }
            });

            var ascii = Encoding.ASCII.GetString(bytes);

            // Resource dictionary should reference image XObjects
            ascii.Should().Contain("/XObject <<");
            ascii.Should().Contain("/Im");

            // Content stream should invoke image draw operator for at least the successfully embedded images
            var doCount = ascii.Split(" Do ", StringSplitOptions.None).Length - 1;
            doCount.Should().BeGreaterOrEqualTo(webpEmbedded ? 3 : 2);
        }

        [Fact]
        public void RichText_Links_CreateAnnotations()
        {
            var bytes = BuildPdfBytes(col =>
            {
                // Anchor then link to it; also external URL
                col.Anchor("intro").Add();

                var rt = new RichTextBuilder(col, 40, col.GetCurrentY(), 300)
                    .Font("Helvetica", 12)
                    .LineHeight(1.2f)
                    .Span("Go to intro ").EndSpan()
                    .Span("here").LinkAnchor("intro").Underline().EndSpan()
                    .Span(" or visit ").EndSpan()
                    .Span("openai.com").LinkUrl("https://openai.com").Underline().EndSpan();
                rt.Add();
            });

            var ascii = Encoding.ASCII.GetString(bytes);
            // Expect /Annots array on the page
            ascii.Should().Contain("/Annots [");
            // Expect an /A << /S /URI ... >> entry for the URL
            ascii.Should().Contain("/S /URI");
        }
    }
}
