using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;

namespace PdfBuilder.Document.Layout;

internal static class TableCellContainerLayout
{
    internal static void AddContentGroups(
        TableSegmentElement segmentElement,
        PdfPage page,
        LayoutOptions options)
    {
        TableElement table = segmentElement.SourceTable;
        if (!segmentElement.Rows.SelectMany(row => row.Cells).Any(cell => cell.Content != null && cell.Measurement != null))
            return;

        var drawBuffer = new CellDrawBuffer(page, options);
        int totalColumns = segmentElement.ColumnWidths.Length;
        float rowTop = segmentElement.Y - (segmentElement.Segment.IncludeCaption ? CaptionHeight(table) : 0f);
        var covered = new HashSet<(int Row, int Column)>();

        for (int rowIndex = 0; rowIndex < segmentElement.Rows.Length; rowIndex++)
        {
            TableRowLayout rowLayout = segmentElement.Rows[rowIndex];
            TableRow row = rowLayout.Row;
            IDisposable? semanticScope = null;
            if (row.SemanticDescriptor != null && page.Owner?.Tagging.Enabled == true)
            {
                PdfSemanticNode node = page.Owner.SemanticRegistry.GetOrCreate(row.SemanticDescriptor);
                semanticScope = page.Owner.SemanticRegistry.Enter(node.Id);
            }
            try
            {
                float cellX = segmentElement.X;
                int columnIndex = 0;
                while (columnIndex < totalColumns && covered.Contains((rowIndex, columnIndex)))
                {
                    cellX += segmentElement.ColumnWidths[columnIndex];
                    columnIndex++;
                }

                for (int cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                {
                    TableCell cell = row.Cells[cellIndex];
                    TableCellLayout cellLayout = rowLayout.Cells[cellIndex];
                    while (columnIndex < totalColumns && covered.Contains((rowIndex, columnIndex)))
                    {
                        cellX += segmentElement.ColumnWidths[columnIndex];
                        columnIndex++;
                    }

                    int columnSpan = Math.Max(1, cell.ColSpan);
                    int rowSpan = Math.Max(1, cell.RowSpan);
                    float cellWidth = Sum(segmentElement.ColumnWidths, columnIndex, columnSpan);
                    float cellHeight = Sum(segmentElement.RowHeights, rowIndex, rowSpan);

                    if (cellLayout.Content != null && cellLayout.Measurement != null)
                        AddContentGroup(page, drawBuffer, table, cell, cellLayout, cellX, rowTop, cellWidth, cellHeight);

                    if (rowSpan > 1 || columnSpan > 1)
                    {
                        for (int rowOffset = 0; rowOffset < rowSpan; rowOffset++)
                            for (int columnOffset = 0; columnOffset < columnSpan; columnOffset++)
                            {
                                if (rowOffset != 0 || columnOffset != 0)
                                    covered.Add((rowIndex + rowOffset, columnIndex + columnOffset));
                            }
                    }

                    cellX += cellWidth;
                    columnIndex += columnSpan;
                }

                rowTop -= segmentElement.RowHeights[rowIndex];
            }
            finally
            {
                semanticScope?.Dispose();
            }
        }
    }

    private static void AddContentGroup(
        PdfPage page,
        CellDrawBuffer drawBuffer,
        TableElement table,
        TableCell cell,
        TableCellLayout cellLayout,
        float cellX,
        float cellTop,
        float cellWidth,
        float cellHeight)
    {
        float uniform = cell.Padding ?? table.CellPadding;
        float leftPadding = cell.PaddingLeft ?? uniform;
        float rightPadding = cell.PaddingRight ?? uniform;
        float topPadding = cell.PaddingTop ?? uniform;
        float bottomPadding = cell.PaddingBottom ?? uniform;
        float contentWidth = Math.Max(0f, cellWidth - leftPadding - rightPadding);
        float contentHeight = Math.Max(0f, cellHeight - topPadding - bottomPadding);
        LayoutMeasurement measurement = cellLayout.Measurement!;

        float contentLeft = cellX + leftPadding;
        if (cell.HorizontalAlign == HorizontalAlign.Center)
            contentLeft += Math.Max(0f, contentWidth - measurement.UsedWidth) * 0.5f;
        else if (cell.HorizontalAlign == HorizontalAlign.Right)
            contentLeft += Math.Max(0f, contentWidth - measurement.UsedWidth);

        float contentTop = cellTop - topPadding;
        if (cell.VerticalAlign == VerticalAlign.Middle)
            contentTop -= Math.Max(0f, contentHeight - measurement.ReservedHeight) * 0.5f;
        else if (cell.VerticalAlign == VerticalAlign.Bottom)
            contentTop -= Math.Max(0f, contentHeight - measurement.ReservedHeight);

        PdfElement[] children = drawBuffer.Draw(
            cellLayout.Content!,
            measurement,
            contentLeft,
            contentTop,
            contentWidth,
            contentHeight);

        if (children.Length > 0)
        {
            page.AddElement(new ClipGroupElement(
                cellX,
                cellTop - cellHeight,
                cellWidth,
                cellHeight,
                children));
        }
    }

    private static float Sum(float[] values, int start, int count)
    {
        float result = 0f;
        int end = Math.Min(values.Length, start + count);
        for (int index = start; index < end; index++) result += values[index];
        return result;
    }

    private static float CaptionHeight(TableElement table)
        => string.IsNullOrWhiteSpace(table.CaptionText)
            ? 0f
            : Math.Max(table.DefaultFontSize, 11f) * PdfDefaults.LineHeightMultiplier + 4f;
}
