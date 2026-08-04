using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout;

/// <summary>
/// Validates the rectangular cell grid consumed by the table layout and renderer.
/// A malformed span must fail before it can corrupt a continuation or PDF content stream.
/// </summary>
internal static class TableGridValidator
{
    public static void Validate(TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.Rows.Count == 0)
            return;

        int columnCount = table.Rows.Max(row => row.Cells.Sum(cell => Math.Max(1, cell.ColSpan)));
        if (columnCount == 0)
            throw new InvalidOperationException("A table row must contain at least one cell.");

        var occupied = new HashSet<(int Row, int Column)>();
        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            int columnIndex = 0;
            foreach (var cell in table.Rows[rowIndex].Cells)
            {
                if (cell.ColSpan <= 0)
                    throw new InvalidOperationException("A table cell ColSpan must be greater than zero.");
                if (cell.RowSpan <= 0)
                    throw new InvalidOperationException("A table cell RowSpan must be greater than zero.");

                while (columnIndex < columnCount && occupied.Contains((rowIndex, columnIndex)))
                    columnIndex++;

                if (columnIndex + cell.ColSpan > columnCount)
                    throw new InvalidOperationException("A table cell span exceeds the configured table columns.");
                if (rowIndex + cell.RowSpan > table.Rows.Count)
                    throw new InvalidOperationException("A table cell RowSpan exceeds the available table rows.");

                for (int row = rowIndex; row < rowIndex + cell.RowSpan; row++)
                {
                    for (int column = columnIndex; column < columnIndex + cell.ColSpan; column++)
                    {
                        if (!occupied.Add((row, column)))
                            throw new InvalidOperationException("Table cell spans overlap. Adjust RowSpan or ColSpan so each grid position has one owner.");
                    }
                }

                columnIndex += cell.ColSpan;
            }
        }
    }
}
