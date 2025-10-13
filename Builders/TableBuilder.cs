using PdfBuilder.Elements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static PdfBuilder.Writer.PdfDefaults;

namespace PdfBuilder.Document
{
    /// <summary>
    /// Fluent builder for the TableElement/TableRow/TableCell model.
    /// Includes pagination, repeating headers, overflow, borders, spans,
    /// per-column defaults, rich text styles, rotation, rounded corners, etc.
    /// </summary>
    public class TableBuilder
    {
        private readonly ColumnBuilder _column;
        private readonly TableElement _table;

        public TableBuilder(ColumnBuilder column, float x, float y, float tableWidth = 0)
        {
            _column = column ?? throw new ArgumentNullException(nameof(column));
            _table = new TableElement(x, y)
            {
                TableWidth = tableWidth > 0 ? tableWidth : null
            };
        }

        /// <summary>Retrieve the built TableElement without adding to the column.</summary>
        public TableElement Build() => _table;

        /// <summary>Finalize and add this table to the column (expects ColumnBuilder.AddTable).</summary>
        public float Add()
        {
            float height = _column.AddTable(_table);
            var flow = _column.GetFlow();
            if (!flow.CanFit(height))
                flow.Advance(height);
            else
                flow.Reserve(height);

            return height;
        }

        // ------------------ Position / Size ------------------

        public TableBuilder At(float x, float y) { _table.X = x; _table.Y = y; return this; }
        public TableBuilder TableWidth(float width) { _table.TableWidth = width; return this; }
        public TableBuilder ColumnWidths(params float[] widths)
        {
            _table.ColumnWidths = (widths ?? Array.Empty<float>()).ToList();
            return this;
        }

        // ------------------ Caption ------------------

        public TableBuilder Caption(string text, HorizontalAlign align = HorizontalAlign.Left)
        { _table.CaptionText = text; _table.CaptionAlign = align; return this; }

        // ------------------ Typography / Padding ------------------

        public TableBuilder DefaultFont(string family) { _table.DefaultFont = family; return this; }
        public TableBuilder DefaultFontSize(float size) { _table.DefaultFontSize = size; return this; }
        public TableBuilder CellPadding(float padding) { _table.CellPadding = padding; return this; }

        // ------------------ Table-level Borders / Backgrounds ------------------

        public TableBuilder Border(Color color, float width) { _table.BorderColor = color; _table.BorderWidth = width; return this; }
        public TableBuilder Border(string hexColor, float width) => Border(ParseColor(hexColor), width);


        public TableBuilder HeaderBackground(Color color) { _table.HeaderBackground = color; return this; }
        public TableBuilder HeaderBackground(string hex) => HeaderBackground(ParseColor(hex));

        public TableBuilder AltRowBackground(Color color) { _table.AltRowBackground = color; return this; }
        public TableBuilder AltRowBackground(string hex) => AltRowBackground(ParseColor(hex));

        /// <summary>Zebra configuration: color already set via AltRowBackground().</summary>
        public TableBuilder AltRowEvery(int every, int startIndex = 0)
        { _table.AltRowEvery = Math.Max(1, every); _table.AltRowStartIndex = Math.Max(0, startIndex); return this; }

        // ------------------ Pagination & Headers ------------------

        /// <summary>Enable/disable automatic page breaks (default true in TableElement).</summary>
        public TableBuilder EnablePageBreaks(bool enable = true) { _table.EnablePageBreaks = enable; return this; }

        /// <summary>Set page vertical bounds (top and bottom Y in PDF coordinates for content area).</summary>
        public TableBuilder PageBounds(float pageTopY, float pageBottomY)
        { _table.PageTopY = pageTopY; _table.PageBottomY = pageBottomY; return this; }

        /// <summary>
        /// Set page-break callback. Called when a break is needed. Should add a page & return new starting Y.
        /// Signature: Func&lt;float, float&gt; where arg could be table width if you want to use it.
        /// </summary>
        public TableBuilder OnPageBreak(Func<float, float> handler) { _table.OnPageBreak = handler; return this; }

