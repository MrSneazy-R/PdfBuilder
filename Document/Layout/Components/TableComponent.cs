using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout.Components;

internal sealed class TableComponent : IMeasurable
{
    private const float Epsilon = 0.1f;

    private readonly TableElement _table;
    private readonly TableLayoutPlanState _planState;
    private readonly int _startBodyRow;
    private readonly TableRow? _pendingBodyRow;
    private readonly bool _isContinuation;
    private readonly SplitProgressState _splitProgress;

    internal TableElement SourceTable => _table;
    internal TableLayoutPlan? LayoutPlan => _planState.Plan;
    internal int StartBodyRow => _startBodyRow;
    internal bool HasPendingBodyRow => _pendingBodyRow != null;

    public TableComponent(TableElement table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        TableGridValidator.Validate(_table);
        _planState = new TableLayoutPlanState();
        _startBodyRow = 0;
        _pendingBodyRow = null;
        _isContinuation = false;
        _splitProgress = new SplitProgressState();
    }

    private TableComponent(TableComponent source, int startBodyRow, TableRow? pendingBodyRow)
    {
        _table = source._table;
        _planState = source._planState;
        _startBodyRow = startBodyRow;
        _pendingBodyRow = pendingBodyRow;
        _isContinuation = true;
        _splitProgress = source._splitProgress;
    }

