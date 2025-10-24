TextBuilder
===========

Purpose
-------
`TextBuilder` configures a `TextElement` positioned within a column flow. Use it for single-style paragraphs with optional inline spans for localized overrides.

Usage
-----
Acquire via `ColumnBuilder.Text(content, x, y, defaultWidth)` or `ColumnBuilder.AddText` helpers. Call `Add()` to append the element to the column and advance the flow cursor.

Styling API
-----------
- `FontFamily(string)`, `FontSize(float)`, `Bold()`, `Italic()`, `Underline()`, `Strikethrough()`, `Overline()`, `SmallCaps()`, `Monospace()`
- `Color(string hex)`, `Opacity(float)`, `BackgroundColor(string)`
- Decoration settings: `DecorationColor(string)`, `DecorationThickness(float)`, `DecorationStyle(TextDecorationStyle)`
- Spacing: `LetterSpacing(float)`, `WordSpacing(float)`, `LineHeight(float)`
- Transformations: `Transform(TextTransform)`, `FlowDirection(FlowDirection)`, `Rotation(float degrees)`, `BaselineOffset(float)`
- Margins: `MarginTop`, `MarginBottom`, `MarginLeft`, `MarginRight`
- Padding: `PaddingTop`, `PaddingBottom`, `PaddingLeft`, `PaddingRight`
- Background box enhancements: `BackgroundBorderColor`, `BackgroundBorderWidth`, `BackgroundCornerRadius` (plus individual corners), `BackgroundShadowOffsetX/Y`, `BackgroundShadowBlur`, `BackgroundShadowColor`
- Layout: `MaxWidth(float)`, `Alignment(TextAlignment)`
- Flow control: `KeepWithNext(bool)`, `AvoidBreakInside(bool, default true)`, `WidowLines`, `OrphanLines`
- Spans: `Span(string text, Action<TextSpan>?)` to mix inline font or color changes; `ClearSpans()` resets the list.

Fallback Fonts
--------------
Set `TextBuilder.Span(...).FallbackFonts` to specify additional families when shaping glyphs; defaults are inherited from `TextStyleDefaults`.

Example
-------
```csharp
page.Column(col =>
{
    col.Text("Customer Satisfaction Update", page.MarginLeft, col.GetCurrentY(), page.ContentWidth)
       .FontSize(18)
       .Bold()
       .Color("#1F2933")
       .KeepWithNext(true)
       .Add();

    col.Text(string.Empty, page.MarginLeft, col.GetCurrentY(), page.ContentWidth)
       .LineHeight(1.4f)
       .Span("This quarter's NPS improved to ")
       .Span("62", span => span.Bold().Color("#10B981"))
       .Span(", driven by faster onboarding and better support coverage.")
       .Add();
});
```

Expected Outcome
----------------
- Heading text renders in bold, 18pt dark blue, and remains glued to the following paragraph thanks to `KeepWithNext`.
- Body paragraph uses the document default font size unless overridden by `LineHeight`; the inline span "62" appears bold and green.
- HarfBuzz shaping ensures ligatures, diacritics, and complex scripts render correctly for all spans.