        /// <summary>Repeat header rows after a page break (default true).</summary>
        public TableBuilder RepeatHeaders(bool repeat = true) { _table.RepeatHeaders = repeat; return this; }

        /// <summary>Explicit header row count at the top of the table. If null, consecutive IsHeader rows are used.</summary>
        public TableBuilder HeaderRowCount(int count) { _table.HeaderRowCount = Math.Max(0, count); return this; }

        /// <summary>Orphan/widow control. Min rows at page start/end (defaults 1/1).</summary>
        public TableBuilder OrphanControl(int minRowsAtPageStart = 1, int minRowsAtPageEnd = 1)
        { _table.MinRowsAtPageStart = Math.Max(0, minRowsAtPageStart); _table.MinRowsAtPageEnd = Math.Max(0, minRowsAtPageEnd); return this; }

        // ------------------ Drawing Policies ------------------

        /// <summary>Resolve inner border conflicts to avoid double-stroking (default true).</summary>
        public TableBuilder BorderConflictResolution(bool on = true) { _table.ResolveBorderConflicts = on; return this; }

        /// <summary>Draw an outer frame around each rendered table segment (default true).</summary>
        public TableBuilder OuterFrame(bool draw = true, Color? color = null, float width = 0.5f)
        {
            _table.DrawOuterFrame = draw;
            if (color.HasValue) _table.OuterFrameColor = color.Value;
            _table.OuterFrameWidth = width;
            return this;
        }
        public TableBuilder OuterFrame(bool draw, string hexColor, float width = 0.5f)
            => OuterFrame(draw, ParseColor(hexColor), width);

        /// <summary>Text overflow policy: Wrap (default), Clip, or Ellipsis.</summary>
        public TableBuilder Overflow(CellOverflowPolicy policy) { _table.OverflowPolicy = policy; return this; }




        // field
        private BorderConflictPolicy _conflictPolicy = BorderConflictPolicy.CollapsedClassic;

        // fluent API (keep yours if it already exists; wire it to this)
        public TableBuilder BorderConflictResolution(BorderConflictPolicy policy)
        {
            _conflictPolicy = policy;
            return this;
        }

        // expose to renderer (in Build() or the object you pass to renderer)
        internal BorderConflictPolicy ConflictPolicy => _conflictPolicy;

        // ------------------ Column defaults ------------------

        /// <summary>Configure per-column default styles (cell-level settings still override).</summary>
        public TableBuilder Column(int index, Action<ColumnStyleBuilder> setup)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            var style = _table.ColumnStyles.FirstOrDefault(c => c.Index == index);
            if (style == null)
            {
                style = new TableColumnStyle { Index = index };
                _table.ColumnStyles.Add(style);
            }
            setup?.Invoke(new ColumnStyleBuilder(style));
            return this;
        }

        public sealed class ColumnStyleBuilder
        {
            private readonly TableColumnStyle _s;
            public ColumnStyleBuilder(TableColumnStyle s) { _s = s; }

            public ColumnStyleBuilder AlignLeft() { _s.HAlign = HorizontalAlign.Left; return this; }
            public ColumnStyleBuilder AlignCenter() { _s.HAlign = HorizontalAlign.Center; return this; }
            public ColumnStyleBuilder AlignRight() { _s.HAlign = HorizontalAlign.Right; return this; }
            public ColumnStyleBuilder VAlignTop() { _s.VAlign = VerticalAlign.Top; return this; }
            public ColumnStyleBuilder VAlignMiddle() { _s.VAlign = VerticalAlign.Middle; return this; }
            public ColumnStyleBuilder VAlignBottom() { _s.VAlign = VerticalAlign.Bottom; return this; }

            public ColumnStyleBuilder Font(string family, float? size = null)
            { _s.Font = family; _s.FontSize = size; return this; }

