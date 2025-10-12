using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using TableModels = PdfBuilder.Elements.Table;
using Xunit;

namespace PdfBuilder.Tests
{
    public class TableRendererTests
    {
        static TableRendererTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void Table_PerSideBorders_Render_Correctly()
        {
            var doc = CreateTableDocument(table =>
            {
                table.BorderCollapse = TableModels.BorderCollapseMode.Separate;
                table.CellPadding = 0;

                var cell = new TableCell { Text = string.Empty, Padding = 0 };
                cell.BorderTop = true; cell.BorderColorTop = Color.Red; cell.BorderWidthTop = 2f;
                cell.BorderRight = true; cell.BorderColorRight = Color.FromArgb(0, 170, 0); cell.BorderWidthRight = 1.5f;
                cell.BorderBottom = true; cell.BorderColorBottom = Color.Blue; cell.BorderWidthBottom = 1f;
                cell.BorderLeft = true; cell.BorderColorLeft = Color.Orange; cell.BorderWidthLeft = 0.75f;

                table.Rows.Add(new TableRow { Cells = { cell } });
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));

            stream.Should().Contain("1 0 0 RG 2 w");
            stream.Should().Contain("0 0.667 0 RG 1.5 w");
            stream.Should().Contain("0 0 1 RG 1 w");
            stream.Should().Contain("1 0.647 0 RG 0.75 w");
        }

        [Fact]
        public void Table_OuterVsInnerBorders_AreIndependent()
        {
            var doc = CreateTableDocument(table =>
            {
                table.DrawOuterFrame = true;
                table.OuterBorder = new TableModels.BorderStyle
                {
                    Color = Color.Black,
                    Width = 3f,
                    DashPattern = new[] { 4f, 2f }
                };
                table.InnerBorder = new TableModels.BorderStyle
                {
                    Color = Color.Gray,
                    Width = 1f,
                    DashPattern = new[] { 1f, 1f }
                };

                var row = new TableRow();
                row.Cells.Add(new TableCell { Text = string.Empty, Padding = 0 });
                row.Cells.Add(new TableCell { Text = string.Empty, Padding = 0 });
                table.ColumnWidths = new List<float> { 80, 80 };
                table.Rows.Add(row);
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));
            stream.Should().Contain("[4 2] 0 d");
            stream.Should().Contain("[1 1] 0 d");
        }

        [Fact]
        public void Table_BorderCollapse_PrefersThicker_WhenConflicting()
        {
            var doc = CreateTableDocument(table =>
            {
                table.BorderCollapse = TableModels.BorderCollapseMode.Collapse;
                table.CellPadding = 0;

                var topCell = new TableCell { Text = string.Empty, Padding = 0 };
                topCell.BorderTop = topCell.BorderLeft = topCell.BorderRight = false;
                topCell.BorderBottom = true; topCell.BorderColorBottom = Color.Red; topCell.BorderWidthBottom = 3f;

                var bottomCell = new TableCell { Text = string.Empty, Padding = 0 };
                bottomCell.BorderBottom = bottomCell.BorderLeft = bottomCell.BorderRight = false;
                bottomCell.BorderTop = true; bottomCell.BorderColorTop = Color.Green; bottomCell.BorderWidthTop = 0.5f;

                table.Rows.Add(new TableRow { Cells = { topCell } });
                table.Rows.Add(new TableRow { Cells = { bottomCell } });
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));
            stream.Should().Contain("3 w 40 700 m 240 700 l");
            stream.Should().NotContain("0.5 w 40 700 m 240 700 l");
        }

        [Fact]
        public void Table_RowBanding_PersistsAcrossPages()
        {
            var doc = CreateTableDocument(table =>
            {
                table.EnablePageBreaks = true;
                table.PageTopY = 500;
                table.PageBottomY = 300;
                table.Y = table.PageTopY ?? table.Y;
                table.RowBanding = new TableModels.RowBandingSpec
                {
                    Step = 1,
                    Fills = new List<TableModels.BandFill>
                    {
                        new TableModels.BandFill { FillColor = Color.Red },
                        new TableModels.BandFill { FillColor = Color.Green }
                    }
                };

                for (int i = 0; i < 5; i++)
                {
                    var row = new TableRow { RowHeight = 80f };
                    row.Cells.Add(new TableCell { Text = string.Empty, Padding = 0 });
                    table.Rows.Add(row);
                }
            });

            var streams = PdfContentHelper.ExtractStreams(PdfContentHelper.Generate(doc));
            streams.Should().HaveCountGreaterThan(1);

            var secondStream = streams[1];
            var colors = GetFillColors(secondStream);
            colors.Should().NotBeEmpty();
            colors.First().Should().Be("1 0 0");
        }

