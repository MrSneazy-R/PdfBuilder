using PdfBuilder.Document;
using PdfBuilder.Models;
using System.Collections.Generic;

namespace PdfBuilder.Elements
{
    public class TableElement : PdfElement
    {
        // Column definitions: constant or relative sizing
        public List<TableColumnDefinition> Columns { get; set; } = new();

        // Header row (optional, styled)
        public List<TableCellElement> HeaderCells { get; set; } = new();

        // Table body: rows of cells (row-major order)
        public List<List<TableCellElement>> Rows { get; set; } = new();

        // Optional: footer row (for totals)
        public List<TableCellElement>? FooterCells { get; set; }

        public bool WrapText { get; set; } = false;
        // Table-wide defaults
        public string? FontFamily { get; set; }
        public float? FontSize { get; set; }
        public string? TextColor { get; set; }

        // Header row overrides
        public string? HeaderFontFamily { get; set; }
        public float? HeaderFontSize { get; set; }
        public string? HeaderTextColor { get; set; }
        // Table-wide and header/body/row background
        public string? HeaderBackgroundColor { get; set; }
        public string? RowBackgroundColor { get; set; }
        public string FooterBackgroundColor { get; set; }

        // Row heights (optional, use float? so user can leave unset)
        public float? HeaderRowHeight { get; set; }
        public float? RowHeight { get; set; }
        // Footer row overrides (optional, for completeness)
        public string? FooterFontFamily { get; set; }
        public float? FooterFontSize { get; set; }
        public string? FooterTextColor { get; set; }
        // Table-wide styling
        public float? MarginTop { get; set; }
        public float? MarginBottom { get; set; }
        public float? MarginLeft { get; set; }
        public float? MarginRight { get; set; }
        public float? PaddingTop { get; set; }
        public float? PaddingBottom { get; set; }
        public float? PaddingLeft { get; set; }
        public float? PaddingRight { get; set; }

        public string? BorderColor { get; set; }
        public float? BorderWidth { get; set; }
        public string? BackgroundColor { get; set; }
        public float? CornerRadius { get; set; }

        // Constructor: requires position and width/height of table
        public TableElement(float x, float y, float width, float height) : base(x, y)
        {
            Width = width;
            Height = height;
        }

        // Table dimension for layout
        public float Width { get; set; }
        public float Height { get; set; }
    }

    public class TableColumnDefinition
    {
        // True = constant width, False = relative width
        public bool IsConstant { get; set; } = false;
        public float Value { get; set; } // If constant: width in points; If relative: weight (e.g. 1.0)
    }

    public class TableCellElement
    {
        // Cell content (text or future: image, checkbox, etc.)
        public string? Text { get; set; }
        // Alignment
        public TableCellAlignment Alignment { get; set; } = TableCellAlignment.Left;
        // Cell styling
        public string? FontFamily { get; set; }
        public float? FontSize { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public string? FontColor { get; set; }
        public string? BackgroundColor { get; set; }
        public float? PaddingTop { get; set; }
        public float? PaddingBottom { get; set; }
        public float? PaddingLeft { get; set; }
        public float? PaddingRight { get; set; }
        public bool WrapText { get; set; } = false;
        // Column/row spanning
        public int ColSpan { get; set; } = 1;
        public int RowSpan { get; set; } = 1;
    }

    public enum TableCellAlignment
    {
        Left,
        Center,
        Right
    }
}