            public ColumnStyleBuilder TextColor(Color color) { _s.TextColor = color; return this; }
            public ColumnStyleBuilder TextColor(string hex) { _s.TextColor = ParseColor(hex); return this; }

            public ColumnStyleBuilder Background(Color color) { _s.Background = color; return this; }
            public ColumnStyleBuilder Background(string hex) { _s.Background = ParseColor(hex); return this; }

            public ColumnStyleBuilder Padding(float all)
            { _s.PaddingTop = _s.PaddingRight = _s.PaddingBottom = _s.PaddingLeft = all; return this; }

            public ColumnStyleBuilder Padding(float top, float right, float bottom, float left)
            { _s.PaddingTop = top; _s.PaddingRight = right; _s.PaddingBottom = bottom; _s.PaddingLeft = left; return this; }

            /// <summary>Optionally override the defined ColumnWidths entry at layout time.</summary>
            public ColumnStyleBuilder Width(float width) { _s.OverrideWidth = width; return this; }
        }

        // ------------------ Rows ------------------

        /// <summary>Add a header row (IsHeader = true). Use Row(...) for body rows.</summary>
        public TableBuilder HeaderRow(params Action<TableCellBuilder>[] cells)
        {
            var row = new TableRow { IsHeader = true };
            foreach (var build in cells ?? Array.Empty<Action<TableCellBuilder>>())
            {
                var cell = new TableCell();
                build(new TableCellBuilder(cell));
                row.Cells.Add(cell);
            }
            _table.Rows.Add(row);
            return this;
        }

        /// <summary>Add a body row, quick form (cells only).</summary>
        public TableBuilder Row(params Action<TableCellBuilder>[] cells)
        {
            var row = new TableRow();
            foreach (var build in cells ?? Array.Empty<Action<TableCellBuilder>>())
            {
                var cell = new TableCell();
                build(new TableCellBuilder(cell));
                row.Cells.Add(cell);
            }
            _table.Rows.Add(row);
            return this;
        }

        /// <summary>Add a row with explicit row-level styling and properties.</summary>
        public TableBuilder Row(Action<RowBuilder> rowBuilder)
        {
            var row = new TableRow();
            rowBuilder?.Invoke(new RowBuilder(row));
            _table.Rows.Add(row);
            return this;
        }

        /// <summary>Add a footer-like row (just a body row with your styling).</summary>
        public TableBuilder FooterRow(params Action<TableCellBuilder>[] cells)
        {
            var row = new TableRow();
            foreach (var build in cells ?? Array.Empty<Action<TableCellBuilder>>())
            {
                var cell = new TableCell();
                build(new TableCellBuilder(cell));
                row.Cells.Add(cell);
            }
            _table.Rows.Add(row);
            return this;
        }

        // ------------------ Nested builders ------------------

        public class RowBuilder
        {
            private readonly TableRow _row;
            public RowBuilder(TableRow row) { _row = row; }

            public RowBuilder IsHeader(bool value = true) { _row.IsHeader = value; return this; }
            public RowBuilder Background(Color color) { _row.BackgroundColor = color; return this; }
            public RowBuilder Background(string hex) => Background(ParseColor(hex));
            public RowBuilder Height(float height) { _row.RowHeight = height; return this; }
            public RowBuilder KeepWithNext(bool value = true) { _row.KeepWithNext = value; return this; }

            /// <summary>Emphasize with a thick top border across the row.</summary>
            public RowBuilder ThickTopBorder(float width = 1.5f, Color? color = null)
            { _row.ThickTopBorder = true; _row.ThickBorderWidth = width; _row.ThickBorderColor = color; return this; }

            /// <summary>Emphasize with a thick bottom border across the row.</summary>
            public RowBuilder ThickBottomBorder(float width = 1.5f, Color? color = null)
            { _row.ThickBottomBorder = true; _row.ThickBorderWidth = width; _row.ThickBorderColor = color; return this; }

