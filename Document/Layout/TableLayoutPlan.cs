using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout;

internal sealed class TableLayoutPlan
{
    public required float Width { get; init; }
    public required float[] ColumnWidths { get; init; }
    public required TableRowLayout[] HeaderRows { get; init; }
    public required TableRowLayout[] BodyRows { get; init; }
    public required TableRowLayout[] FooterRows { get; init; }
    public required bool[] BlockedBreaks { get; init; }
    public required float HeaderHeight { get; init; }
    public required float FooterHeight { get; init; }
    private float[] BodyHeightSuffix { get; init; } = [];

    public static TableLayoutPlan Create(
        TableElement table,
        float width,
        PdfPage page,
        LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);

        TableMeasurementHelper.TableMetrics metrics = TableMeasurementHelper.Measure(table, width, page, options);
        table.ResolvedColumnWidths = metrics.ColumnWidths.ToArray();

        TableRowLayout[] allRows = CreateRowLayouts(table.Rows, metrics.RowHeights, metrics.ColumnWidths);
        TableRowLayout[] headerRows = allRows.Where(row => row.Row.IsHeader).ToArray();
        TableRowLayout[] bodyRows = allRows.Where(row => !row.Row.IsHeader && !row.Row.IsFooter).ToArray();
        TableRowLayout[] footerRows = allRows.Where(row => row.Row.IsFooter).ToArray();
        for (int index = 0; index < bodyRows.Length; index++)
            bodyRows[index].BodyIndex = index;

        return new TableLayoutPlan
        {
            Width = width,
            ColumnWidths = metrics.ColumnWidths.ToArray(),
            HeaderRows = headerRows,
            BodyRows = bodyRows,
            FooterRows = footerRows,
            BlockedBreaks = ComputeBlockedBreaks(bodyRows),
            HeaderHeight = SumHeights(headerRows),
            FooterHeight = SumHeights(footerRows),
            BodyHeightSuffix = ComputeHeightSuffix(bodyRows)
        };
    }

    public float GetRemainingBodyHeight(int startBodyRow)
    {
        int index = Math.Clamp(startBodyRow, 0, BodyRows.Length);
        return BodyHeightSuffix[index];
    }

    public TableRowLayout MeasureStandaloneRow(
        TableElement table,
        TableRow row,
        PdfPage page,
        LayoutOptions options)
    {
        var measurementTable = LayoutSplitUtils.CloneTableStructure(table);
        measurementTable.CaptionText = null;
        measurementTable.TableWidth = Width;
        measurementTable.ResolvedColumnWidths = ColumnWidths.ToArray();
        measurementTable.Rows.Add(row);
        TableMeasurementHelper.TableMetrics metrics = TableMeasurementHelper.Measure(measurementTable, Width, page, options);
        return CreateRowLayouts(measurementTable.Rows, metrics.RowHeights, ColumnWidths).Single();
    }

    public TableRowLayout CreatePreparedRowLayout(TableRow row, float height)
        => CreateRowLayouts([row], [height], ColumnWidths).Single();

    private static TableRowLayout[] CreateRowLayouts(
        IReadOnlyList<TableRow> rows,
        IReadOnlyList<float> rowHeights,
        float[] columnWidths)
    {
        var layouts = new TableRowLayout[rows.Count];
        var covered = new HashSet<(int Row, int Column)>();

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            TableRow row = rows[rowIndex];
            var cellLayouts = new TableCellLayout[row.Cells.Count];
            int columnIndex = 0;
            while (columnIndex < columnWidths.Length && covered.Contains((rowIndex, columnIndex)))
                columnIndex++;

            for (int cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                while (columnIndex < columnWidths.Length && covered.Contains((rowIndex, columnIndex)))
                    columnIndex++;

                TableCell cell = row.Cells[cellIndex];
                int columnSpan = Math.Min(Math.Max(1, cell.ColSpan), columnWidths.Length - columnIndex);
                int rowSpan = Math.Max(1, cell.RowSpan);
                float cellWidth = 0f;
                for (int offset = 0; offset < columnSpan; offset++)
                    cellWidth += columnWidths[columnIndex + offset];

                cellLayouts[cellIndex] = new TableCellLayout
                {
                    Width = cellWidth,
                    ContentHeight = cell.CachedContentHeight,
                    Content = cell.MeasuredContent,
                    Measurement = cell.MeasuredContentLayout
                };

                for (int rowOffset = 0; rowOffset < rowSpan; rowOffset++)
                {
                    for (int columnOffset = 0; columnOffset < columnSpan; columnOffset++)
                    {
                        if (rowOffset != 0 || columnOffset != 0)
                            covered.Add((rowIndex + rowOffset, columnIndex + columnOffset));
                    }
                }

                columnIndex += columnSpan;
            }

            layouts[rowIndex] = new TableRowLayout
            {
                Row = row,
                Height = rowHeights[rowIndex],
                Cells = cellLayouts
            };
        }

        return layouts;
    }

    private static bool[] ComputeBlockedBreaks(IReadOnlyList<TableRowLayout> rows)
    {
        var blocked = new bool[rows.Count];
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            TableRow row = rows[rowIndex].Row;
            if (row.KeepWithNext && rowIndex < rows.Count - 1)
                blocked[rowIndex] = true;

            foreach (TableCell cell in row.Cells)
            {
                int finalCoveredRow = Math.Min(rows.Count - 1, rowIndex + Math.Max(1, cell.RowSpan) - 1);
                for (int breakAfter = rowIndex; breakAfter < finalCoveredRow; breakAfter++)
                    blocked[breakAfter] = true;
            }
        }
        return blocked;
    }

    private static float SumHeights(IEnumerable<TableRowLayout> rows)
        => rows.Sum(row => row.Height);

    private static float[] ComputeHeightSuffix(IReadOnlyList<TableRowLayout> rows)
    {
        var suffix = new float[rows.Count + 1];
        for (int index = rows.Count - 1; index >= 0; index--)
            suffix[index] = suffix[index + 1] + rows[index].Height;
        return suffix;
    }
}

