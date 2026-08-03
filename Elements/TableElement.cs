using System;
using System.Collections.Generic;
using System.Drawing;
using PdfBuilder.Document;
using PdfBuilder.Document.TextShaping;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Elements
{
    // -------------------------------
    // Enums
    // -------------------------------
    public enum CellOverflowPolicy
    {
        Wrap,       // multi-line wrapping within cell bounds
        Clip,       // single line, clipped at cell bounds
        Ellipsis    // single line, truncates tail with "…"
    }

    public enum CellWordBreak
    {
        Normal,     // wrap on whitespace / natural break points only
        BreakWord   // allow breaking long tokens mid-word
    }

    // -------------------------------
    // Root Table Element
    // -------------------------------
    public class TableElement : PdfElement
    {
        // ---- Table Layout ----
        public float? TableWidth { get; set; } = null;             // null = auto-fit to content/columns
        public List<float> ColumnWidths { get; set; } = new();     // optional fixed widths
        public List<TableModels.TableColumnDefinition> ColumnDefinitions { get; set; } = new();

        // Optional caption rendered by the table renderer
        public string? CaptionText { get; set; } = null;
        public HorizontalAlign CaptionAlign { get; set; } = HorizontalAlign.Left;

        // ---- Table Styles ----
        public Color? HeaderBackground { get; set; } = PdfDefaults.HeaderBackground;
        public Color? AltRowBackground { get; set; } = null; // default: OFF
        public int AltRowEvery { get; set; } = 2;            // keep a sensible cadence for when it IS enabled
        public int AltRowStartIndex { get; set; } = 0;
        public Color BorderColor { get; set; } = Color.Black;
        public float BorderWidth { get; set; } = PdfDefaults.DefaultBorderWidth;
        public TableModels.BorderStyle? BorderStyle { get; set; }
        public float CellPadding { get; set; } = 4f;                // fallback when cell/column padding not set
        public string DefaultFont { get; set; } = "Helvetica";
        public float DefaultFontSize { get; set; } = 10f;
        public TableModels.BorderCollapseMode BorderCollapse { get; set; } = TableModels.BorderCollapseMode.Separate;
        public TableModels.BorderStyle? OuterBorder { get; set; }
        public TableModels.BorderStyle? InnerBorder { get; set; }
        public TableModels.RowBandingSpec? RowBanding { get; set; }
        public TableModels.ColumnBandingSpec? ColumnBanding { get; set; }
        public TableModels.TextStyle DefaultTextStyle { get; set; } = new TableModels.TextStyle();
        public float OuterCornerRadiusTopLeft { get; set; }
        public float OuterCornerRadiusTopRight { get; set; }
        public float OuterCornerRadiusBottomRight { get; set; }
        public float OuterCornerRadiusBottomLeft { get; set; }
        internal int RowBandOffset { get; set; } = 0;

        // Per-column style defaults (optional, cell overrides win)
        public List<TableColumnStyle> ColumnStyles { get; set; } = new();

        // ---- Rows ----
        public List<TableRow> Rows { get; set; } = new();

        // ---- Pagination & Layout ----
        public bool EnablePageBreaks { get; set; } = true;
        public bool RepeatHeaders { get; set; } = true;
        public int MinRowsAtPageStart { get; set; } = 1;  // orphan control
        public int MinRowsAtPageEnd { get; set; } = 1;    // widow control

        // Content area vertical bounds (PDF coordinates: origin at bottom-left; your builder supplies top/bottom Y)
        public float? PageTopY { get; set; } = null;      // e.g., top Y of content region
        public float? PageBottomY { get; set; } = null;   // e.g., bottom Y of content region

        // Page-break callback: should add a page and return the new starting Y for the next segment
        public Func<float, float>? OnPageBreak { get; set; } = null;

        // Headers: if null, consecutive top rows with IsHeader=true form the header block
        public int? HeaderRowCount { get; set; } = null;

        // ---- Borders / Frames ----
        public bool ResolveBorderConflicts { get; set; } = true; // avoid double-stroking inner borders
        public bool DrawOuterFrame { get; set; } = true;
        public Color OuterFrameColor { get; set; } = Color.Black;
        public float OuterFrameWidth { get; set; } = 0.5f;

        // ---- Text overflow policy (default for the table; cells may honor/override at render time if needed) ----
        public CellOverflowPolicy OverflowPolicy { get; set; } = CellOverflowPolicy.Wrap;
        public bool AutoSizeColumns { get; set; } = true;
        public TableElement() : base(0, 0) { }
        public TableElement(float x, float y) : base(x, y) { }
        public bool KeepWithNext { get; set; } = false;
        public bool AvoidBreakInside { get; set; } = false; // when true, force start on new page if it doesn't fit

    }

    // -------------------------------
    // Per-Column Defaults (optional)
    // -------------------------------
    public class TableColumnStyle
    {
        public int Index { get; set; }                              // 0-based column index
        public HorizontalAlign? HAlign { get; set; } = null;
        public VerticalAlign? VAlign { get; set; } = null;
        public string? Font { get; set; } = null;
        public float? FontSize { get; set; } = null;
        public Color? TextColor { get; set; } = null;
        public Color? Background { get; set; } = null;

        // Padding defaults (per side)
        public float? PaddingTop { get; set; } = null;
        public float? PaddingRight { get; set; } = null;
        public float? PaddingBottom { get; set; } = null;
        public float? PaddingLeft { get; set; } = null;

        // Optional width override for this column (if you want to override ColumnWidths entry at layout time)
        public float? OverrideWidth { get; set; } = null;
    }

    // -------------------------------
    // Row Model
    // -------------------------------
    public class TableRow
    {
        public List<TableCell> Cells { get; set; } = new();

        // Row-level styling
        public Color? BackgroundColor { get; set; }
        public float? RowHeight { get; set; } = null;
        public bool IsHeader { get; set; } = false;

        // Pagination hints
        public bool KeepWithNext { get; set; } = false;

        // Optional thicker separators for emphasis bands
        public bool ThickTopBorder { get; set; } = false;
        public bool ThickBottomBorder { get; set; } = false;
        public float ThickBorderWidth { get; set; } = 1.5f;
        public Color? ThickBorderColor { get; set; } = null;

        public TableRow() { }
        public TableRow(params TableCell[] cells) => Cells.AddRange(cells);
    }

    // -------------------------------
    // Cell Model
    // -------------------------------
    public class TableCell
    {
        // ---- Content ----
        public string Text { get; set; } = string.Empty;
        public List<TableModels.InlineRun> TextRuns { get; set; } = new();
        public TableModels.TextStyle? TextStyle { get; set; } = null;

        // Typography
        public string Font { get; set; } = "Helvetica";
        public float FontSize { get; set; } = 10f;
        public Color TextColor { get; set; } = Color.Black;
        public bool Bold { get; set; } = false;
        public bool Italic { get; set; } = false;
        public bool Underline { get; set; } = false;
        public bool Strikethrough { get; set; } = false;
        public bool Overline { get; set; } = false;
        public bool SmallCaps { get; set; } = false;
        public float? LineHeight { get; set; } = null;     // multiplier (e.g., 1.2f)
        public int? MaxLines { get; set; } = null;         // limit visible lines (with Wrap/Ellipsis)

        public CellWordBreak WordBreak { get; set; } = CellWordBreak.Normal;
        public float RotationDegrees { get; set; } = 0f;   // rotate text around its origin inside the cell

        // ---- Alignment ----
        public HorizontalAlign HorizontalAlign { get; set; } = HorizontalAlign.Left;
        public VerticalAlign VerticalAlign { get; set; } = VerticalAlign.Top;

        // ---- Background & Borders ----
        public Color? BackgroundColor { get; set; } = null;
        public float CornerRadius { get; set; } = 0f;      // rounded background/border for the cell
        public float CornerRadiusTopLeft { get; set; }
        public float CornerRadiusTopRight { get; set; }
        public float CornerRadiusBottomRight { get; set; }
        public float CornerRadiusBottomLeft { get; set; }

        public Color BorderColor { get; set; } = Color.Black;
        public float BorderWidth { get; set; } = PdfDefaults.DefaultBorderWidth;
        public TableModels.BorderStyle? BorderStyle { get; set; }

        public bool BorderTop { get; set; } = true;
        public bool BorderBottom { get; set; } = true;
        public bool BorderLeft { get; set; } = true;
        public bool BorderRight { get; set; } = true;

        // add fields
        public Color? BorderColorTop { get; set; }
        public Color? BorderColorRight { get; set; }
        public Color? BorderColorBottom { get; set; }
        public Color? BorderColorLeft { get; set; }

        public float? BorderWidthTop { get; set; }
        public float? BorderWidthRight { get; set; }
        public float? BorderWidthBottom { get; set; }
        public float? BorderWidthLeft { get; set; }
        public TableModels.BorderStyle? BorderStyleTop { get; set; }
        public TableModels.BorderStyle? BorderStyleRight { get; set; }
        public TableModels.BorderStyle? BorderStyleBottom { get; set; }
        public TableModels.BorderStyle? BorderStyleLeft { get; set; }

        // ---- Padding ----
        // If null the renderer should fall back to TableElement.CellPadding
        public float? Padding { get; set; } = null;        // legacy/all-sides convenience
        public float? PaddingTop { get; set; } = null;
        public float? PaddingRight { get; set; } = null;
        public float? PaddingBottom { get; set; } = null;
        public float? PaddingLeft { get; set; } = null;

        // ---- Spanning ----
        public int ColSpan { get; set; } = 1;
        public int RowSpan { get; set; } = 1;

        public TableCell() { }
        public TableCell(string text) { Text = text; }

        internal RichTextLayoutResult? CachedLayout { get; set; }
        internal float CachedLayoutWidth { get; set; } = -1f;
        internal float CachedContentHeight { get; set; }
    }
}


