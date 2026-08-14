using System.Drawing;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Elements.Table;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class CanonicalTableRowContinuationTests
{
    [Fact]
    public void Table_OptInRichTextRow_ContinuesAcrossPages()
    {
        PdfDocument document = CreateSplitDocument();
        byte[] bytes = document.GenerateBytes();
        string extracted = string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes));

        document.Pages.Count.Should().BeGreaterThan(2);
        extracted.Should().Contain("BEGIN-CONTINUED-CONTENT")
            .And.Contain("END-CONTINUED-CONTENT");
        CountOccurrences(extracted, "BEGIN-CONTINUED-CONTENT").Should().Be(1);
        CountOccurrences(extracted, "END-CONTINUED-CONTENT").Should().Be(1);
    }

    [Fact]
    public void Table_OptInRichTextRow_GeneratesDeterministically()
    {
        PdfDocument firstDocument = CreateSplitDocument();
        PdfDocument secondDocument = CreateSplitDocument();
        MakeDeterministic(firstDocument);
        MakeDeterministic(secondDocument);
        byte[] first = firstDocument.GenerateBytes();
        byte[] second = secondDocument.GenerateBytes();

        second.Should().Equal(first);
    }

    [Fact]
    public void Table_RowOverride_CanEnableOrDisableSplitting()
    {
        Action enabled = () => CreateOversizedDocument(
            configureTable: _ => { },
            configureRow: row => row.AllowSplit());
        Action disabled = () => CreateOversizedDocument(
            configureTable: table => table.AllowRowSplitting(),
            configureRow: row => row.AllowSplit(false));

        enabled.Should().NotThrow();
        disabled.Should().Throw<PdfTableRowSplitException>()
            .Which.Reason.Should().Be("row-splitting-disabled");
    }

    [Fact]
    public void Table_SplitSegments_PreserveGroupsAndDefineContinuationEdges()
    {
        PdfDocument document = CreateSplitDocument();
        document.GenerateBytes().Should().NotBeEmpty();

        List<TableSegmentElement> segments = document.Pages
            .SelectMany(page => page.Elements)
            .OfType<TableSegmentElement>()
            .ToList();
        segments.Should().HaveCount(document.Pages.Count);
        segments.Should().OnlyContain(segment => segment.Segment.IncludeHeader);
        segments.Should().OnlyContain(segment => segment.Segment.IncludeFooter);

        List<TableCell> bodyCells = segments
            .Select(segment => segment.Rows.Single(row => !row.Row.IsHeader && !row.Row.IsFooter).Row.Cells.Single())
            .ToList();
        bodyCells.Should().HaveCountGreaterThan(1);
        bodyCells[0].BorderTop.Should().BeTrue();
        bodyCells[0].BorderBottom.Should().BeFalse();
        bodyCells[0].PaddingTop.Should().Be(6);
        bodyCells[0].PaddingBottom.Should().Be(0);
        bodyCells[^1].BorderTop.Should().BeFalse();
        bodyCells[^1].BorderBottom.Should().BeTrue();
        bodyCells[^1].PaddingTop.Should().Be(0);
        bodyCells[^1].PaddingBottom.Should().Be(6);
        bodyCells.Should().OnlyContain(cell => cell.BackgroundColor.HasValue
            && cell.BackgroundColor.Value.ToArgb() == Color.AliceBlue.ToArgb());
    }

    [Fact]
    public void Table_ContainerTextBaseline_RemainsInsideCellClip()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Row(row => row.Cell().Padding(2).Text("Ascenders stay visible"));
            })));
        document.GenerateBytes().Should().NotBeEmpty();

        ClipGroupElement group = document.Pages.SelectMany(page => page.Elements)
            .OfType<ClipGroupElement>()
            .Single();
        TextElement text = group.Children.OfType<TextElement>().Single();
        float ascent = text.ShapedLayout!.Lines[text.ShapedStartLine].Ascent;

        (text.Y + ascent).Should().BeLessThanOrEqualTo(group.Y + group.Height + 0.1f);
    }

    [Fact]
    public void Table_OversizedFixedHeight_ThrowsStructuredException()
    {
        Action action = () => PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(new PageSize(240, 180));
            page.Margin(18);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.AllowRowSplitting();
                table.Row(row => row.Height(500).AllowSplit().Cell().RichText(LongRichText));
            });
        }));

        PdfTableRowSplitException exception = action.Should().Throw<PdfTableRowSplitException>().Which;
        exception.RowIndex.Should().Be(0);
        exception.Reason.Should().Be("fixed-row-height");
    }

    [Fact]
    public void Table_OversizedUnsplittableCell_ThrowsStructuredException()
    {
        Action action = () => PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(new PageSize(240, 180));
            page.Margin(18);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.AllowRowSplitting();
                table.Row(row => row.Cell().Image(TestImage, 24, 500));
            });
        }));

        PdfTableRowSplitException exception = action.Should().Throw<PdfTableRowSplitException>().Which;
        exception.ColumnIndex.Should().Be(0);
        exception.Reason.Should().Be("unsplittable-content");
    }

    [Fact]
    public void Table_SplitRowWithRowSpan_FailsExplicitly()
    {
        Action action = () => PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(new PageSize(240, 180));
            page.Margin(18);
            page.Content().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.AllowRowSplitting();
                table.Row(row =>
                {
                    row.Cell().RowSpan(2).RichText(LongRichText);
                    row.Cell().Text("first");
                });
                table.Row(row => row.Cell().Position(1).Text("second"));
            });
        }));

        action.Should().Throw<PdfTableRowSplitException>()
            .Which.Reason.Should().Be("row-span");
    }

    [Fact]
    public void Table_SplitRowWithColumnSpan_RemainsSupported()
    {
        Action action = () => PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(new PageSize(240, 180));
            page.Margin(18);
            page.Content().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.AllowRowSplitting();
                table.Row(row => row.Cell().ColumnSpan(2).RichText(LongRichText));
            });
        }));

        action.Should().NotThrow();
    }

    [Fact]
    public void Table_ZeroProgressContinuation_CountsAgainstRenderLimit()
    {
        var document = new PdfDocument();
        document.RenderLimits.MaximumLayoutIterations = 1;
        PdfPage page = document.AddPage();
        var table = new TableElement { AllowRowSplitting = true };
        table.ColumnDefinitions.Add(TableColumn.Relative(1f));
        table.Rows.Add(new TableRow(new TableCell
        {
            ContentFactory = static () => new ZeroProgressComponent()
        })
        { AllowSplit = true });
        var component = new TableComponent(table);
        var flow = new FlowColumn(0, 0, 160, 100, 0);
        var context = new LayoutMeasureContext(page, flow, page.LayoutOptions);

        component.Measure(context).IsWrap.Should().BeTrue();
        Action secondAttempt = () => component.Measure(context);

        secondAttempt.Should().Throw<PdfRenderLimitException>()
            .Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumLayoutIterations));
    }

    private static PdfDocument CreateSplitDocument()
        => PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(new PageSize(240, 180));
            page.Margin(18);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.CellPadding(6);
                table.AllowRowSplitting();
                table.RepeatHeaders();
                table.RepeatFooters(TableFooterRepeatMode.EveryPage);
                table.Header(row => row.Cell().Text("CONTINUATION HEADER"));
                table.Row(row => row.Cell()
                    .Background("#F0F8FF")
                    .Border(1, "#123456")
                    .CornerRadius(4)
                    .RichText(paragraph =>
                    {
                        paragraph.Span("BEGIN-CONTINUED-CONTENT ").Bold();
                        paragraph.Span(string.Join(" ", Enumerable.Repeat("splittable", 220)));
                        paragraph.Span(" END-CONTINUED-CONTENT").Italic();
                    }));
                table.Footer(row => row.Cell().Text("CONTINUATION FOOTER"));
            });
        }));

    private static PdfDocument CreateOversizedDocument(
        Action<ITableDescriptor> configureTable,
        Action<ITableRowDescriptor> configureRow)
        => PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(new PageSize(240, 180));
            page.Margin(18);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                configureTable(table);
                table.Row(row =>
                {
                    configureRow(row);
                    row.Cell().RichText(LongRichText);
                });
            });
        }));

    private static void LongRichText(IRichTextDescriptor paragraph)
    {
        paragraph.Span(string.Join(" ", Enumerable.Repeat("continued content", 200)));
    }

    private static int CountOccurrences(string value, string marker)
        => value.Split(marker, StringSplitOptions.None).Length - 1;

    private static void MakeDeterministic(PdfDocument document)
    {
        document.GenerationOptions.Deterministic = true;
        document.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.ModificationTime = DateTimeOffset.UnixEpoch;
    }

    private static readonly byte[] TestImage = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private sealed class ZeroProgressComponent : IMeasurable
    {
        public LayoutMeasurement Measure(LayoutMeasureContext context)
            => context.AvailableHeight > 10_000
                ? new LayoutMeasurement(0, 1_000, 0, context.AvailableWidth)
                : new LayoutMeasurement(0, 0, 0, context.AvailableWidth, result: LayoutResultKind.Partial, remainder: this);

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) { }
    }
}