        [Fact]
        public void Table_ColumnBanding_WithSpans_Works()
        {
            var doc = CreateTableDocument(table =>
            {
                table.ColumnWidths = new List<float> { 60, 60, 60 };
                table.ColumnBanding = new TableModels.ColumnBandingSpec
                {
                    Step = 1,
                    Fills = new List<TableModels.BandFill>
                    {
                        new TableModels.BandFill { FillColor = Color.Blue },
                        new TableModels.BandFill { FillColor = Color.Yellow }
                    }
                };

                var row = new TableRow();
                row.Cells.Add(new TableCell { Text = string.Empty, Padding = 0, ColSpan = 2 });
                row.Cells.Add(new TableCell { Text = string.Empty, Padding = 0 });
                table.Rows.Add(row);
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));
            var colors = GetFillColors(stream);
            colors.Should().HaveCount(2);
            colors.Should().OnlyContain(c => c == "0 0 1"); // pattern wraps after the second column
            stream.Should().Contain("120 12 re f"); // span covers the first two columns
            stream.Should().Contain("60 12 re f");  // independent third column fill
        }

        [Fact]
        public void Table_RoundedCorners_ClipAndStroke_Once()
        {
            var doc = CreateTableDocument(table =>
            {
                var cell = new TableCell
                {
                    Text = string.Empty,
                    Padding = 0,
                    CornerRadius = 12f,
                    BackgroundColor = Color.FromArgb(255, 200, 200)
                };
                table.Rows.Add(new TableRow { Cells = { cell } });
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));
            stream.Should().Contain(" c f");
            stream.Should().NotContain(" re f");
        }

        [Fact]
        public void TableCell_Text_Underline_And_Strikethrough_Positioned_Correctly()
        {
            var decoColor = Color.FromArgb(32, 160, 220);
            var doc = CreateTableDocument(table =>
            {
                var style = new TableModels.TextStyle
                {
                    FontFamily = "Helvetica",
                    FontSize = 12f,
                    Underline = true,
                    Strikethrough = true,
                    DecorationColor = decoColor
                };

                var cell = new TableCell { Text = "Decorated", Padding = 0, TextStyle = style };
                table.Rows.Add(new TableRow { Cells = { cell } });
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));
            var rgb = string.Format(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###}",
                decoColor.R / 255.0,
                decoColor.G / 255.0,
                decoColor.B / 255.0);

            var occurrences = Regex.Matches(stream, $"{Regex.Escape(rgb)} RG");
            occurrences.Count.Should().BeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public void TableCell_Text_Rotation_Clips_WithinCell()
        {
            var doc = CreateTableDocument(table =>
            {
                var cell = new TableCell { Text = "Rotated", Padding = 0, RotationDegrees = 45f };
                table.Rows.Add(new TableRow { Cells = { cell } });
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));
            stream.Should().Contain(" re W n\nq ");
        }

        [Fact]
        public void TableCell_RichRuns_MixStyles_And_FallbackFonts()
        {
            var doc = CreateTableDocument(table =>
            {
                var cell = new TableCell { Padding = 0 };
                cell.TextRuns.Add(new TableModels.InlineRun
                {
                    Text = "Primary",
                    Style = new TableModels.TextStyle { FontFamily = "Helvetica" },
                    FallbackFonts = new List<string> { "Courier" }
                });
                cell.TextRuns.Add(new TableModels.InlineRun
                {
                    Text = "Secondary",
                    Style = new TableModels.TextStyle { FontFamily = "Times", Bold = true }
                });
                table.Rows.Add(new TableRow { Cells = { cell } });
            });

            var fonts = PdfContentHelper.CollectFonts(doc);
            fonts.Should().Contain("Helvetica");
            fonts.Should().Contain("Times-Bold");
            fonts.Should().Contain("Courier");
        }

        [Fact]
        public void TableCell_Wrap_Hyphenate_Ellipsis_Behavior()
        {
            var doc = CreateTableDocument(table =>
            {
                table.ColumnWidths = new List<float> { 60 };

                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = "wrap wrap wrap",
                            Padding = 0,
                            TextStyle = new TableModels.TextStyle { Wrap = TableModels.TextWrapMode.Wrap }
                        }
                    }
                });

                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = "supercalifragilistic",
                            Padding = 0,
                            WordBreak = CellWordBreak.BreakWord,
                            TextStyle = new TableModels.TextStyle { Wrap = TableModels.TextWrapMode.Hyphenate }
                        }
                    }
                });

                table.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = "ellipsisbehavior",
                            Padding = 0,
                            TextStyle = new TableModels.TextStyle { Wrap = TableModels.TextWrapMode.EllipsisWhenClipped }
                        }
                    }
                });
            });

            var stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(doc));
            stream.Should().Contain("<2D>");
            stream.Should().Contain("<2E2E2E>");
        }

        private static PdfDocument CreateTableDocument(Action<TableElement> configure)
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var table = new TableElement(page.MarginLeft, page.Height - page.MarginTop - 40)
            {
                TableWidth = 200,
                ColumnWidths = new List<float> { 200 },
                CellPadding = 0,
                AutoSizeColumns = false
            };

            configure?.Invoke(table);
            if (table.ColumnWidths == null || table.ColumnWidths.Count == 0)
                table.ColumnWidths = new List<float> { table.TableWidth ?? 200 };

            page.Elements.Add(table);
            return doc;
        }

        private static List<string> GetFillColors(string stream)
            => Regex.Matches(stream, @"([0-9\. ]+) rg")
                    .Select(m => m.Groups[1].Value.Trim())
                    .ToList();
    }
}



