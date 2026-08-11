using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout.Components;

internal sealed class TableComponent : IMeasurable
{
    private const float Epsilon = 0.1f;

    private readonly TableElement _workingTable;
    private readonly TableElement _rootTable;
    private readonly bool _isContinuation;
    private readonly SplitProgressState _splitProgress;

    public TableComponent(TableElement table)
        : this(table ?? throw new ArgumentNullException(nameof(table)), table, isContinuation: false, new SplitProgressState())
    {
    }

    private TableComponent(TableElement workingTable, TableElement rootTable, bool isContinuation, SplitProgressState splitProgress)
    {
        _workingTable = workingTable;
        _rootTable = rootTable;
        _isContinuation = isContinuation;
        _splitProgress = splitProgress;
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

        float headerHeight = Sum(headerHeights);
        float footerHeight = Sum(footerHeights);
        float bodyHeight = Sum(bodyHeights);
        float completeHeight = captionHeight + headerHeight + bodyHeight + footerHeight;
        float pageContentHeight = context.Column.TopY - context.Column.BottomY;
        float maximumBodyHeight = Math.Max(0f, pageContentHeight - captionHeight - headerHeight - footerHeight);
        ValidateOversizedRows(headers, headerHeights, pageContentHeight, "header");
        ValidateOversizedRows(footers, footerHeights, pageContentHeight, "footer");
        ValidateBodySplitPolicies(body, bodyHeights, maximumBodyHeight);

        if (completeHeight <= context.AvailableHeight + Epsilon)
            return FullMeasurement(width, completeHeight, BuildSegment(segmentHeaders, body, footers, width, includeCaption: captionHeight > 0f));

        if (!_workingTable.EnablePageBreaks || _workingTable.AvoidBreakInside || context.AvailableHeight <= Epsilon)
            return LayoutMeasurement.Wrap(width);

        float reserved = captionHeight + headerHeight + footerHeight;
        if (reserved > context.AvailableHeight + Epsilon)
            return WrapOrThrow(context, width, "Table header/footer groups leave no usable space for a body row.");

        int rowsTaken = FindRowsToTake(body, bodyHeights, context.AvailableHeight - reserved);
        if (rowsTaken <= 0)
        {
            if (body.Count > 0 && bodyHeights[0] > maximumBodyHeight + Epsilon && IsRowSplittable(body[0]))
            {
                if (!IsFullPage(context))
                    return LayoutMeasurement.Wrap(width);
                return SplitFirstBodyRow(
                    context,
                    width,
                    segmentHeaders,
                    footers,
                    body,
                    context.AvailableHeight - reserved,
                    captionHeight,
                    headerHeight,
                    footerHeight);
            }

            if (captionHeight > 0f)
                return LayoutMeasurement.Wrap(width);
            return WrapOrThrow(context, width, "Table widow/orphan or keep-with-next constraints prevent a valid page break.");
        }

        if (rowsTaken >= body.Count)
            return FullMeasurement(width, completeHeight, BuildSegment(segmentHeaders, body, footers, width, includeCaption: captionHeight > 0f));

        bool repeatFooter = _rootTable.FooterRepeatMode == TableFooterRepeatMode.EveryPage
            || (_rootTable.FooterRepeatMode == TableFooterRepeatMode.ContinuationPages && _isContinuation);
        List<TableRow> segmentFooter = repeatFooter ? footers : [];
        List<TableRow> segmentBody = body.Take(rowsTaken).ToList();
        float segmentHeight = captionHeight + headerHeight + Sum(bodyHeights.Take(rowsTaken)) + (repeatFooter ? footerHeight : 0f);
        TableElement segment = BuildSegment(segmentHeaders, segmentBody, segmentFooter, width, includeCaption: captionHeight > 0f);
        TableComponent remainder = CreateRemainder(body.Skip(rowsTaken).ToList(), rowsTaken);

        return PartialMeasurement(width, segmentHeight, segment, remainder);
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

    private LayoutMeasurement PartialMeasurement(float width, float height, TableElement element, TableComponent remainder)
        => new(
            marginTop: 0f,
            contentHeight: height,
            marginBottom: 0f,
            usedWidth: width,
            metadata: new TableLayoutMetadata(element, width),
            avoidBreakInside: _workingTable.AvoidBreakInside,
            result: LayoutResultKind.Partial,
            remainder: remainder);

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
        return new TableComponent(remainderTable, _rootTable, isContinuation: true, _splitProgress);
    }

