using PdfBuilder.Elements;
using System;
using System.Collections.Generic;

namespace PdfBuilder.Document
{
    public class TableBuilder
    {
        private readonly ColumnBuilder _column;
        private readonly TableElement _table;

        // Track the current row/cell being edited
        private List<TableCellElement>? _currentRow;
        public TableBuilder FontFamily(string family) { _table.FontFamily = family; return this; }
        public TableBuilder FontSize(float size) { _table.FontSize = size; return this; }
        public TableBuilder TextColor(string color) { _table.TextColor = color; return this; }

        // Header row
        public TableBuilder HeaderFontFamily(string family) { _table.HeaderFontFamily = family; return this; }
        public TableBuilder HeaderFontSize(float size) { _table.HeaderFontSize = size; return this; }
        public TableBuilder HeaderTextColor(string color) { _table.HeaderTextColor = color; return this; }

        // Footer row
        public TableBuilder FooterFontFamily(string family) { _table.FooterFontFamily = family; return this; }
        public TableBuilder FooterFontSize(float size) { _table.FooterFontSize = size; return this; }
        public TableBuilder FooterTextColor(string color) { _table.FooterTextColor = color; return this; }

        public TableBuilder HeaderBackgroundColor(string color) { _table.HeaderBackgroundColor = color; return this; }
        public TableBuilder RowBackgroundColor(string color) { _table.RowBackgroundColor = color; return this; }
        public TableBuilder FooterBackgroundColor(string color)
        {
            _table.FooterBackgroundColor = color;
            return this;
        }
        public TableBuilder HeaderRowHeight(float height) { _table.HeaderRowHeight = height; return this; }
        public TableBuilder RowHeight(float height) { _table.RowHeight = height; return this; }

        public TableBuilder(ColumnBuilder column, float x, float y, float width, float height)
        {
            _column = column;
            _table = new TableElement(x, y, width, height);
        }

        // --- Table-wide config ---
        public TableBuilder WrapText(bool enable)
        {
            _table.WrapText = enable;
            return this;
        }
        
        public TableBuilder Margin(float all) => Margin(all, all, all, all);
        public TableBuilder Margin(float top, float bottom, float left, float right)
        {
            _table.MarginTop = top; _table.MarginBottom = bottom;
            _table.MarginLeft = left; _table.MarginRight = right;
            return this;
        }
        public TableBuilder Padding(float all) => Padding(all, all, all, all);
        public TableBuilder Padding(float top, float bottom, float left, float right)
        {
            _table.PaddingTop = top; _table.PaddingBottom = bottom;
            _table.PaddingLeft = left; _table.PaddingRight = right;
            return this;
        }
        public TableBuilder Border(string color, float width)
        {
            _table.BorderColor = color;
            _table.BorderWidth = width;
            return this;
        }
        public TableBuilder BackgroundColor(string color) { _table.BackgroundColor = color; return this; }
        public TableBuilder CornerRadius(float radius) { _table.CornerRadius = radius; return this; }

        // --- Columns ---

        public TableBuilder ConstantColumn(float width)
        {
            _table.Columns.Add(new TableColumnDefinition { IsConstant = true, Value = width });
            return this;
        }
        public TableBuilder RelativeColumn(float weight = 1f)
        {
            _table.Columns.Add(new TableColumnDefinition { IsConstant = false, Value = weight });
            return this;
        }

        // --- Header ---

        public TableBuilder Header(params Action<TableCellBuilder>[] cellBuilders)
        {
            foreach (var build in cellBuilders)
            {
                var cell = new TableCellElement { Bold = true }; // default: bold header
                var builder = new TableCellBuilder(cell);
                build(builder);
                _table.HeaderCells.Add(cell);
            }
            return this;
        }

        // --- Rows ---

        public TableBuilder Row(params Action<TableCellBuilder>[] cellBuilders)
        {
            var row = new List<TableCellElement>();
            foreach (var build in cellBuilders)
            {
                var cell = new TableCellElement();
                var builder = new TableCellBuilder(cell);
                build(builder);
                row.Add(cell);
            }
            _table.Rows.Add(row);
            return this;
        }

        // --- Footer (optional) ---

        public TableBuilder Footer(params Action<TableCellBuilder>[] cellBuilders)
        {
            var cells = new List<TableCellElement>();

            foreach (var build in cellBuilders)
            {
                var cell = new TableCellElement { Bold = true };
                var builder = new TableCellBuilder(cell);
                build(builder);
                cells.Add(cell);
            }

            _table.FooterCells = cells;
            return this;
        }


        // --- Finalize (add table to the page) ---

        public ColumnBuilder Add()
        {
            Console.WriteLine("=== Debug Table ===");
            Console.WriteLine($"Headers: {string.Join(", ", _table.HeaderCells.Select(c => c.Text))}");
            foreach (var row in _table.Rows)
                Console.WriteLine($"Row: {string.Join(", ", row.Select(c => c.Text))}");
            if (_table.FooterCells != null)
                Console.WriteLine($"Footer: {string.Join(", ", _table.FooterCells.Select(c => c.Text))}");
            Console.WriteLine("=== End Debug Table ===");

            _column.AddTable(_table);
            return _column;
        }

        // --- Cell Builder (nested helper) ---

        public class TableCellBuilder
        {
            private readonly TableCellElement _cell;
            public TableCellBuilder(TableCellElement cell) { _cell = cell; }

            public TableCellBuilder Text(string text) { _cell.Text = text; return this; }
            public TableCellBuilder FontFamily(string family) { _cell.FontFamily = family; return this; }
            public TableCellBuilder FontSize(float size) { _cell.FontSize = size; return this; }
            public TableCellBuilder Bold() { _cell.Bold = true; return this; }
            public TableCellBuilder FontColor(string color) { _cell.FontColor = color; return this; }
            public TableCellBuilder BackgroundColor(string color) { _cell.BackgroundColor = color; return this; }
            public TableCellBuilder AlignLeft() { _cell.Alignment = TableCellAlignment.Left; return this; }
            public TableCellBuilder AlignCenter() { _cell.Alignment = TableCellAlignment.Center; return this; }
            public TableCellBuilder AlignRight() { _cell.Alignment = TableCellAlignment.Right; return this; }
            public TableCellBuilder Padding(float all) => Padding(all, all, all, all);
            public TableCellBuilder Padding(float top, float bottom, float left, float right)
            {
                _cell.PaddingTop = top; _cell.PaddingBottom = bottom;
                _cell.PaddingLeft = left; _cell.PaddingRight = right;
                return this;
            }
            public TableCellBuilder WrapText(bool enable)
            {
                _cell.WrapText = enable;
                return this;
            }
            public TableCellBuilder ColSpan(int span) { _cell.ColSpan = span; return this; }
            public TableCellBuilder RowSpan(int span) { _cell.RowSpan = span; return this; }
        }
    }
}