internal sealed class TableRowLayout
{
    public required TableRow Row { get; init; }
    public required float Height { get; init; }
    public required TableCellLayout[] Cells { get; init; }
    public int BodyIndex { get; set; } = -1;
}

internal sealed class TableCellLayout
{
    public required float Width { get; init; }
    public required float ContentHeight { get; init; }
    public required IMeasurable? Content { get; init; }
    public required LayoutMeasurement? Measurement { get; init; }
}

internal sealed record TableSegment(
    int StartBodyRow,
    int BodyRowCount,
    bool IncludeHeader,
    bool IncludeFooter,
    bool IncludeCaption);

internal sealed class TableSegmentElement : PdfElement
{
    public TableSegmentElement(
        TableElement sourceTable,
        TableSegment segment,
        float width,
        float[] columnWidths,
        TableRowLayout[] rows,
        float[] rowHeights)
        : base(0, 0)
    {
        SourceTable = sourceTable;
        Segment = segment;
        Width = width;
        ColumnWidths = columnWidths;
        Rows = rows;
        RowHeights = rowHeights;
        RenderRows = new TableSegmentRowView(rows);
    }

    public TableElement SourceTable { get; }
    public TableSegment Segment { get; }
    public float Width { get; }
    public float[] ColumnWidths { get; }
    public TableRowLayout[] Rows { get; }
    public float[] RowHeights { get; }
    public IReadOnlyList<TableRow> RenderRows { get; }

    private sealed class TableSegmentRowView(IReadOnlyList<TableRowLayout> rows) : IReadOnlyList<TableRow>
    {
        public int Count => rows.Count;
        public TableRow this[int index] => rows[index].Row;
        public IEnumerator<TableRow> GetEnumerator()
        {
            for (int index = 0; index < rows.Count; index++)
                yield return rows[index].Row;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
