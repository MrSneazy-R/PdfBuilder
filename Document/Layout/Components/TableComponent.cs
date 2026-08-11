using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout.Components;

internal sealed class TableComponent : IMeasurable
{
    private const float Epsilon = 0.1f;

    private readonly TableElement _workingTable;
    private readonly TableElement _rootTable;
    private readonly bool _isContinuation;

    public TableComponent(TableElement table)
        : this(table ?? throw new ArgumentNullException(nameof(table)), table, isContinuation: false)
    {
    }

    private TableComponent(TableElement workingTable, TableElement rootTable, bool isContinuation)
    {
        _workingTable = workingTable;
        _rootTable = rootTable;
        _isContinuation = isContinuation;
    }

    public LayoutMeasurement Measure(LayoutMeasureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TableGridValidator.Validate(_rootTable);
        TableGridValidator.Validate(_workingTable);

        float width = ResolveWidth(context.AvailableWidth);
        EnsureStableColumnWidths(width);

        List<TableRow> headers = _rootTable.Rows.Where(row => row.IsHeader).ToList();
        List<TableRow> footers = _rootTable.Rows.Where(row => row.IsFooter).ToList();
        List<TableRow> body = _workingTable.Rows.Where(row => !row.IsHeader && !row.IsFooter).ToList();
        bool showHeader = headers.Count > 0 && (!_isContinuation || _rootTable.RepeatHeaders);
        IReadOnlyList<TableRow> segmentHeaders = showHeader ? headers : Array.Empty<TableRow>();
        float captionHeight = _isContinuation ? 0f : ComputeCaptionHeight(_workingTable);

        float[] headerHeights = showHeader ? MeasureRows(headers, width, context).RowHeights : Array.Empty<float>();
        float[] footerHeights = footers.Count > 0 ? MeasureRows(footers, width, context).RowHeights : Array.Empty<float>();
        float[] bodyHeights = body.Count > 0 ? MeasureRows(body, width, context).RowHeights : Array.Empty<float>();

        float pageContentHeight = context.Column.TopY - context.Column.BottomY;
        if (headerHeights.Concat(footerHeights).Concat(bodyHeights).Any(height => height > pageContentHeight + Epsilon))
        {
            throw new InvalidOperationException(
                "A table row is larger than the available page height and row splitting is not enabled.");
        }

        float headerHeight = Sum(headerHeights);
        float footerHeight = Sum(footerHeights);
        float bodyHeight = Sum(bodyHeights);
        float completeHeight = captionHeight + headerHeight + bodyHeight + footerHeight;

        if (completeHeight <= context.AvailableHeight + Epsilon)
            return FullMeasurement(width, completeHeight, BuildSegment(segmentHeaders, body, footers, width, includeCaption: captionHeight > 0f));

        if (!_workingTable.EnablePageBreaks || _workingTable.AvoidBreakInside || context.AvailableHeight <= Epsilon)
            return LayoutMeasurement.Wrap(width);

        float reserved = captionHeight + headerHeight + footerHeight;
        if (reserved > context.AvailableHeight + Epsilon)
            return WrapOrThrow(context, width, "Table header/footer groups leave no usable space for a body row.");

        int rowsTaken = FindRowsToTake(body, bodyHeights, context.AvailableHeight - reserved);
        if (rowsTaken <= 0)
            return WrapOrThrow(context, width, "Table widow/orphan or keep-with-next constraints prevent a valid page break.");

        if (rowsTaken >= body.Count)
            return FullMeasurement(width, completeHeight, BuildSegment(segmentHeaders, body, footers, width, includeCaption: captionHeight > 0f));

        bool repeatFooter = _rootTable.FooterRepeatMode == TableFooterRepeatMode.EveryPage
            || (_rootTable.FooterRepeatMode == TableFooterRepeatMode.ContinuationPages && _isContinuation);
        List<TableRow> segmentFooter = repeatFooter ? footers : [];
        List<TableRow> segmentBody = body.Take(rowsTaken).ToList();
        float segmentHeight = captionHeight + headerHeight + Sum(bodyHeights.Take(rowsTaken)) + (repeatFooter ? footerHeight : 0f);
        TableElement segment = BuildSegment(segmentHeaders, segmentBody, segmentFooter, width, includeCaption: captionHeight > 0f);
        TableComponent remainder = CreateRemainder(body.Skip(rowsTaken).ToList(), rowsTaken);

        return new LayoutMeasurement(
            marginTop: 0f,
            contentHeight: segmentHeight,
            marginBottom: 0f,
            usedWidth: width,
            metadata: new TableLayoutMetadata(segment, width),
            avoidBreakInside: _workingTable.AvoidBreakInside,
            result: LayoutResultKind.Partial,
            remainder: remainder);
    }

    public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(measurement);
        if (measurement.Metadata is not TableLayoutMetadata metadata)
            throw new InvalidOperationException("Table measurement metadata missing.");

