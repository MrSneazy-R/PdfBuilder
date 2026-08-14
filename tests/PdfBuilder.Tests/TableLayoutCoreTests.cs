using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class TableLayoutCoreTests
{
    [Fact]
    public void Table_SinglePage_RendersCorrectly()
    {
        var document = CreateDocument(3);
        var bytes = PdfContentHelper.Generate(document);

        document.Pages.Should().ContainSingle();
        PdfTextExtractor.ExtractTextBlocks(bytes).Should().Contain(block => block.Contains("row-2", StringComparison.Ordinal));
    }

    [Fact]
    public void Table_MultiplePages_RepeatsHeader()
    {
        var document = CreateDocument(120);

        document.Pages.Count.Should().BeGreaterThan(1);
        document.Pages.Should().OnlyContain(page =>
            page.Elements.OfType<TableSegmentElement>().Any(segment => segment.Segment.IncludeHeader && segment.Rows[0].Row.IsHeader));
    }

    [Fact]
    public void Table_ContentAfterSplitTable_ContinuesCorrectly()
    {
        var document = CreateDocument(120, includeFollowingText: true);
        var bytes = PdfContentHelper.Generate(document);

        string.Join("\n", PdfTextExtractor.ExtractTextBlocks(bytes)).Should().Contain("After table");
    }

    [Fact]
    public void Table_ThousandRows_NoRowsLostOrDuplicated()
    {
        var document = CreateDocument(1_000);
        var extracted = PdfTextExtractor.ExtractTextBlocks(PdfContentHelper.Generate(document));

        for (var index = 0; index < 1_000; index++)
            extracted.Count(block => string.Equals(block, $"row-{index}", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public void Table_ContinuationPreservesColumnWidths()
    {
        var document = CreateDocument(120);
        var tables = document.Pages.SelectMany(page => page.Elements).OfType<TableSegmentElement>().ToList();

        tables.Should().HaveCountGreaterThan(1);
        tables.Select(table => table.Width).Should().OnlyContain(width => Math.Abs(width - tables[0].Width) < 0.01f);
        tables.Should().OnlyContain(segment => segment.ColumnWidths.SequenceEqual(tables[0].ColumnWidths));
    }

    [Fact]
    public void Table_GenerationMetrics_ReportMeasureOncePlan()
    {
        const int bodyRows = 40;
        var document = CreateDocument(bodyRows, enableTableLayoutCounters: true);
        var writer = new PdfWriter();

        writer.GenerateBytes(document);

        PdfBuilder.Models.PdfGenerationMetrics metrics = writer.LastGenerationMetrics!;
        metrics.TableMeasurementCount.Should().Be(1);
        metrics.TableRowMeasurementCount.Should().Be(bodyRows + 1L);
        metrics.TableCellMeasurementCount.Should().Be(metrics.TableRowMeasurementCount * 3L);
        metrics.ContentFactoryInvocationCount.Should().Be(metrics.TableCellMeasurementCount);
        metrics.TableCloneCount.Should().Be(0);
        metrics.TableRowCloneCount.Should().Be(0);
    }

    [Fact]
    public void Table_LayoutPlan_PreservesCanonicalContentAcrossRetainedSegments()
    {
        const int rowCount = 12;
        int factoryCalls = 0;
        var document = new PdfDocument();
        document.LayoutOptions.Diagnostics.EnableTableLayoutCounters = true;
        PdfPage page = document.AddPage(200, 100);
        var table = new TableElement { TableWidth = 160, CellPadding = 0 };
        table.ColumnDefinitions.Add(PdfBuilder.Elements.Table.TableColumn.Relative(1));
        for (int index = 0; index < rowCount; index++)
        {
            table.Rows.Add(new TableRow(new TableCell
            {
                ContentFactory = () =>
                {
                    factoryCalls++;
                    return new FixedHeightComponent(20);
                }
            }));
        }

        var context = new LayoutMeasureContext(page, new FlowColumn(0, 0, 160, 60, 0), page.LayoutOptions);
        var drawContext = new LayoutDrawContext(page, context.Column, 0, 60, 160, page.LayoutOptions);
        var component = new TableComponent(table);
        LayoutMeasurement measurement = component.Measure(context);
        factoryCalls.Should().Be(rowCount);
        var measuredMetrics = new PdfBuilder.Models.PdfGenerationMetrics();
        document.TableLayoutDiagnostics.CopyTo(measuredMetrics);
        measuredMetrics.TableMeasurementCount.Should().Be(1);
        measuredMetrics.TableRowMeasurementCount.Should().Be(rowCount);
        measuredMetrics.TableCellMeasurementCount.Should().Be(rowCount);

        foreach (TableCell cell in table.Rows.SelectMany(row => row.Cells))
        {
            cell.MeasuredContent = null;
            cell.MeasuredContentLayout = null;
            cell.CachedContentHeight = 0;
        }

        while (true)
        {
            TableSegmentElement segmentElement = measurement.Metadata.Should().BeOfType<TableSegmentElement>().Subject;
            segmentElement.Rows.SelectMany(row => row.Cells).Should().OnlyContain(cell =>
                cell.Content != null && cell.Measurement != null);
            component.Draw(drawContext, measurement);
            page.Elements.OfType<TableSegmentElement>().Last().Should().BeSameAs(segmentElement);
            page.Elements.OfType<TableElement>().Should().BeEmpty();
            segmentElement.SourceTable.Should().BeSameAs(table);

            factoryCalls.Should().Be(rowCount);
            var drawMetrics = new PdfBuilder.Models.PdfGenerationMetrics();
            document.TableLayoutDiagnostics.CopyTo(drawMetrics);
            drawMetrics.TableMeasurementCount.Should().Be(measuredMetrics.TableMeasurementCount);
            drawMetrics.TableRowMeasurementCount.Should().Be(measuredMetrics.TableRowMeasurementCount);
            drawMetrics.TableCellMeasurementCount.Should().Be(measuredMetrics.TableCellMeasurementCount);
            drawMetrics.ContentFactoryInvocationCount.Should().Be(measuredMetrics.ContentFactoryInvocationCount);
            if (measurement.Remainder == null)
                break;

            component = measurement.Remainder.Should().BeOfType<TableComponent>().Subject;
            measurement = component.Measure(context);
        }

        var metrics = new PdfBuilder.Models.PdfGenerationMetrics();
        document.TableLayoutDiagnostics.CopyTo(metrics);
        metrics.TableMeasurementCount.Should().Be(1);
        metrics.TableCellMeasurementCount.Should().Be(rowCount);
        metrics.ContentFactoryInvocationCount.Should().Be(rowCount);
        metrics.TableCellDrawBufferAllocationCount.Should().Be(page.Elements.OfType<TableSegmentElement>().LongCount());
        metrics.TableCellDrawBufferAllocationCount.Should().BeLessThan(metrics.TableCellMeasurementCount);
    }

    [Fact]
    public void CloneTableWithRows_ClonesStructureAndOnlyRequestedRows()
    {
        var source = new TableElement
        {
            TableWidth = 420,
            CaptionText = "Clone fixture",
            DefaultFont = "Times-Roman",
            BorderWidth = 1.5f,
            ResolvedColumnWidths = [280, 140]
        };
        source.ColumnWidths.AddRange([280, 140]);
        source.Rows.Add(new TableRow(new TableCell("first"), new TableCell("1")));
        source.Rows.Add(new TableRow(new TableCell("second"), new TableCell("2")));
        source.Rows.Add(new TableRow(new TableCell("third"), new TableCell("3")));
        var diagnostics = new TableLayoutDiagnosticsSession { Enabled = true };
        source.LayoutDiagnostics = diagnostics;

        TableElement structure = LayoutSplitUtils.CloneTableStructure(source);
        TableElement selected = LayoutSplitUtils.CloneTableWithRows(source, [source.Rows[1]]);
        TableElement full = LayoutSplitUtils.CloneTable(source);

        structure.Rows.Should().BeEmpty();
        structure.TableWidth.Should().Be(source.TableWidth);
        structure.CaptionText.Should().Be(source.CaptionText);
        structure.DefaultFont.Should().Be(source.DefaultFont);
        structure.BorderWidth.Should().Be(source.BorderWidth);
        structure.ColumnWidths.Should().Equal(source.ColumnWidths);
        structure.ResolvedColumnWidths.Should().Equal(source.ResolvedColumnWidths!);
        structure.ResolvedColumnWidths.Should().NotBeSameAs(source.ResolvedColumnWidths);

        selected.Rows.Should().ContainSingle();
        selected.Rows[0].Cells.Select(cell => cell.Text).Should().Equal("second", "2");
        selected.Rows[0].Should().NotBeSameAs(source.Rows[1]);
        selected.Rows[0].Cells[0].Should().NotBeSameAs(source.Rows[1].Cells[0]);

        full.Rows.Should().HaveCount(source.Rows.Count);
        full.Rows.Should().NotContain(source.Rows[0]);

        var metrics = new PdfBuilder.Models.PdfGenerationMetrics();
        diagnostics.CopyTo(metrics);
        metrics.TableCloneCount.Should().Be(3);
        metrics.TableRowCloneCount.Should().Be(4);
    }

    [Fact]
    public void Table_Remainder_UsesOriginalTableAndBodyRowCursor()
    {
        var document = new PdfDocument();
        document.LayoutOptions.Diagnostics.EnableTableLayoutCounters = true;
        PdfPage page = document.AddPage(200, 100);
        var table = new TableElement { TableWidth = 160 };
        table.ColumnDefinitions.Add(PdfBuilder.Elements.Table.TableColumn.Relative(1));
        for (int index = 0; index < 10; index++)
            table.Rows.Add(new TableRow(new TableCell($"row-{index}")) { RowHeight = 20 });

        var component = new TableComponent(table);
        var context = new LayoutMeasureContext(page, new FlowColumn(0, 0, 160, 100, 0), page.LayoutOptions);

        LayoutMeasurement firstPage = component.Measure(context);
        var remainder = firstPage.Remainder.Should().BeOfType<TableComponent>().Subject;
        TableSegmentElement firstSegment = firstPage.Metadata.Should().BeOfType<TableSegmentElement>().Subject;

        firstPage.IsPartial.Should().BeTrue();
        firstSegment.SourceTable.Should().BeSameAs(table);
        firstSegment.Segment.StartBodyRow.Should().Be(0);
        firstSegment.Segment.BodyRowCount.Should().Be(remainder.StartBodyRow);
        firstSegment.Segment.IncludeHeader.Should().BeFalse();
        firstSegment.Segment.IncludeFooter.Should().BeFalse();
        firstSegment.Segment.IncludeCaption.Should().BeFalse();
        component.LayoutPlan.Should().NotBeNull();
        component.LayoutPlan!.Width.Should().Be(160);
        component.LayoutPlan.ColumnWidths.Should().ContainSingle().Which.Should().Be(160);
        component.LayoutPlan.HeaderRows.Should().BeEmpty();
        component.LayoutPlan.BodyRows.Should().HaveCount(table.Rows.Count);
        component.LayoutPlan.FooterRows.Should().BeEmpty();
        component.LayoutPlan.BlockedBreaks.Should().HaveCount(table.Rows.Count).And.OnlyContain(value => !value);
        component.LayoutPlan.BodyRows.Should().OnlyContain(row => row.Height > 0 && row.Cells.Length == 1 && row.Cells[0].Width == 160);
        component.LayoutPlan.HeaderHeight.Should().Be(0);
        component.LayoutPlan.FooterHeight.Should().Be(0);
        component.LayoutPlan.GetRemainingBodyHeight(0).Should().BeApproximately(component.LayoutPlan.BodyRows.Sum(row => row.Height), 0.001f);
        component.LayoutPlan.GetRemainingBodyHeight(5).Should().BeApproximately(component.LayoutPlan.BodyRows.Skip(5).Sum(row => row.Height), 0.001f);
        remainder.SourceTable.Should().BeSameAs(table);
        remainder.StartBodyRow.Should().BeGreaterThan(0).And.BeLessThan(table.Rows.Count);
        remainder.HasPendingBodyRow.Should().BeFalse();

        var metrics = new PdfBuilder.Models.PdfGenerationMetrics();
        document.TableLayoutDiagnostics.CopyTo(metrics);
        metrics.TableCloneCount.Should().Be(0);
        metrics.TableRowCloneCount.Should().Be(0);

        int previousStart = remainder.StartBodyRow;
        TableComponent current = remainder;
        TableLayoutPlan firstPlan = component.LayoutPlan;
        var narrowerContext = new LayoutMeasureContext(page, new FlowColumn(0, 0, 120, 100, 0), page.LayoutOptions);
        while (true)
        {
            LayoutMeasurement pageMeasurement = current.Measure(narrowerContext);
            current.LayoutPlan.Should().BeSameAs(firstPlan);
            pageMeasurement.UsedWidth.Should().Be(160);
            if (pageMeasurement.Remainder == null)
                break;

            current = pageMeasurement.Remainder.Should().BeOfType<TableComponent>().Subject;
            current.SourceTable.Should().BeSameAs(table);
            current.StartBodyRow.Should().BeGreaterThan(previousStart);
            current.HasPendingBodyRow.Should().BeFalse();
            previousStart = current.StartBodyRow;
        }

        document.TableLayoutDiagnostics.CopyTo(metrics);
        metrics.TableMeasurementCount.Should().Be(1);
        metrics.TableRowMeasurementCount.Should().Be(table.Rows.Count);
        metrics.TableCellMeasurementCount.Should().Be(table.Rows.Count);
    }

    [Fact]
    public void Table_NearPageBottom_MovesOrSplitsCorrectly()
    {
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Margin(40);
            page.Content().Column(column =>
            {
                for (var index = 0; index < 34; index++)
                    column.Item().Text($"leading-{index}");
                column.Item().Table(table =>
                {
                    table.Columns(columns => columns.RelativeColumn());
                    for (var index = 0; index < 25; index++)
                        table.Row(row => row.Cell().Text($"near-bottom-{index}"));
                });
            });
        }));

        document.Pages.Count.Should().BeGreaterThan(1);
        string text = string.Join("\n", PdfTextExtractor.ExtractTextBlocks(PdfContentHelper.Generate(document)));
        text.Should().Contain("near-bottom-24");
    }

    [Fact]
    public void Table_EmptyTable_RendersDefinedEmptyState()
    {
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Content().Table(table => table.Columns(columns => columns.RelativeColumn()));
        }));

        document.Invoking(value => PdfContentHelper.Generate(value)).Should().NotThrow();
    }

    [Fact]
    public void Table_HeaderOnly_DoesNotLoop()
    {
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Header(row => row.Cell().Text("Only header").Bold());
            });
        }));

        document.Pages.Should().ContainSingle();
        string.Join("\n", PdfTextExtractor.ExtractTextBlocks(PdfContentHelper.Generate(document))).Should().Contain("Only header");
    }

    [Fact]
    public void Table_OversizedRow_ThrowsOrSplitsAccordingToPolicy()
    {
        Action action = () => PdfDocument.Create(document => document.Page(page =>
        {
            page.Margin(40);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Row(row => row.Cell().Text(string.Join("\n", Enumerable.Repeat("oversized", 1_000))));
            });
        }));

        action.Should().Throw<PdfTableRowSplitException>()
            .Which.Reason.Should().Be("row-splitting-disabled");
    }

    [Fact]
    public void Writer_DoesNotInvokeLegacyTablePaginator()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Writer", "PdfWriter.cs")));
        source.Should().NotContain("TablePaginator.Paginate");
    }

    private static PdfDocument CreateDocument(int rows, bool includeFollowingText = false, bool enableTableLayoutCounters = false)
    {
        return PdfDocument.Create(document =>
        {
            if (enableTableLayoutCounters)
                document.Diagnostics(options => options.EnableTableLayoutCounters = true);
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Content().Column(column =>
                {
                    column.Item().Table(table =>
                    {
                        table.CellPadding(3);
                        table.Border(0.5f, "#000000");
                        table.HeaderBackground("#E8EEF7");
                        table.Columns(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(90);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Text("Description").Bold();
                            header.Cell().AlignRight().Text("Quantity").Bold();
                            header.Cell().AlignRight().Text("Amount").Bold();
                        });
                        for (var index = 0; index < rows; index++)
                        {
                            table.Row(row =>
                            {
                                row.Cell().Text($"row-{index}");
                                row.Cell().AlignRight().Text(index, "N0");
                                row.Cell().AlignRight().Text(index + 0.5m, "N2");
                            });
                        }
                    });
                    if (includeFollowingText)
                        column.Item().Text("After table").Bold();
                });
            });
        });
    }

    private sealed class FixedHeightComponent(float height) : IMeasurable
    {
        public LayoutMeasurement Measure(LayoutMeasureContext context)
            => new(0, height, 0, context.AvailableWidth);

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) { }
    }
}