    private LayoutMeasurement SplitFirstBodyRow(
        LayoutMeasureContext context,
        float width,
        IReadOnlyList<TableRow> headers,
        IReadOnlyList<TableRow> footers,
        IReadOnlyList<TableRow> body,
        float availableBodyHeight,
        float captionHeight,
        float headerHeight,
        float footerHeight)
    {
        int rowIndex = body[0].BandIndex ?? _workingTable.RowBandOffset;
        SplitRowResult split = SplitRow(body[0], rowIndex, availableBodyHeight, context);
        if (!split.MadeProgress)
        {
            RecordZeroProgress(context, rowIndex);
            return LayoutMeasurement.Wrap(width);
        }

        _splitProgress.ZeroProgressAttempts = 0;
        var remaining = new List<TableRow>();
        if (split.Remainder != null) remaining.Add(split.Remainder);
        remaining.AddRange(body.Skip(1));

        bool hasRemainder = remaining.Count > 0;
        bool repeatFooter = !hasRemainder
            || _rootTable.FooterRepeatMode == TableFooterRepeatMode.EveryPage
            || (_rootTable.FooterRepeatMode == TableFooterRepeatMode.ContinuationPages && _isContinuation);
        IReadOnlyList<TableRow> segmentFooter = repeatFooter ? footers : Array.Empty<TableRow>();
        float segmentHeight = captionHeight + headerHeight + split.Height + (repeatFooter ? footerHeight : 0f);
        TableElement segment = BuildSegment(headers, [split.Segment], segmentFooter, width, includeCaption: captionHeight > 0f);

        if (!hasRemainder)
            return FullMeasurement(width, segmentHeight, segment);

        TableComponent remainder = CreateRemainder(remaining, split.Remainder == null ? 1 : 0);
        return PartialMeasurement(width, segmentHeight, segment, remainder);
    }