            public RowBuilder Cells(params Action<TableCellBuilder>[] cells)
            {
                foreach (var build in cells ?? Array.Empty<Action<TableCellBuilder>>())
                {
                    var cell = new TableCell();
                    build(new TableCellBuilder(cell));
                    _row.Cells.Add(cell);
                }
                return this;
            }
        }

        public class TableCellBuilder
        {
            private readonly TableCell _cell;
            public TableCellBuilder(TableCell cell) { _cell = cell; }

            // -------- Content / Typography --------
            public TableCellBuilder Text(string? text) { _cell.Text = text ?? string.Empty; return this; }
            public TableCellBuilder Font(string family, float? size = null)
            {
                _cell.Font = family ?? _cell.Font;
                if (size.HasValue) _cell.FontSize = size.Value;
                return this;
            }
            public TableCellBuilder FontSize(float size) { _cell.FontSize = size; return this; }
            public TableCellBuilder TextColor(Color color) { _cell.TextColor = color; return this; }
            public TableCellBuilder TextColor(string hex) => TextColor(ParseColor(hex));

            public TableCellBuilder Bold() { _cell.Bold = true; return this; }
            public TableCellBuilder Italic() { _cell.Italic = true; return this; }
            public TableCellBuilder Underline() { _cell.Underline = true; return this; }
            public TableCellBuilder Strikethrough() { _cell.Strikethrough = true; return this; }
            public TableCellBuilder Overline() { _cell.Overline = true; return this; }
            public TableCellBuilder SmallCaps() { _cell.SmallCaps = true; return this; }
            public TableCellBuilder LineHeight(float lh) { _cell.LineHeight = lh; return this; }
            public TableCellBuilder MaxLines(int n) { _cell.MaxLines = Math.Max(1, n); return this; }
            public TableCellBuilder WordBreak(CellWordBreak wb) { _cell.WordBreak = wb; return this; }

            // -------- Alignment --------
            public TableCellBuilder AlignLeft() { _cell.HorizontalAlign = HorizontalAlign.Left; return this; }
            public TableCellBuilder AlignCenter() { _cell.HorizontalAlign = HorizontalAlign.Center; return this; }
            public TableCellBuilder AlignRight() { _cell.HorizontalAlign = HorizontalAlign.Right; return this; }
            public TableCellBuilder VAlignTop() { _cell.VerticalAlign = VerticalAlign.Top; return this; }
            public TableCellBuilder VAlignMiddle() { _cell.VerticalAlign = VerticalAlign.Middle; return this; }
            public TableCellBuilder VAlignBottom() { _cell.VerticalAlign = VerticalAlign.Bottom; return this; }

            // -------- Background & Borders --------
            public TableCellBuilder Background(Color color) { _cell.BackgroundColor = color; return this; }
            public TableCellBuilder Background(string hex) => Background(ParseColor(hex));

            /// <summary>Rounded background/border corners for the cell.</summary>
            public TableCellBuilder CornerRadius(float r) { _cell.CornerRadius = Math.Max(0, r); return this; }

            // Borders (per side or all)
            public TableCellBuilder Border(Color color, float width = 1f, bool top = true, bool right = true, bool bottom = true, bool left = true)
            {
                _cell.BorderColor = color;
                _cell.BorderWidth = width;
                _cell.BorderTop = top;
                _cell.BorderRight = right;
                _cell.BorderBottom = bottom;
                _cell.BorderLeft = left;
                return this;
            }
            public TableCellBuilder Border(string hex, float width = 1f, bool top = true, bool right = true, bool bottom = true, bool left = true)
                => Border(ParseColor(hex), width, top, right, bottom, left);

            public TableCellBuilder NoBorder()
            {
                _cell.BorderTop = _cell.BorderRight = _cell.BorderBottom = _cell.BorderLeft = false;
                return this;
            }

