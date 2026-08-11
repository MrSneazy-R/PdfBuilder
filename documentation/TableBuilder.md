TableBuilder
============

Canonical container cells
-------------------------
The preferred coordinate-free API treats every `ITableCellDescriptor` as an `IContainer`.
`Cell().Text(...)` remains the concise text convenience, while the same cell can compose
rich text, images, SVG, barcodes, columns, rows, grids, stacks, layers, and reusable
components. Content is measured and drawn by the normal canonical component pipeline;
there is no separate nested cell-layout engine.

```csharp
container.Table(table =>
{
    table.Columns(columns =>
    {
        columns.ConstantColumn(90);
        columns.RelativeColumn();
    });
    table.Header(row =>
    {
        row.Cell().Background("Primary").Padding("Compact").Text("Item").Bold();
        row.Cell().Background("Primary").Padding("Compact").Text("Details").Bold();
    });
    table.Row(row =>
    {
        row.Cell().Padding("Compact").Image(logoBytes, 28, 28);
        row.Cell().Padding("Compact").Column(column =>
        {
            column.Spacing("Compact");
            column.Item().RichText(text =>
            {
                text.Span("Reusable ").Bold();
                text.Span("rich content").Italic();
            });
            column.Item().Component(productDetails, model);
        });
    });
});
```

Cell padding, backgrounds, borders, corner radius, horizontal/vertical alignment, and
clipping remain cell properties. Named colours and spacing tokens resolve against the
current document theme, including inside reused components and repeated header cells.
Rows remain atomic in this release: content that cannot complete within one usable row
fails with an explicit error instead of silently clipping or looping. Controlled row
continuation is introduced separately.

Purpose
-------
`TableBuilder` creates `TableElement` instances with advanced pagination, styling, and typography controls. It supports headers, banding, per-cell overrides, spans, rotation, and HarfBuzz-shaped runs.

Initialization
--------------
Acquire a builder through `ColumnBuilder.Table(x, y, width, estimatedRowHeight)` or via helper methods on your column flow.

Table-Level Configuration
-------------------------
- `At(float x, float y)`: override anchor point.
- `TableWidth(float)`: fixed width; omit to auto-fit the content column.
- `ColumnWidths(params float[])`: set absolute column widths.
- `ColumnLayout(params TableColumnDefinition[])`: declarative column definitions with min/max/flex modes.
- `Caption(string text, HorizontalAlign align)`: add a caption above the table.
- `DefaultFont(string family)` / `DefaultFontSize(float)` / `CellPadding(float)`: typography and padding defaults.
- `Border(Color|hex, float width)`: outer table border.
- `HeaderBackground`, `AltRowBackground`, `AltRowEvery(int every, int startIndex)`: banded styling.
- `EnablePageBreaks(bool)`: toggle automatic pagination.
- `PageBounds(float topY, float bottomY)`: override vertical bounds for custom flows.
- `OnPageBreak(Func<float, float>)`: handle manual page break transitions.
- `RepeatHeaders(bool)`: control header repetition on new pages (enabled by default).
- `CornerRadius(float)`, `InnerBorder`, `OuterBorder`, `BorderCollapseMode`: apply rounded corners and fine-tune border rendering.
- `RowBanding(RowBandingSpec)` / `ColumnBanding(ColumnBandingSpec)`: multiple banding schemes beyond simple alternation.
- `OuterBorder(BorderStyle)` / `InnerBorder(BorderStyle)`: global border styles with dash patterns, join styles, etc.

Row Builders
------------
- `HeaderRow(params Action<TableCellBuilder>[] cells)`: define header rows (respected by pagination).
- `Row(params Action<TableCellBuilder>[] cells)`: add body rows.
- `SectionHeader(Action<TableCellBuilder>)`: row that does not repeat across page breaks.
- `RowSpanGroup(string key, Action<TableCellBuilder> configure)`: convenience for multi-row spans.
- `ApplyCellDefaults(Action<TableCell>)`: mutate each cell as it is created (useful for consistent padding).

TableCellBuilder Highlights
---------------------------
- `Text(string)`/`Text(Action<TextElement>)`: cell content.
- `RichText(Action<TableCellBuilder.RichTextCell>)`: inline runs with per-span styling.
- `Bold()`, `Italic()`, `Font(string, float)`: typography overrides.
- `TextStyle(Action<Table.TextStyle>)`:: access to advanced settings (wrap mode, letter/word spacing, fallback fonts, rotations).
- `AlignLeft/Right/Center`, `VerticalAlign(VerticalAlign)`: alignment.
- `Background(Color|hex)`, `BackgroundOpacity(float)`.
- `BorderTop/Right/Bottom/Left(Color|hex, float width)`: per-side borders with override colors.
- `BorderStyle(Action<BorderStyle>)`: apply dash pattern, miter limit, join caps.
- `Padding(...)`: per-side padding; defaults fallback to table-level padding.
- `ColSpan(int)`, `RowSpan(int)`.
- `Rotation(float degrees)`: rotate cell content with proper text matrix (validated by HarfBuzz tests).
- `Wrap(TextWrapMode)`, `Hyphenation(bool)`, `EllipsisWhenClipped`: text overflow options.
- `FallbackFonts(params string[])`: provide font fallbacks per cell or per run.

Example
-------
```csharp
page.Column(col =>
{
    float tableWidth = page.Width - page.MarginLeft - page.MarginRight;
    col.Table(page.MarginLeft, col.GetCurrentY(), tableWidth, 0)
       .Caption("Revenue by Region", HorizontalAlign.Center)
       .DefaultFont("Helvetica")
       .DefaultFontSize(11)
       .CellPadding(6)
       .OuterBorder(new BorderStyle { Color = "#555555", Width = 1.5f, LineJoin = BorderLineJoin.Miter })
       .InnerBorder(new BorderStyle { Color = "#DDDDDD", Width = 0.5f })
       .RowBanding(new RowBandingSpec
       {
           Step = 2,
           Fills = new[] { "#FFFFFF", "#F9FAFB" }
       })
       .HeaderRow(
           c => c.Text("Region").Bold().Background("#1F2933").TextColor("#FFFFFF"),
           c => c.Text("Q1").AlignRight().Background("#1F2933").TextColor("#FFFFFF"),
           c => c.Text("Q2").AlignRight().Background("#1F2933").TextColor("#FFFFFF"))
       .Row(
           c => c.Text("North America"),
           c => c.Text("$1.2M").AlignRight(),
           c => c.Text("$1.4M").AlignRight())
       .Row(
           c => c.Text("EMEA"),
           c => c.Text("$980K").AlignRight(),
           c => c.Text("$1.1M").AlignRight())
       .Row(
           c => c.RichText(r =>
           {
               r.Span("APAC ").Bold();
               r.Span("↑ 6% YoY").Color("#10B981");
           }),
           c => c.Text("$860K").AlignRight(),
           c => c.Text("$910K").AlignRight())
       .Add();
});
```

Expected Outcome
----------------
- A captioned table centered within the column.
- Header row uses a dark background with white text, repeated on subsequent pages if the table breaks.
- Alternating row fill colors provide zebra banding.
- The APAC row shows mixed styling within a single cell (bold label plus green growth indicator).
- Right-aligned currency columns keep numbers aligned at the decimal separator.