    private SplitRowResult SplitRow(TableRow source, int rowIndex, float availableHeight, LayoutMeasureContext context)
    {
        if (source.RowHeight.HasValue)
            throw SplitFailure(rowIndex, null, "fixed-row-height", "Remove the exact row height before enabling row continuation.");
        if (source.Cells.Any(cell => cell.RowSpan > 1))
            throw SplitFailure(rowIndex, null, "row-span", "Rows containing RowSpan greater than one cannot be split. Restructure the span or keep the row atomic.");

        TableRow segment = CloneSingleRow(source);
        TableRow remainder = CloneSingleRow(source);
        float[] widths = _rootTable.ResolvedColumnWidths ?? throw new InvalidOperationException("Resolved table columns are unavailable.");
        int column = 0;
        float segmentHeight = 0f;
        bool hasRemainder = false;
        bool madeProgress = false;

        for (int cellIndex = 0; cellIndex < source.Cells.Count; cellIndex++)
        {
            TableCell sourceCell = source.Cells[cellIndex];
            TableCell segmentCell = segment.Cells[cellIndex];
            TableCell remainderCell = remainder.Cells[cellIndex];
            int span = Math.Max(1, sourceCell.ColSpan);
            float cellWidth = Sum(widths.Skip(column).Take(span));
            column += span;

            float uniform = sourceCell.Padding ?? _rootTable.CellPadding;
            float left = sourceCell.PaddingLeft ?? uniform;
            float top = sourceCell.PaddingTop ?? uniform;
            float right = sourceCell.PaddingRight ?? uniform;
            float bottom = sourceCell.PaddingBottom ?? uniform;
            float innerHeight = Math.Max(0f, availableHeight - top - bottom);
            IMeasurable? component = sourceCell.ContinuationContent ?? sourceCell.ContentFactory?.Invoke();

            if (component == null)
            {
                if (!string.IsNullOrEmpty(sourceCell.Text) || sourceCell.TextRuns.Count > 0)
                    throw SplitFailure(rowIndex, column - span, "legacy-cell-content", "Only canonical container content can participate in controlled row continuation.");
                ClearCellContent(remainderCell);
                continue;
            }

            var flow = new FlowColumn(0, 0f, Math.Max(0f, cellWidth - left - right), 0f, -innerHeight);
            var measurement = component.Measure(new LayoutMeasureContext(context.Page, flow, context.Options));
            if (measurement.IsWrap)
                throw SplitFailure(rowIndex, column - span, "unsplittable-content", "The cell contains content that cannot split within one usable page. Resize it or keep the row atomic.");
            if (measurement.ReservedHeight > innerHeight + Epsilon)
                throw SplitFailure(rowIndex, column - span, "invalid-measurement", "The cell reported a split segment taller than the offered continuation area.");

            segmentCell.PreparedSplitContent = true;
            segmentCell.MeasuredContent = component;
            segmentCell.MeasuredContentLayout = measurement;
            segmentCell.CachedContentHeight = measurement.ReservedHeight + top + bottom;
            segmentHeight = Math.Max(segmentHeight, segmentCell.CachedContentHeight);
            madeProgress |= measurement.ReservedHeight > Epsilon;

            if (measurement.Remainder != null)
            {
                if (!measurement.IsPartial)
                    throw SplitFailure(rowIndex, column - span, "invalid-remainder", "The cell returned a remainder without a partial measurement result.");
                remainderCell.ContinuationContent = measurement.Remainder;
                remainderCell.PreparedSplitContent = false;
                remainderCell.MeasuredContent = null;
                remainderCell.MeasuredContentLayout = null;
                remainderCell.CachedContentHeight = 0f;
                hasRemainder = true;
            }
            else
            {
                ClearCellContent(remainderCell);
            }
        }

        if (!madeProgress && hasRemainder)
            return new SplitRowResult(segment, remainder, 0f, MadeProgress: false);

        if (!hasRemainder)
            return new SplitRowResult(segment, null, segmentHeight, MadeProgress: true);

        ApplyContinuationEdges(segment, isTopSegment: true, _rootTable.CellPadding);
        ApplyContinuationEdges(remainder, isTopSegment: false, _rootTable.CellPadding);
        segmentHeight = segment.Cells.Max(cell => cell.CachedContentHeight);
        segment.RowHeight = Math.Max(Epsilon, segmentHeight);
        remainder.RowHeight = null;
        return new SplitRowResult(segment, remainder, segmentHeight, MadeProgress: true);
    }

    private static TableRow CloneSingleRow(TableRow row)
    {
        var table = new TableElement();
        table.Rows.Add(row);
        return LayoutSplitUtils.CloneTable(table).Rows.Single();
    }

    private void ValidateOversizedRows(IReadOnlyList<TableRow> rows, IReadOnlyList<float> heights, float maximumHeight, string group)
    {
        for (int index = 0; index < heights.Count; index++)
        {
            if (heights[index] > maximumHeight + Epsilon)
                throw SplitFailure(index, null, $"oversized-{group}", $"Table {group} rows cannot split. Reduce the repeated group content or page reservations.");
        }
    }

    private void ValidateBodySplitPolicies(IReadOnlyList<TableRow> rows, IReadOnlyList<float> heights, float maximumHeight)
    {
        for (int index = 0; index < heights.Count; index++)
        {
            if (heights[index] <= maximumHeight + Epsilon)
                continue;
            int bodyIndex = rows[index].BandIndex ?? _workingTable.RowBandOffset + index;
            if (!IsRowSplittable(rows[index]))
                throw SplitFailure(bodyIndex, null, "row-splitting-disabled", "Enable table.AllowRowSplitting() or row.AllowSplit() for oversized canonical container content.");
            if (rows[index].RowHeight.HasValue)
                throw SplitFailure(bodyIndex, null, "fixed-row-height", "An oversized exact row height cannot be split. Remove Height(...) or reduce it.");
            if (rows[index].Cells.Any(cell => cell.RowSpan > 1))
                throw SplitFailure(bodyIndex, null, "row-span", "Rows containing RowSpan greater than one cannot be split.");
        }
    }