            // -------- Padding --------
            /// <summary>All-sides padding (legacy convenience). Renderer falls back to table.CellPadding if null.</summary>
            public TableCellBuilder Padding(float all) { _cell.Padding = all; return this; }

            /// <summary>Per-side padding.</summary>
            public TableCellBuilder Padding(float top, float right, float bottom, float left)
            { _cell.PaddingTop = top; _cell.PaddingRight = right; _cell.PaddingBottom = bottom; _cell.PaddingLeft = left; return this; }

            public TableCellBuilder PaddingTop(float v) { _cell.PaddingTop = v; return this; }
            public TableCellBuilder PaddingRight(float v) { _cell.PaddingRight = v; return this; }
            public TableCellBuilder PaddingBottom(float v) { _cell.PaddingBottom = v; return this; }
            public TableCellBuilder PaddingLeft(float v) { _cell.PaddingLeft = v; return this; }

            // -------- Rotation --------
            public TableCellBuilder Rotation(float degrees) { _cell.RotationDegrees = degrees; return this; }

            // -------- Spans --------
            public TableCellBuilder ColSpan(int span) { _cell.ColSpan = Math.Max(1, span); return this; }
            public TableCellBuilder RowSpan(int span) { _cell.RowSpan = Math.Max(1, span); return this; }


            public TableCellBuilder BorderTop(Color color, float width = 1f)
            { _cell.BorderTop = true; _cell.BorderColorTop = color; _cell.BorderWidthTop = width; return this; }
            public TableCellBuilder BorderRight(Color color, float width = 1f)
            { _cell.BorderRight = true; _cell.BorderColorRight = color; _cell.BorderWidthRight = width; return this; }
            public TableCellBuilder BorderBottom(Color color, float width = 1f)
            { _cell.BorderBottom = true; _cell.BorderColorBottom = color; _cell.BorderWidthBottom = width; return this; }
            public TableCellBuilder BorderLeft(Color color, float width = 1f)
            { _cell.BorderLeft = true; _cell.BorderColorLeft = color; _cell.BorderWidthLeft = width; return this; }

            public TableCellBuilder BorderTop(string hex, float width = 1f) => BorderTop(ParseColor(hex), width);
            public TableCellBuilder BorderRight(string hex, float width = 1f) => BorderRight(ParseColor(hex), width);
            public TableCellBuilder BorderBottom(string hex, float width = 1f) => BorderBottom(ParseColor(hex), width);
            public TableCellBuilder BorderLeft(string hex, float width = 1f) => BorderLeft(ParseColor(hex), width);
        }

        // ------------------ Helpers ------------------

        private static Color ParseColor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Color.Black;

            hex = hex.Trim();
            if (hex.StartsWith("#")) hex = hex[1..];

            // 3-digit RGB (#RGB)
            if (hex.Length == 3)
            {
                hex = new string(new[]
                {
                    hex[0], hex[0],
                    hex[1], hex[1],
                    hex[2], hex[2]
                });
            }
            // 4-digit RGBA (#RGBA)
            else if (hex.Length == 4)
            {
                hex = new string(new[]
                {
                    hex[0], hex[0],
                    hex[1], hex[1],
                    hex[2], hex[2],
                    hex[3], hex[3]
                });
            }

            // Parse 6-digit RGB
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                return Color.FromArgb(r, g, b);
            }

            // Parse 8-digit RGBA
            if (hex.Length == 8 &&
                int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r8) &&
                int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g8) &&
                int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b8) &&
                int.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out var a8))
            {
                return Color.FromArgb(a8, r8, g8, b8);
            }

            // Fallback to named colors
            return hex.ToLowerInvariant() switch
            {
                "black" => Color.Black,
                "white" => Color.White,
                "gray" or "grey" => Color.Gray,
                "red" => Color.Red,
                "green" => Color.Green,
                "blue" => Color.Blue,
                _ => Color.Black
            };
        }
    }
}
