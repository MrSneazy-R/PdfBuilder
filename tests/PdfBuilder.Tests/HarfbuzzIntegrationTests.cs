using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using Xunit;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Tests
{
    public class HarfbuzzIntegrationTests
    {
        static HarfbuzzIntegrationTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void SharedShaper_ConcurrentSpacedText_PreservesGlyphAdvances()
        {
            const string text = "Synthetic organisation - no real customer or operational data.";
            var request = new TextShapingRequest(
                text, "Helvetica", 10, 1.2f, 500, false, false, false, false, null);
            float expectedWidth = TextShaper.Shared.ShapeParagraph(request).Lines.Single().Width;

            float[] widths = Enumerable.Range(0, 128)
                .AsParallel()
                .WithDegreeOfParallelism(Math.Min(16, Environment.ProcessorCount))
                .Select(_ => TextShaper.Shared.ShapeParagraph(request).Lines.Single().Width)
                .ToArray();

            widths.Should().OnlyContain(width => Math.Abs(width - expectedWidth) < 0.001f);
        }

        [Fact]
        public void TableCaption_UsesEmbeddedFonts()
        {
            var doc = CreateShapedTable(table =>
            {
                table.CaptionText = "Caf\u00E9 Data";
                table.Rows.Add(new TableRow
                {
                    Cells = { new TableCell { Text = "Hello world", Padding = 0 } }
                });
            });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);

            stream.Should().Contain("] TJ");
        }

        [Fact]
        public void TableCell_WithInternationalText_RoundTripsThroughExtractor()
        {
            const string text = "Café مرحبا 中文";

            var doc = CreateShapedTable(table =>
            {
                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = text,
                            Padding = 0,
                            TextStyle = CreateMultilingualTextStyle()
                        }
                    }
                });
            });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);

            stream.Should().Contain("] TJ");

            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);
            var normalized = string.Join(" ", blocks)
                                   .Replace("  ", " ", StringComparison.Ordinal)
                                   .Trim();

            normalized.Should().Contain("Café");
            normalized.Should().Contain("مرحبا");
            normalized.Should().Contain("中文");
        }

        [Fact]
        public void TableCell_RotatedText_UsesGlyphRunMatrix()
        {
            var doc = CreateShapedTable(table =>
            {
                table.ColumnWidths = new List<float> { 120 };
                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = "Ångström Axis",
                            Padding = 0,
                            RotationDegrees = 90f,
                            TextStyle = new TableModels.TextStyle
                            {
                                FontFamily = "Helvetica",
                                Wrap = TableModels.TextWrapMode.Wrap
                            }
                        }
                    }
                });
            });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);

            stream.Should().Contain("/Ff");
            stream.Should().Contain("] TJ");

            var matches = Regex.Matches(stream, @"([\-0-9\.]+)\s+([\-0-9\.]+)\s+([\-0-9\.]+)\s+([\-0-9\.]+)\s+0\s+0\s+cm\s+BT");
            matches.Count.Should().BeGreaterThan(0);

            bool hasRotation = matches.Cast<Match>().Any(m =>
            {
                double b = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                double c = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                return Math.Abs(b) > 1e-3 || Math.Abs(c) > 1e-3;
            });

            hasRotation.Should().BeTrue("rotated text should introduce off-diagonal values in the text matrix");
        }

        [Fact]
        public void TableCell_Hyphenation_ProducesShapedRuns()
        {
            var doc = CreateShapedTable(table =>
            {
                table.ColumnWidths = new List<float> { 70 };
                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = "hyperlocalization",
                            Padding = 0,
                            WordBreak = CellWordBreak.BreakWord,
                            TextStyle = new TableModels.TextStyle
                            {
                                Wrap = TableModels.TextWrapMode.Hyphenate
                            }
                        }
                    }
                });
            });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);
            stream.Should().Contain("] TJ");

            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);
            blocks.Should().NotBeEmpty();

            bool hasHyphen = blocks.Any(b =>
                b.Contains("-", StringComparison.Ordinal) ||
                b.Contains('\u00AD'));

            hasHyphen.Should().BeTrue("hyphenated runs should include a hyphen glyph (blocks: {0})", string.Join(" | ", blocks));
        }

        [Fact]
        public void TableCell_EllipsisWhenClipped_AppendsEllipsisGlyph()
        {
            var doc = CreateShapedTable(table =>
            {
                table.ColumnWidths = new List<float> { 60 };
                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = "ellipsis behaviour validation sample",
                            Padding = 0,
                            TextStyle = new TableModels.TextStyle
                            {
                                Wrap = TableModels.TextWrapMode.EllipsisWhenClipped
                            }
                        }
                    }
                });
            });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);

            stream.Should().Contain("] TJ");
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);
            blocks.Should().NotBeEmpty();

            bool hasEllipsis = blocks.Any(b =>
                b.Contains("...", StringComparison.Ordinal) ||
                b.Contains('\u2026'));

            hasEllipsis.Should().BeTrue("clipped text should end with an ellipsis glyph (blocks: {0})", string.Join(" | ", blocks));
        }

        [Fact]
        public void TableCell_MixedDirectionText_RoundTripsInOrder()
        {
            const string english = "Analytics";
            const string arabic = "مرحبا";
            const string hebrew = "שָׁלוֹם";
            string combined = $"{english} {arabic} {hebrew}";

            var doc = CreateShapedTable(table =>
            {
                table.ColumnWidths = new List<float> { 180 };
                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = combined,
                            Padding = 0,
                            TextStyle = new TableModels.TextStyle
                            {
                                Wrap = TableModels.TextWrapMode.Wrap,
                                FallbackFonts = CreateMultilingualTextStyle().FallbackFonts
                            }
                        }
                    }
                });
            });

            var pdfBytes = PdfContentHelper.Generate(doc);
            var stream = PdfContentHelper.ExtractFirstStream(pdfBytes);
            stream.Should().Contain("/Ff");
            stream.Should().Contain("] TJ");

            var blocks = PdfTextExtractor.ExtractTextBlocks(pdfBytes);
            blocks.Should().NotBeEmpty();

            var normalized = string.Join(" ", blocks)
                                   .Replace("  ", " ", StringComparison.Ordinal)
                                   .Trim();

            normalized.Should().Contain(english);
            normalized.Should().Contain(arabic);
            var normalizedNoMarks = RemoveDiacritics(normalized);
            normalizedNoMarks.Should().Contain("\u05E9\u05DC\u05D5\u05DD");
        }

        private static PdfDocument CreateShapedTable(Action<TableElement> configure)
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var table = new TableElement(page.MarginLeft, page.Height - page.MarginTop - 40)
            {
                TableWidth = 220,
                ColumnWidths = new List<float> { 220 },
                CellPadding = 4,
                AutoSizeColumns = false
            };

            configure?.Invoke(table);

            if (table.ColumnWidths == null || table.ColumnWidths.Count == 0)
                table.ColumnWidths = new List<float> { table.TableWidth ?? 220 };

            if (table.Rows.Count == 0)
            {
                table.Rows.Add(new TableRow
                {
                    Cells = { new TableCell { Text = "placeholder" } }
                });
            }

            page.AddElement(table);
            return doc;
        }

        private static TableModels.TextStyle CreateMultilingualTextStyle() => new()
        {
            FallbackFonts = new List<string>
            {
                "Noto Naskh Arabic",
                "Noto Sans Arabic",
                "Noto Sans Hebrew"
            }
        };

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var decomposed = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.SpacingCombiningMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

    }
}
