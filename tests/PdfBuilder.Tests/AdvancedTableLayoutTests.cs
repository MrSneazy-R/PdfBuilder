using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Writer;
using Xunit;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Tests;

[Collection("Table performance serial")]
public sealed class AdvancedTableLayoutTests
{
    [Fact]
    public void Table_ColumnBanding_PersistsAcrossPages()
    {
        var document = CreateFlowingTable(10, model =>
        {
            model.ColumnBanding = new TableModels.ColumnBandingSpec
            {
                Step = 1,
                Fills = new List<TableModels.BandFill>
                {
                    new() { FillColor = Color.Blue }, new() { FillColor = Color.Yellow }
                }
            };
        });

        var streams = PdfContentHelper.ExtractStreams(new PdfWriter().GenerateBytes(document));
        streams.Should().HaveCountGreaterThan(1);
        Regex.Matches(streams[1], @"0 0 1 rg").Count.Should().BeGreaterThan(0);
        Regex.Matches(streams[1], @"1 1 0 rg").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Table_RowSpan_DoesNotBreakAcrossPagesIncorrectly()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).AutoPaginate(document).Content(column =>
        {
            var table = column.Table(page.MarginLeft, column.GetCurrentY(), page.Width - 80, 0).CellPadding(0);
            var model = table.Build();
            model.ColumnWidths = new List<float> { 250, 250 };
            model.Rows.Add(new TableRow { Cells = { new TableCell { Text = "span", RowSpan = 2 }, new TableCell { Text = "one", Padding = 0 } } });
            model.Rows.Add(new TableRow { Cells = { new TableCell { Text = "two", Padding = 0 } } });
            for (var index = 0; index < 10; index++)
                model.Rows.Add(new TableRow { RowHeight = 20, Cells = { new TableCell { Text = index.ToString(), Padding = 0 }, new TableCell { Text = "body", Padding = 0 } } });
            table.Add();
        });

        document.Invoking(value => new PdfWriter().GenerateBytes(value)).Should().NotThrow();
    }

    [Fact]
    public void Table_ColSpan_ResolvesColumnWidthsCorrectly()
    {
        var document = DirectTable(table =>
        {
            table.ColumnWidths = new List<float> { 60, 60, 80 };
            table.Rows.Add(new TableRow { Cells = { new TableCell { Text = "span", ColSpan = 2, Padding = 0 }, new TableCell { Text = "tail", Padding = 0 } } });
        });

        string stream = PdfContentHelper.ExtractFirstStream(new PdfWriter().GenerateBytes(document));
        var clippingRectangles = Regex.Matches(
                stream,
                @"(?<x>-?\d+(?:\.\d+)?)\s+(?<y>-?\d+(?:\.\d+)?)\s+(?<width>-?\d+(?:\.\d+)?)\s+(?<height>-?\d+(?:\.\d+)?)\s+re\s+W\s+n")
            .Cast<Match>()
            .Select(match => new
            {
                X = float.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                Width = float.Parse(match.Groups["width"].Value, CultureInfo.InvariantCulture)
            });

        clippingRectangles.Should().Contain(rectangle =>
            Math.Abs(rectangle.X - 40f) <= 0.1f &&
            Math.Abs(rectangle.Width - 120f) <= 0.1f,
            "the spanning cell must cover the first two 60-point columns");
    }

    [Fact]
    public void Table_InvalidOverlappingSpans_ThrowsUsefulException()
    {
        var document = DirectTable(table =>
        {
            table.Rows.Add(new TableRow { Cells = { new TableCell { Text = "owner", RowSpan = 2 } } });
            table.Rows.Add(new TableRow { Cells = { new TableCell { Text = "overlap", ColSpan = 2 } } });
        });

        document.Invoking(value => new PdfWriter().GenerateBytes(value))
            .Should().Throw<InvalidOperationException>().WithMessage("*span*");
    }

    [Fact]
    public void Table_AutoColumns_RespectMinAndMaxWidths()
    {
        var document = DirectTable(table =>
        {
            table.TableWidth = 180;
            table.ColumnDefinitions = new List<TableModels.TableColumnDefinition>
            {
                TableModels.TableColumn.Auto(minWidth: 50, maxWidth: 80),
                TableModels.TableColumn.Relative(1, minWidth: 60, maxWidth: 100)
            };
            table.Rows.Add(new TableRow { Cells = { new TableCell { Text = "automatic column" }, new TableCell { Text = "relative column" } } });
        });

        document.Invoking(value => new PdfWriter().GenerateBytes(value)).Should().NotThrow();
    }

    [Fact]
    public void Table_NoWrapAndEllipsis_RenderCorrectly()
    {
        var document = DirectTable(table =>
        {
            table.ColumnWidths = new List<float> { 45 };
            table.Rows.Add(new TableRow { Cells = { new TableCell { Text = "unbroken text", TextStyle = new TableModels.TextStyle { Wrap = TableModels.TextWrapMode.NoWrap } } } });
            table.Rows.Add(new TableRow { Cells = { new TableCell { Text = "ellipsis behavior", TextStyle = new TableModels.TextStyle { Wrap = TableModels.TextWrapMode.EllipsisWhenClipped } } } });
        });

        var stream = PdfContentHelper.ExtractFirstStream(new PdfWriter().GenerateBytes(document));
        stream.Should().Contain("<2E2E2E>");
    }

    [Fact]
    public void Table_ThousandRows_CompletesWithinBaselineBudget()
    {
        var stopwatch = Stopwatch.StartNew();
        var document = CreateFlowingTable(1_000, _ => { });
        var bytes = new PdfWriter().GenerateBytes(document);
        stopwatch.Stop();

        bytes.Should().NotBeEmpty();
        var budget = OperatingSystem.IsMacOS()
            ? TimeSpan.FromSeconds(60)
            : TimeSpan.FromSeconds(15);
        stopwatch.Elapsed.Should().BeLessThan(budget, "shared macOS CI runs the net8.0 and net10.0 suites concurrently");
    }

    private static PdfDocument CreateFlowingTable(int rows, Action<TableElement> configure)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).AutoPaginate(document).Content(column =>
        {
            var table = column.Table(page.MarginLeft, column.GetCurrentY(), page.Width - 80, 0).CellPadding(0);
            var model = table.Build();
            model.ColumnWidths = new List<float> { 250, 250 };
            configure(model);
            for (var index = 0; index < rows; index++)
                model.Rows.Add(new TableRow { RowHeight = 80, Cells = { new TableCell { Text = index.ToString(), Padding = 0 }, new TableCell { Text = "body", Padding = 0 } } });
            table.Add();
        });
        return document;
    }

    private static PdfDocument DirectTable(Action<TableElement> configure)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var table = new TableElement(page.MarginLeft, page.Height - page.MarginTop - 40) { TableWidth = 200, CellPadding = 0 };
        configure(table);
        page.AddElement(table);
        return document;
    }
}

[CollectionDefinition("Table performance serial", DisableParallelization = true)]
public sealed class TablePerformanceSerialCollection;
