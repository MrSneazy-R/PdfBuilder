using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;

namespace PdfBuilder.Document.Layout;

internal static class TableCellContainerLayout
{
    internal static void AddContentGroups(TableElement table, PdfPage page, LayoutOptions options)
    {
        if (!table.Rows.SelectMany(row => row.Cells).Any(cell => cell.HasContainerContent))
            return;

        float width = table.TableWidth ?? page.Width;
        TableMeasurementHelper.TableMetrics metrics = TableMeasurementHelper.Measure(table, width, page, options);
        float[] columnWidths = metrics.ColumnWidths;
        float[] rowHeights = metrics.RowHeights;
        int totalColumns = columnWidths.Length;
        float rowTop = table.Y - CaptionHeight(table);
        var covered = new HashSet<(int Row, int Column)>();

        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            TableRow row = table.Rows[rowIndex];
            IDisposable? semanticScope = null;
            if (row.SemanticDescriptor != null && page.Owner?.Tagging.Enabled == true)
            {
                PdfSemanticNode node = page.Owner.SemanticRegistry.GetOrCreate(row.SemanticDescriptor);
                semanticScope = page.Owner.SemanticRegistry.Enter(node.Id);
            }
            try
            {
                float cellX = table.X;
                int columnIndex = 0;
                while (columnIndex < totalColumns && covered.Contains((rowIndex, columnIndex)))
                {
                    cellX += columnWidths[columnIndex];
                    columnIndex++;
                }

                foreach (TableCell cell in row.Cells)
                {
                    while (columnIndex < totalColumns && covered.Contains((rowIndex, columnIndex)))
                    {
                        cellX += columnWidths[columnIndex];
                        columnIndex++;
                    }

                    int columnSpan = Math.Max(1, cell.ColSpan);
                    int rowSpan = Math.Max(1, cell.RowSpan);
                    float cellWidth = Sum(columnWidths, columnIndex, columnSpan);
                    float cellHeight = Sum(rowHeights, rowIndex, rowSpan);

                    if (cell.MeasuredContent != null && cell.MeasuredContentLayout != null)
                        AddContentGroup(page, options, table, cell, cellX, rowTop, cellWidth, cellHeight);

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

                rowTop -= rowHeights[rowIndex];
            }
            finally
            {
                semanticScope?.Dispose();
            }
        }
    }

    private static void AddContentGroup(
        PdfPage page,
        LayoutOptions options,
        TableElement table,
        TableCell cell,
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
        LayoutMeasurement measurement = cell.MeasuredContentLayout!;

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

        var temporaryPage = new PdfPage(page.Width, page.Height)
        {
            Owner = page.Owner,
            Pagination = page.Pagination,
            ProfilerSession = page.ProfilerSession,
            CompositionPageNumber = page.CompositionPageNumber,
            LayoutOptions = options,
            TextDefaults = page.TextDefaults.Clone(),
            Theme = page.Theme.Clone()
        };
        var column = new FlowColumn(0, contentLeft, contentWidth, contentTop, contentTop - contentHeight);
        var drawContext = new LayoutDrawContext(temporaryPage, column, contentLeft, contentTop, contentWidth, options);
        cell.MeasuredContent!.Draw(drawContext, measurement);
        NormalizeTextBaselines(temporaryPage.Elements);

        if (temporaryPage.Elements.Count > 0)
        {
            page.AddElement(new ClipGroupElement(
                cellX,
                cellTop - cellHeight,
                cellWidth,
                cellHeight,
                temporaryPage.Elements.ToArray()));
        }
    }

    private static void NormalizeTextBaselines(IEnumerable<PdfElement> elements)
    {
        foreach (PdfElement element in elements)
        {
            switch (element)
            {
                case TextElement text when text.ShapedLayout is { Lines.Count: > 0 } layout:
                    int textLine = Math.Clamp(text.ShapedStartLine, 0, layout.Lines.Count - 1);
                    text.Y -= layout.Lines[textLine].Ascent;
                    break;
                case RichTextElement richText when richText.ShapedLayout is { Lines.Count: > 0 } layout:
                    int richTextLine = Math.Clamp(richText.ShapedStartLine, 0, layout.Lines.Count - 1);
                    richText.Y -= layout.Lines[richTextLine].Ascent;
                    break;
                case ClipGroupElement clipGroup:
                    NormalizeTextBaselines(clipGroup.Children);
                    break;
            }
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