        TableElement element = metadata.Element;
        element.EnablePageBreaks = false;
        element.X = context.ContentLeft;
        element.Y = context.ContentTop;
        element.TableWidth ??= metadata.Width;
        element.PageTopY = context.Column.TopY;
        element.PageBottomY = context.Column.BottomY;
        context.Page.AddElement(element);
        TableCellContainerLayout.AddContentGroups(element, context.Page, context.Options);
    }

    private LayoutMeasurement FullMeasurement(float width, float height, TableElement element)
        => new(
            marginTop: 0f,
            contentHeight: height,
            marginBottom: 0f,
            usedWidth: width,
            metadata: new TableLayoutMetadata(element, width),
            avoidBreakInside: _workingTable.AvoidBreakInside,
            result: LayoutResultKind.Full,
            remainder: null);

    private LayoutMeasurement WrapOrThrow(LayoutMeasureContext context, float width, string message)
    {
        float fullPageHeight = context.Column.TopY - context.Column.BottomY;
        if (context.AvailableHeight + Epsilon < fullPageHeight)
            return LayoutMeasurement.Wrap(width);
        throw new InvalidOperationException(message + " Reduce the configured row constraints or repeated table groups.");
    }

    private int FindRowsToTake(IReadOnlyList<TableRow> rows, IReadOnlyList<float> heights, float budget)
    {
        int count = 0;
        float used = 0f;
        while (count < heights.Count && used + heights[count] <= budget + Epsilon)
        {
            used += heights[count];
            count++;
        }
        if (count == 0 || count >= rows.Count)
            return count;

        bool[] blockedBreaks = ComputeBlockedBreaks(rows);
        while (count > 0 && blockedBreaks[count - 1]) count--;
        if (count == 0)
            return 0;

        int minimumAtEnd = Math.Min(_rootTable.MinRowsAtPageEnd, rows.Count);
        if (count < minimumAtEnd)
            return 0;

        int remaining = rows.Count - count;
        int minimumAtStart = Math.Min(_rootTable.MinRowsAtPageStart, rows.Count);
        if (remaining > 0 && remaining < minimumAtStart)
        {
            count -= minimumAtStart - remaining;
            while (count > 0 && blockedBreaks[count - 1]) count--;
        }

        return Math.Max(0, count);
    }

    private static bool[] ComputeBlockedBreaks(IReadOnlyList<TableRow> rows)
    {
        var blocked = new bool[rows.Count];
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (rows[rowIndex].KeepWithNext && rowIndex < rows.Count - 1)
                blocked[rowIndex] = true;

            foreach (TableCell cell in rows[rowIndex].Cells)
            {
                int finalCoveredRow = Math.Min(rows.Count - 1, rowIndex + Math.Max(1, cell.RowSpan) - 1);
                for (int breakAfter = rowIndex; breakAfter < finalCoveredRow; breakAfter++)
                    blocked[breakAfter] = true;
            }
        }
        return blocked;
    }

    private TableElement BuildSegment(
        IReadOnlyList<TableRow> headers,
        IReadOnlyList<TableRow> body,
        IReadOnlyList<TableRow> footers,
        float width,
        bool includeCaption)
    {
        var rows = new List<TableRow>(headers.Count + body.Count + footers.Count);
        rows.AddRange(headers);
        rows.AddRange(body);
        rows.AddRange(footers);
        var segment = LayoutSplitUtils.CloneTableWithRows(_rootTable, rows);
        segment.EnablePageBreaks = false;
        segment.TableWidth = width;
        segment.HeaderRowCount = headers.Count;
        segment.ResolvedColumnWidths = _rootTable.ResolvedColumnWidths?.ToArray();
        if (!includeCaption) segment.CaptionText = null;
        TableGridValidator.Validate(segment);
        return segment;
    }

    private TableComponent CreateRemainder(List<TableRow> remainingBody, int rowsConsumed)
    {
        var remainderTable = LayoutSplitUtils.CloneTableWithRows(_workingTable, remainingBody);
        remainderTable.CaptionText = null;
        remainderTable.HeaderRowCount = 0;
        remainderTable.EnablePageBreaks = _workingTable.EnablePageBreaks;
        remainderTable.RowBandOffset = _workingTable.RowBandOffset + rowsConsumed;
        remainderTable.TableWidth = _workingTable.TableWidth;
        remainderTable.ResolvedColumnWidths = _rootTable.ResolvedColumnWidths?.ToArray();
        return new TableComponent(remainderTable, _rootTable, isContinuation: true);
    }

    private TableMeasurementHelper.TableMetrics MeasureRows(IReadOnlyList<TableRow> rows, float width, LayoutMeasureContext context)
    {
        var table = LayoutSplitUtils.CloneTableWithRows(_rootTable, rows);
        table.CaptionText = null;
        table.TableWidth = width;
        table.ResolvedColumnWidths = _rootTable.ResolvedColumnWidths?.ToArray();
        return TableMeasurementHelper.Measure(table, width, context.Page, context.Options);
    }

    private void EnsureStableColumnWidths(float width)
    {
        if (_rootTable.ResolvedColumnWidths is { Length: > 0 })
        {
            _workingTable.ResolvedColumnWidths = _rootTable.ResolvedColumnWidths.ToArray();
            return;
        }

        int totalColumns = _rootTable.ColumnDefinitions.Count > 0
            ? _rootTable.ColumnDefinitions.Count
            : _rootTable.Rows.Max(row => row.Cells.Sum(cell => Math.Max(1, cell.ColSpan)));
        _rootTable.ResolvedColumnWidths = TableColumnWidthCalculator.Calculate(_rootTable, totalColumns, width);
        _workingTable.ResolvedColumnWidths = _rootTable.ResolvedColumnWidths.ToArray();
    }

    private float ResolveWidth(float availableWidth)
        => _workingTable.TableWidth is > 0f ? Math.Min(availableWidth, _workingTable.TableWidth.Value) : availableWidth;

    private static float ComputeCaptionHeight(TableElement table)
        => string.IsNullOrWhiteSpace(table.CaptionText)
            ? 0f
            : Math.Max(table.DefaultFontSize, 11f) * Writer.PdfDefaults.LineHeightMultiplier + 4f;

    private static float Sum(IEnumerable<float> values) => values.Sum();

    private sealed class TableLayoutMetadata
    {
        public TableLayoutMetadata(TableElement element, float width) { Element = element; Width = width; }
        public TableElement Element { get; }
        public float Width { get; }
    }
}
