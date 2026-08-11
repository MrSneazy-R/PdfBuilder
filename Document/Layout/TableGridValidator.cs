using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout;

/// <summary>
/// Normalizes explicit canonical placement and validates the rectangular grid consumed
/// by measurement, pagination, and rendering. This is the single span/overlap validator.
/// </summary>
internal static class TableGridValidator
{
    public static void Validate(TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        NormalizeRowGroups(table);

        if (table.Rows.Count == 0)
            return;

        int columnCount = ResolveColumnCount(table);
        if (columnCount == 0)
            throw new InvalidOperationException("A table row must contain at least one cell.");

        var occupied = new HashSet<(int Row, int Column)>();
        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            TableRow row = table.Rows[rowIndex];
            var placed = new List<(int Column, TableCell Cell)>();
            int cursor = 0;

            foreach (TableCell cell in row.Cells)
            {
                if (cell.ColSpan <= 0)
                    throw new InvalidOperationException("A table cell ColumnSpan must be greater than zero.");
                if (cell.RowSpan <= 0)
                    throw new InvalidOperationException("A table cell RowSpan must be greater than zero.");

                int column = cell.ExplicitColumnIndex ?? FindNextAvailableColumn(occupied, rowIndex, cursor, cell.ColSpan, columnCount);
                if (column < 0)
                    throw new InvalidOperationException("A table cell column position cannot be negative.");
                if (column + cell.ColSpan > columnCount)
                    throw new InvalidOperationException($"Table cell at row {rowIndex}, column {column} spans beyond the configured {columnCount} columns.");
                if (rowIndex + cell.RowSpan > table.Rows.Count)
                    throw new InvalidOperationException($"Table cell at row {rowIndex}, column {column} has a RowSpan beyond the available table rows.");

                TableRowGroup group = GetGroup(row);
                for (int spannedRow = rowIndex; spannedRow < rowIndex + cell.RowSpan; spannedRow++)
                {
                    if (GetGroup(table.Rows[spannedRow]) != group)
                        throw new InvalidOperationException("A table cell RowSpan cannot cross header, body, or footer group boundaries.");

                    for (int spannedColumn = column; spannedColumn < column + cell.ColSpan; spannedColumn++)
                    {
                        if (!occupied.Add((spannedRow, spannedColumn)))
                            throw new InvalidOperationException($"Table cell spans overlap at row {spannedRow}, column {spannedColumn}. Adjust Position, RowSpan, or ColumnSpan so each grid position has one owner.");
                    }
                }

                placed.Add((column, cell));
                cursor = column + cell.ColSpan;
            }

            for (int column = 0; column < columnCount; column++)
            {
                if (!occupied.Contains((rowIndex, column)))
                {
                    var placeholder = new TableCell { ExplicitColumnIndex = column };
                    occupied.Add((rowIndex, column));
                    placed.Add((column, placeholder));
                }
            }

            row.Cells.Clear();
            foreach (var placement in placed.OrderBy(item => item.Column))
            {
                placement.Cell.ExplicitColumnIndex = null;
                row.Cells.Add(placement.Cell);
            }
        }
    }

    private static void NormalizeRowGroups(TableElement table)
    {
        if (table.HeaderRowCount is > 0 && !table.Rows.Any(row => row.IsHeader))
        {
            foreach (TableRow row in table.Rows.Take(Math.Min(table.HeaderRowCount.Value, table.Rows.Count)))
                row.IsHeader = true;
        }

        var headers = NormalizeGroup(table.Rows.Where(row => row.IsHeader), TableRowGroup.Header);
        var bodies = NormalizeGroup(table.Rows.Where(row => !row.IsHeader && !row.IsFooter), TableRowGroup.Body);
        var footers = NormalizeGroup(table.Rows.Where(row => row.IsFooter), TableRowGroup.Footer);

        table.Rows.Clear();
        table.Rows.AddRange(headers);
        table.Rows.AddRange(bodies);
        table.Rows.AddRange(footers);

        for (int index = 0; index < bodies.Count; index++)
            bodies[index].BandIndex ??= index;
    }

    private static List<TableRow> NormalizeGroup(IEnumerable<TableRow> source, TableRowGroup group)
    {
        var rows = source.ToList();
        if (rows.Count == 0)
            return rows;

        var positions = new SortedDictionary<int, TableRow>();
        int nextImplicit = 0;
        foreach (TableRow row in rows)
        {
            int position;
            if (row.ExplicitRowIndex.HasValue)
            {
                position = row.ExplicitRowIndex.Value;
                if (position < 0)
                    throw new InvalidOperationException("A table row position cannot be negative.");
            }
            else
            {
                while (positions.ContainsKey(nextImplicit)) nextImplicit++;
                position = nextImplicit++;
            }

            if (!positions.TryAdd(position, row))
                throw new InvalidOperationException($"Two {group.ToString().ToLowerInvariant()} rows target position {position}.");
            row.ExplicitRowIndex = null;
        }

        int lastPosition = positions.Keys.Max();
        var result = new List<TableRow>(lastPosition + 1);
        for (int position = 0; position <= lastPosition; position++)
        {
            if (!positions.TryGetValue(position, out TableRow? row))
            {
                row = new TableRow
                {
                    IsHeader = group == TableRowGroup.Header,
                    IsFooter = group == TableRowGroup.Footer
                };
            }
            result.Add(row);
        }
        return result;
    }

    private static int ResolveColumnCount(TableElement table)
    {
        if (table.ColumnDefinitions.Count > 0)
            return table.ColumnDefinitions.Count;
        if (table.ColumnWidths.Count > 0)
            return table.ColumnWidths.Count;

        int maximum = 0;
        foreach (TableRow row in table.Rows)
        {
            int sequential = 0;
            foreach (TableCell cell in row.Cells)
            {
                int span = Math.Max(1, cell.ColSpan);
                maximum = Math.Max(maximum, (cell.ExplicitColumnIndex ?? sequential) + span);
                sequential = (cell.ExplicitColumnIndex ?? sequential) + span;
            }
        }
        return maximum;
    }

    private static int FindNextAvailableColumn(
        HashSet<(int Row, int Column)> occupied,
        int row,
        int start,
        int span,
        int columnCount)
    {
        for (int column = Math.Max(0, start); column + span <= columnCount; column++)
        {
            bool available = true;
            for (int offset = 0; offset < span; offset++)
                available &= !occupied.Contains((row, column + offset));
            if (available) return column;
        }
        return columnCount;
    }

    private static TableRowGroup GetGroup(TableRow row)
        => row.IsHeader ? TableRowGroup.Header : row.IsFooter ? TableRowGroup.Footer : TableRowGroup.Body;

    private enum TableRowGroup { Header, Body, Footer }
}
