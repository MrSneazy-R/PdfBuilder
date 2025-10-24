Typography & HarfBuzz Integration
=================================

Overview
--------
PdfBuilder uses HarfBuzz (via SkiaSharp.HarfBuzz) for shaping text in `TextElement`, `RichTextElement`, and table cells. Complex scripts, ligatures, diacritics, and mixed-direction paragraphs render correctly without manual intervention.

Key Components
--------------
- `TextShaper` (`Document/TextShaping/TextShaper.cs`): shapes paragraphs into glyph runs with directionality and line wrapping.
- `RichTextLayouter` and `TextElementLayouter`: manage line breaking, hyphenation, rotation, ellipsis, and fallback fonts.
- Tests in `tests/PdfBuilder.Tests/HarfbuzzIntegrationTests.cs` verify:
  - Unicode text round-trips through extraction (`TableCell_WithInternationalText_RoundTripsThroughExtractor`).
  - Rotated cells produce proper text matrices.
  - Hyphenation, ellipsis, and mixed-direction text behave as expected.

Best Practices
--------------
1. **Fallback fonts**: supply `TextStyleDefaults.FallbackFonts` or `Table.TextStyle.FallbackFonts` when using scripts not covered by the primary font.
2. **Rotation**: set `TextBuilder.Rotation`, `TableCellBuilder.Rotation`, or `TextElement.Rotation` to rotate text while maintaining glyph alignment.
3. **Hyphenation & wrapping**: choose `TextWrapMode` in tables and `AvoidBreakInside`, `WidowLines`, `OrphanLines` on text blocks to control pagination.
4. **Bidi text**: mixed languages are handled automatically; set `FlowDirection` for paragraphs that should default to RTL.

Example
-------
```csharp
builder.DefaultTextStyle(defaults =>
{
    defaults.FontFamily = "Noto Sans";
    defaults.FallbackFonts = new List<string> { "Noto Sans Arabic", "Noto Sans Hebrew" };
});

page.Compose(flow =>
{
    flow.Text("مرحبا بالعالم • שלום עולם • Hello World")
        .FontSize(14)
        .LineHeight(1.4f);

    flow.Table(table =>
    {
        table.ColumnWidths(200);
        table.Row(row => row.Cell(cell =>
        {
            cell.Text("Rotation demo");
            cell.Rotation(90);
        }));
    });
});
```

Expected Outcome
----------------
- The combined Arabic, Hebrew, and Latin sentence renders in the correct visual order with appropriate glyph shaping.
- The rotated table cell text uses a proper transformation matrix (validated by tests) so glyphs remain crisp.