    private bool IsRowSplittable(TableRow row) => row.AllowSplit ?? _rootTable.AllowRowSplitting;

    private PdfTableRowSplitException SplitFailure(int rowIndex, int? columnIndex, string reason, string action)
        => new(rowIndex, columnIndex, reason, $"Table body row {rowIndex} cannot continue ({reason}). {action}");

    private void RecordZeroProgress(LayoutMeasureContext context, int rowIndex)
    {
        _splitProgress.ZeroProgressAttempts++;
        int configured = context.Page.Owner == null
            ? context.Options.Diagnostics.LayoutIterationLimit
            : Math.Min(context.Page.Owner.RenderLimits.MaximumLayoutIterations, context.Options.Diagnostics.LayoutIterationLimit);
        int limit = Math.Max(1, configured);
        if (_splitProgress.ZeroProgressAttempts > limit)
        {
            throw new PdfRenderLimitException(
                nameof(PdfRenderLimits.MaximumLayoutIterations),
                $"Table row {rowIndex} made zero progress during {_splitProgress.ZeroProgressAttempts} continuation attempts, exceeding the configured limit of {limit}.");
        }
    }

    private static bool IsFullPage(LayoutMeasureContext context)
        => context.AvailableHeight + Epsilon >= context.Column.TopY - context.Column.BottomY;

    private static void ApplyContinuationEdges(TableRow row, bool isTopSegment, float tablePadding)
    {
        foreach (TableCell cell in row.Cells)
        {
            MaterializePadding(cell, tablePadding);
            MaterializeCornerRadii(cell);
            if (isTopSegment)
            {
                cell.PaddingBottom = 0f;
                cell.BorderBottom = false;
                cell.CornerRadiusBottomLeft = 0f;
                cell.CornerRadiusBottomRight = 0f;
            }
            else
            {
                cell.PaddingTop = 0f;
                cell.BorderTop = false;
                cell.CornerRadiusTopLeft = 0f;
                cell.CornerRadiusTopRight = 0f;
            }
            if (cell.PreparedSplitContent && cell.MeasuredContentLayout != null)
                cell.CachedContentHeight = cell.MeasuredContentLayout.ReservedHeight + (cell.PaddingTop ?? 0f) + (cell.PaddingBottom ?? 0f);
        }
    }

    private static void MaterializePadding(TableCell cell, float tablePadding)
    {
        float uniform = cell.Padding ?? tablePadding;
        cell.PaddingLeft ??= uniform;
        cell.PaddingTop ??= uniform;
        cell.PaddingRight ??= uniform;
        cell.PaddingBottom ??= uniform;
        cell.Padding = null;
    }

    private static void MaterializeCornerRadii(TableCell cell)
    {
        float uniform = cell.CornerRadius;
        if (cell.CornerRadiusTopLeft <= 0f) cell.CornerRadiusTopLeft = uniform;
        if (cell.CornerRadiusTopRight <= 0f) cell.CornerRadiusTopRight = uniform;
        if (cell.CornerRadiusBottomRight <= 0f) cell.CornerRadiusBottomRight = uniform;
        if (cell.CornerRadiusBottomLeft <= 0f) cell.CornerRadiusBottomLeft = uniform;
        cell.CornerRadius = 0f;
    }

    private static void ClearCellContent(TableCell cell)
    {
        cell.Text = string.Empty;
        cell.TextRuns.Clear();
        cell.ContentBuilder = null;
        cell.ContentFactory = null;
        cell.ContinuationContent = null;
        cell.PreparedSplitContent = false;
        cell.MeasuredContent = null;
        cell.MeasuredContentLayout = null;
        cell.CachedContentHeight = 0f;
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

    private sealed class SplitProgressState
    {
        public int ZeroProgressAttempts { get; set; }
    }

    private sealed record SplitRowResult(TableRow Segment, TableRow? Remainder, float Height, bool MadeProgress);
}
