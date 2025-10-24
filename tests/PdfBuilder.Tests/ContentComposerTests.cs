using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Models;
using PdfBuilder.Writer.Fonts;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PdfBuilder.Tests
{
    public class ContentComposerTests
    {
        [Fact]
        public void ContentComposer_ColumnBuilder_RendersText()
        {
            var doc = new PdfDocument();
            var builder = new PdfDocumentBuilder(doc);

            builder.Compose(document =>
            {
                document.Page(page =>
                {
                    page.Margin(32);
                    page.Content(content =>
                    {
                        content.Column(column =>
                        {
                            column.Spacing(12);
                            column.Item(item => item.Text("Hello composer"));
                            column.Item(item => item.Row(row =>
                            {
                                row.Gap(8);
                                row.Item(inner => inner.Text("Left"));
                                row.Item(inner => inner.Text("Right"));
                            }));
                        });
                    });
                });
            });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);

            blocks.Should().Contain(block => block.Contains("Hello composer"));
            blocks.Should().Contain(block => block.Contains("Left"));
            blocks.Should().Contain(block => block.Contains("Right"));
        }

        [Fact]
        public void PdfDocumentBuilder_FontDiagnostics_EnabledViaEnvironment()
        {
            var previous = Environment.GetEnvironmentVariable("PDFBUILDER_FONT_DIAGNOSTICS");
            var previousEnabled = FontDiagnostics.Enabled;
            var previousWriter = FontDiagnostics.Writer;
            try
            {
                Environment.SetEnvironmentVariable("PDFBUILDER_FONT_DIAGNOSTICS", "1");
                FontDiagnostics.Enabled = false;
                FontDiagnostics.Writer = null;

                _ = new PdfDocumentBuilder(new PdfDocument());

                FontDiagnostics.Enabled.Should().BeTrue();
            }
            finally
            {
                Environment.SetEnvironmentVariable("PDFBUILDER_FONT_DIAGNOSTICS", previous);
                FontDiagnostics.Enabled = previousEnabled;
                FontDiagnostics.Writer = previousWriter;
            }
        }

        [Fact]
        public void PdfDocumentBuilder_FontFolders_FromEnvironment_ReportsDiagnostics()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"pdfbuilder-fonts-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "dummy.ttf"), string.Empty);

            var previousFolders = Environment.GetEnvironmentVariable("PDFBUILDER_FONT_FOLDERS");
            var previousWriter = FontDiagnostics.Writer;
            var previousEnabled = FontDiagnostics.Enabled;

            var messages = new List<string>();

            try
            {
                Environment.SetEnvironmentVariable("PDFBUILDER_FONT_FOLDERS", tempDir);
                FontDiagnostics.Writer = messages.Add;
                FontDiagnostics.Enabled = true;

                _ = new PdfDocumentBuilder(new PdfDocument());

                messages.Should().Contain(message => message.Contains("Failed to register font", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PDFBUILDER_FONT_FOLDERS", previousFolders);
                FontDiagnostics.Enabled = previousEnabled;
                FontDiagnostics.Writer = previousWriter;

                try { Directory.Delete(tempDir, true); } catch { /* ignore cleanup errors */ }
            }
        }
    }
}