    public LayoutMeasurement Measure(LayoutMeasureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TableLayoutDiagnosticsSession? diagnostics = context.Page.Owner?.TableLayoutDiagnostics;
        if (diagnostics != null && context.Options.Diagnostics.EnableTableLayoutCounters)
            diagnostics.Enabled = true;
        _table.LayoutDiagnostics ??= diagnostics;

        float requestedWidth = ResolveWidth(context.AvailableWidth);
        TableLayoutPlan plan = GetOrCreatePlan(requestedWidth, context);
        float width = plan.Width;
        TableRowLayout? pendingBodyLayout = _pendingBodyRow == null
            ? null
            : plan.MeasureStandaloneRow(_table, _pendingBodyRow, context.Page, context.Options);
        var body = new BodyRowLayoutView(plan.BodyRows, _startBodyRow, pendingBodyLayout);
        bool showHeader = plan.HeaderRows.Length > 0 && (!_isContinuation || _table.RepeatHeaders);
        IReadOnlyList<TableRowLayout> segmentHeaders = showHeader ? plan.HeaderRows : Array.Empty<TableRowLayout>();
        float captionHeight = _isContinuation ? 0f : ComputeCaptionHeight(_table);

        float headerHeight = showHeader ? plan.HeaderHeight : 0f;
        float footerHeight = plan.FooterHeight;
        float bodyHeight = plan.GetRemainingBodyHeight(_startBodyRow) + (pendingBodyLayout?.Height ?? 0f);
        float completeHeight = captionHeight + headerHeight + bodyHeight + footerHeight;
        float pageContentHeight = context.Column.TopY - context.Column.BottomY;
        float maximumBodyHeight = Math.Max(0f, pageContentHeight - captionHeight - headerHeight - footerHeight);
        ValidatePlannedRowsOnce(plan, pageContentHeight, maximumBodyHeight);
        if (pendingBodyLayout != null)
            ValidateBodySplitPolicies([pendingBodyLayout], maximumBodyHeight);

        if (completeHeight <= context.AvailableHeight + Epsilon)
            return FullMeasurement(width, completeHeight, BuildSegment(plan, segmentHeaders, body, plan.FooterRows, includeCaption: captionHeight > 0f));

        if (!_table.EnablePageBreaks || _table.AvoidBreakInside || context.AvailableHeight <= Epsilon)
            return LayoutMeasurement.Wrap(plan.Width);

        float reserved = captionHeight + headerHeight + footerHeight;
        if (reserved > context.AvailableHeight + Epsilon)
            return WrapOrThrow(context, width, "Table header/footer groups leave no usable space for a body row.");

        int rowsTaken = FindRowsToTake(plan, body, context.AvailableHeight - reserved);
        if (rowsTaken <= 0)
        {
            if (body.Count > 0 && body[0].Height > maximumBodyHeight + Epsilon && IsRowSplittable(body[0].Row))
            {
                if (!IsFullPage(context))
                    return LayoutMeasurement.Wrap(width);
                return SplitFirstBodyRow(
                    context,
                    plan,
                    segmentHeaders,
                    plan.FooterRows,
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
            return FullMeasurement(width, completeHeight, BuildSegment(plan, segmentHeaders, body, plan.FooterRows, includeCaption: captionHeight > 0f));

        bool repeatFooter = _table.FooterRepeatMode == TableFooterRepeatMode.EveryPage
            || (_table.FooterRepeatMode == TableFooterRepeatMode.ContinuationPages && _isContinuation);
        IReadOnlyList<TableRowLayout> segmentFooter = repeatFooter ? plan.FooterRows : Array.Empty<TableRowLayout>();
        TableRowLayout[] segmentBody = body.Take(rowsTaken).ToArray();
        float segmentHeight = captionHeight + headerHeight + SumHeights(segmentBody) + (repeatFooter ? footerHeight : 0f);
        TableSegmentElement segment = BuildSegment(plan, segmentHeaders, segmentBody, segmentFooter, includeCaption: captionHeight > 0f);
        int nextBodyRow = _startBodyRow + rowsTaken - (_pendingBodyRow == null ? 0 : 1);
        TableComponent remainder = CreateRemainder(nextBodyRow, pendingBodyRow: null);

        return PartialMeasurement(width, segmentHeight, segment, remainder);
    }

    public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(measurement);
        if (measurement.Metadata is not TableSegmentElement segmentElement)
            throw new InvalidOperationException("Table measurement metadata missing.");

        segmentElement.X = context.ContentLeft;
        segmentElement.Y = context.ContentTop;
        context.Page.AddElement(segmentElement);
        TableCellContainerLayout.AddContentGroups(segmentElement, context.Page, context.Options);
    }

    private LayoutMeasurement FullMeasurement(float width, float height, TableSegmentElement segment)
        => new(
            marginTop: 0f,
            contentHeight: height,
            marginBottom: 0f,
            usedWidth: width,
            metadata: segment,
            avoidBreakInside: _table.AvoidBreakInside,
            result: LayoutResultKind.Full,
            remainder: null);

    private LayoutMeasurement PartialMeasurement(float width, float height, TableSegmentElement segment, TableComponent remainder)
        => new(
            marginTop: 0f,
            contentHeight: height,
            marginBottom: 0f,
            usedWidth: width,
            metadata: segment,
            avoidBreakInside: _table.AvoidBreakInside,
            result: LayoutResultKind.Partial,
            remainder: remainder);

    private LayoutMeasurement WrapOrThrow(LayoutMeasureContext context, float width, string message)
    {
        float fullPageHeight = context.Column.TopY - context.Column.BottomY;
        if (context.AvailableHeight + Epsilon < fullPageHeight)
            return LayoutMeasurement.Wrap(width);
        throw new InvalidOperationException(message + " Reduce the configured row constraints or repeated table groups.");
    }

    private int FindRowsToTake(TableLayoutPlan plan, IReadOnlyList<TableRowLayout> rows, float budget)
    {
        int count = 0;
        float used = 0f;
        while (count < rows.Count && used + rows[count].Height <= budget + Epsilon)
        {
            used += rows[count].Height;
            count++;
        }
        if (count == 0 || count >= rows.Count)
            return count;

        while (count > 0 && IsBreakBlocked(plan, rows, count - 1)) count--;
        if (count == 0)
            return 0;

        int minimumAtEnd = Math.Min(_table.MinRowsAtPageEnd, rows.Count);
        if (count < minimumAtEnd)
            return 0;

        int remaining = rows.Count - count;
        int minimumAtStart = Math.Min(_table.MinRowsAtPageStart, rows.Count);
        if (remaining > 0 && remaining < minimumAtStart)
        {
            count -= minimumAtStart - remaining;
            while (count > 0 && IsBreakBlocked(plan, rows, count - 1)) count--;
        }

        return Math.Max(0, count);
    }

    private static bool IsBreakBlocked(
        TableLayoutPlan plan,
        IReadOnlyList<TableRowLayout> rows,
        int breakAfterIndex)
    {
        TableRowLayout row = rows[breakAfterIndex];
        if (row.Row.KeepWithNext && breakAfterIndex < rows.Count - 1)
            return true;
        return row.BodyIndex >= 0
            && row.BodyIndex < plan.BlockedBreaks.Length
            && plan.BlockedBreaks[row.BodyIndex];
    }

    private TableLayoutPlan GetOrCreatePlan(float width, LayoutMeasureContext context)
    {
        _planState.Plan ??= TableLayoutPlan.Create(_table, width, context.Page, context.Options);
        return _planState.Plan;
    }

    private void ValidatePlannedRowsOnce(TableLayoutPlan plan, float pageContentHeight, float maximumBodyHeight)
    {
        if (_planState.RowsValidated)
            return;

        ValidateOversizedRows(plan.HeaderRows, pageContentHeight, "header");
        ValidateOversizedRows(plan.FooterRows, pageContentHeight, "footer");
        ValidateBodySplitPolicies(plan.BodyRows, maximumBodyHeight);
        _planState.RowsValidated = true;
    }

    private TableSegmentElement BuildSegment(
        TableLayoutPlan plan,
        IReadOnlyList<TableRowLayout> headers,
        IReadOnlyList<TableRowLayout> body,
        IReadOnlyList<TableRowLayout> footers,
        bool includeCaption)
    {
        var layouts = new List<TableRowLayout>(headers.Count + body.Count + footers.Count);
        layouts.AddRange(headers);
        layouts.AddRange(body);
        layouts.AddRange(footers);
        TableRowLayout[] segmentRows = layouts.ToArray();
        int startBodyRow = body.Count == 0
            ? _startBodyRow
            : body[0].BodyIndex >= 0 ? body[0].BodyIndex : _startBodyRow;
        var segment = new TableSegment(
            startBodyRow,
            body.Count,
            headers.Count > 0,
            footers.Count > 0,
            includeCaption);

        return new TableSegmentElement(
            _table,
            segment,
            plan.Width,
            plan.ColumnWidths,
            segmentRows,
            segmentRows.Select(layout => layout.Height).ToArray());
    }

    private TableComponent CreateRemainder(int nextBodyRow, TableRow? pendingBodyRow)
        => new(this, nextBodyRow, pendingBodyRow);

    private LayoutMeasurement SplitFirstBodyRow(
        LayoutMeasureContext context,
        TableLayoutPlan plan,
        IReadOnlyList<TableRowLayout> headers,
        IReadOnlyList<TableRowLayout> footers,
        IReadOnlyList<TableRowLayout> body,
        float availableBodyHeight,
        float captionHeight,
        float headerHeight,
        float footerHeight)
    {
        int rowIndex = ResolveBodyRowIndex(body[0].Row, localIndex: 0);
        SplitRowResult split = SplitRow(body[0].Row, rowIndex, availableBodyHeight, context);
        if (!split.MadeProgress)
        {
            RecordZeroProgress(context, rowIndex);
            return LayoutMeasurement.Wrap(plan.Width);
        }

        _splitProgress.ZeroProgressAttempts = 0;
        int nextBodyRow = _startBodyRow + (_pendingBodyRow == null ? 1 : 0);
        TableRow? pendingBodyRow = split.Remainder;
        split.Segment.BandIndex ??= rowIndex;
        if (pendingBodyRow != null)
            pendingBodyRow.BandIndex ??= rowIndex;
        bool hasRemainder = pendingBodyRow != null || nextBodyRow < plan.BodyRows.Length;
        bool repeatFooter = !hasRemainder
            || _table.FooterRepeatMode == TableFooterRepeatMode.EveryPage
            || (_table.FooterRepeatMode == TableFooterRepeatMode.ContinuationPages && _isContinuation);
        IReadOnlyList<TableRowLayout> segmentFooter = repeatFooter ? footers : Array.Empty<TableRowLayout>();
        float segmentHeight = captionHeight + headerHeight + split.Height + (repeatFooter ? footerHeight : 0f);
        TableRowLayout segmentRow = plan.CreatePreparedRowLayout(split.Segment, split.Height);
        TableSegmentElement segment = BuildSegment(plan, headers, [segmentRow], segmentFooter, includeCaption: captionHeight > 0f);

        if (!hasRemainder)
            return FullMeasurement(plan.Width, segmentHeight, segment);

        TableComponent remainder = CreateRemainder(nextBodyRow, pendingBodyRow);
        return PartialMeasurement(plan.Width, segmentHeight, segment, remainder);
    }

    private SplitRowResult SplitRow(TableRow source, int rowIndex, float availableHeight, LayoutMeasureContext context)
    {
        if (source.RowHeight.HasValue)
            throw SplitFailure(rowIndex, null, "fixed-row-height", "Remove the exact row height before enabling row continuation.");
        if (source.Cells.Any(cell => cell.RowSpan > 1))
            throw SplitFailure(rowIndex, null, "row-span", "Rows containing RowSpan greater than one cannot be split. Restructure the span or keep the row atomic.");

        TableRow segment = CloneSingleRow(source);
        TableRow remainder = CloneSingleRow(source);
        float[] widths = _table.ResolvedColumnWidths ?? throw new InvalidOperationException("Resolved table columns are unavailable.");
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

            float uniform = sourceCell.Padding ?? _table.CellPadding;
            float left = sourceCell.PaddingLeft ?? uniform;
            float top = sourceCell.PaddingTop ?? uniform;
            float right = sourceCell.PaddingRight ?? uniform;
            float bottom = sourceCell.PaddingBottom ?? uniform;
            float innerHeight = Math.Max(0f, availableHeight - top - bottom);
            IMeasurable? component = sourceCell.ContinuationContent ?? sourceCell.MeasuredContent;
            if (component == null && sourceCell.ContentFactory != null)
            {
                _table.LayoutDiagnostics?.RecordContentFactoryInvocation();
                component = sourceCell.ContentFactory();
            }

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

        ApplyContinuationEdges(segment, isTopSegment: true, _table.CellPadding);
        ApplyContinuationEdges(remainder, isTopSegment: false, _table.CellPadding);
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

    private void ValidateOversizedRows(IReadOnlyList<TableRowLayout> rows, float maximumHeight, string group)
    {
        for (int index = 0; index < rows.Count; index++)
        {
            if (rows[index].Height > maximumHeight + Epsilon)
                throw SplitFailure(index, null, $"oversized-{group}", $"Table {group} rows cannot split. Reduce the repeated group content or page reservations.");
        }
    }

    private void ValidateBodySplitPolicies(IReadOnlyList<TableRowLayout> rows, float maximumHeight)
    {
        for (int index = 0; index < rows.Count; index++)
        {
            TableRow row = rows[index].Row;
            if (rows[index].Height <= maximumHeight + Epsilon)
                continue;
            int bodyIndex = ResolveBodyRowIndex(row, index);
            if (!IsRowSplittable(row))
                throw SplitFailure(bodyIndex, null, "row-splitting-disabled", "Enable table.AllowRowSplitting() or row.AllowSplit() for oversized canonical container content.");
            if (row.RowHeight.HasValue)
                throw SplitFailure(bodyIndex, null, "fixed-row-height", "An oversized exact row height cannot be split. Remove Height(...) or reduce it.");
            if (row.Cells.Any(cell => cell.RowSpan > 1))
                throw SplitFailure(bodyIndex, null, "row-span", "Rows containing RowSpan greater than one cannot be split.");
        }
    }

    private int ResolveBodyRowIndex(TableRow row, int localIndex)
    {
        if (row.BandIndex.HasValue)
            return row.BandIndex.Value;

        int pendingOffset = _pendingBodyRow == null ? 0 : 1;
        int originalIndex = localIndex < pendingOffset
            ? _startBodyRow - 1
            : _startBodyRow + localIndex - pendingOffset;
        return _table.RowBandOffset + Math.Max(0, originalIndex);
    }

    private bool IsRowSplittable(TableRow row) => row.AllowSplit ?? _table.AllowRowSplitting;

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

    private float ResolveWidth(float availableWidth)
        => _table.TableWidth is > 0f ? Math.Min(availableWidth, _table.TableWidth.Value) : availableWidth;

    private static float ComputeCaptionHeight(TableElement table)
        => string.IsNullOrWhiteSpace(table.CaptionText)
            ? 0f
            : Math.Max(table.DefaultFontSize, 11f) * Writer.PdfDefaults.LineHeightMultiplier + 4f;

    private static float Sum(IEnumerable<float> values) => values.Sum();

    private static float SumHeights(IEnumerable<TableRowLayout> rows)
        => rows.Sum(row => row.Height);

    private sealed class TableLayoutPlanState
    {
        public TableLayoutPlan? Plan { get; set; }
        public bool RowsValidated { get; set; }
    }

    private sealed class SplitProgressState
    {
        public int ZeroProgressAttempts { get; set; }
    }

    private sealed class BodyRowLayoutView : IReadOnlyList<TableRowLayout>
    {
        private readonly IReadOnlyList<TableRowLayout> _rows;
        private readonly int _startIndex;
        private readonly TableRowLayout? _pendingRow;

        public BodyRowLayoutView(IReadOnlyList<TableRowLayout> rows, int startIndex, TableRowLayout? pendingRow)
        {
            _rows = rows;
            _startIndex = startIndex;
            _pendingRow = pendingRow;
        }

        public int Count => (_pendingRow == null ? 0 : 1) + Math.Max(0, _rows.Count - _startIndex);

        public TableRowLayout this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                if (_pendingRow != null)
                {
                    if (index == 0)
                        return _pendingRow;
                    index--;
                }
                return _rows[_startIndex + index];
            }
        }

        public IEnumerator<TableRowLayout> GetEnumerator()
        {
            if (_pendingRow != null)
                yield return _pendingRow;
            for (int index = _startIndex; index < _rows.Count; index++)
                yield return _rows[index];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed record SplitRowResult(TableRow Segment, TableRow? Remainder, float Height, bool MadeProgress);
}
