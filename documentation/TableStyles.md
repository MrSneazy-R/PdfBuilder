Table Styling Models
====================

This guide documents supporting classes under `Elements/Table` that give granular control over table appearance and behavior.

TableElement (core properties)
------------------------------
- Dimensions: `TableWidth`, `ColumnWidths`, `AutoSizeColumns`.
- Borders: `BorderWidth`, `BorderStyle`, `OuterBorder`, `InnerBorder`, `BorderCollapse`, `ResolveBorderConflicts`, `DrawOuterFrame`.
- Padding & typography: `CellPadding`, `DefaultFont`, `DefaultFontSize`, `DefaultTextStyle`.
- Corners: `OuterCornerRadiusTopLeft/Right/BottomLeft/BottomRight`.
- Pagination: `EnablePageBreaks`, `RepeatHeaders`, `MinRowsAtPageStart`, `MinRowsAtPageEnd`, `KeepWithNext`, `AvoidBreakInside`.
- Page bounds: `PageTopY`, `PageBottomY` (override automatic placement).

Row & Cell Options
------------------
- Rows: `RowHeight`, `IsHeader`, `ThickTopBorder`, `ThickBottomBorder`, `ThickBorderWidth`, `ThickBorderColor`, `KeepWithNext`.
- Cells:
  - Typography: `Font`, `FontSize`, `Bold`, `Italic`, `Underline`, `Strikethrough`, `Overline`, `SmallCaps`, `LineHeight`, `MaxLines`.
  - Alignment: `HorizontalAlign`, `VerticalAlign`.
  - Borders: `BorderTop/Right/Bottom/Left`, per-side colors/widths via `BorderColorTop` etc., or set `BorderStyle`.
  - Corner radius: `CornerRadius` or per-corner overrides.
  - Padding: `Padding`, `PaddingTop/Right/Bottom/Left`.
  - Background: `BackgroundColor`, `BackgroundOpacity`.
  - Rotation: `RotationDegrees`.
  - Text overflow: `Wrap`, `Hyphenate`, `EllipsisWhenClipped`.
  - Rich content: `TextRuns` (`InlineRun`) to mix styles; `FallbackFonts` for glyph coverage.
  - Hyperlinks: `Hyperlink`, `ToolTip`.

BorderStyle (`Elements/Table/BorderStyle.cs`)
---------------------------------------------
- `Color`, `Width`.
- `DashPattern` (array, e.g., `{3f, 2f}`), `DashPhase`.
- `LineJoin`: `Miter`, `Round`, `Bevel`.
- `LineCap`: `Butt`, `Round`, `Square`.
- `MiterLimit`: optional override.

Banding Specs (`Elements/Table/BandingSpec.cs`)
----------------------------------------------
- `RowBandingSpec` and `ColumnBandingSpec` define repeating fills:
  - `Step`: number of rows/columns per pattern cycle.
  - `Fills`: list of `BandFill` items (`FillColor`, optional `BorderOverride`).
  - `BorderOverride`: apply a custom `BorderStyle` when the band is active.
- Combine row and column banding for checkerboard patterns.

TextStyle (`Elements/Table/TextStyle.cs`)
----------------------------------------
Per-cell styling object exposed via `TableCellBuilder.TextStyle`:
- Font options, kerning, spacing, and decoration details.
- Wrap behavior via `TextWrapMode`.
- Flow direction and hyperlink metadata.
- Rotation degrees for vertical headers.

InlineRun (`Elements/Table/TextStyle.cs`)
----------------------------------------
Used for `TableCell.TextRuns` to mix fonts, colors, or scripts within a single cell. Each run holds a `TextStyle` clone plus optional fallback fonts.

Example: Alternating stripes with dashed borders
------------------------------------------------
```csharp
var stripe = new BorderStyle
{
    Color = Color.FromArgb(0x9CA3AF),
    Width = 0.75f,
    DashPattern = new[] { 2f, 2f },
    LineJoin = BorderLineJoin.Round
};

table
    .OuterBorder(new BorderStyle { Color = Color.Black, Width = 1.2f })
    .InnerBorder(new BorderStyle { Color = Color.FromArgb(0xD1D5DB), Width = 0.5f })
    .RowBanding(new RowBandingSpec
    {
        Step = 2,
        Fills = new List<BandFill>
        {
            new BandFill { FillColor = Color.White },
            new BandFill { FillColor = Color.FromArgb(0xF3F4F6), BorderOverride = stripe }
        }
    })
    .HeaderRow(c => c.Text("Task").Bold().TextStyle(style =>
    {
        style.Wrap = TextWrapMode.NoWrap;
        style.BackgroundColor = Color.FromArgb(0x1F2933);
        style.TextColor = Color.White;
    }));
```

Expected Outcome
----------------
- Outer frame in solid black with 1.2pt stroke.
- Inner grid lines in light gray.
- Even-numbered rows use a soft gray fill and dashed horizontal borders.
- Header row appears dark with white text and no wrapping.
