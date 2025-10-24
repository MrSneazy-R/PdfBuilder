RichTextBuilder
===============

Purpose
-------
`RichTextBuilder` is the high-level API for composing multi-span paragraphs with inline styling, links, and bidi support. It maps to `RichTextElement` which shapes text with HarfBuzz.

Obtaining a Builder
-------------------
Call `ColumnBuilder.RichText(x, y, defaultWidth)` to start. The builder preloads defaults from the current `TextStyleDefaults`.

Core Settings
-------------
- `Font(string family, float size)`: paragraph-level defaults.
- `LineHeight(float)`, `Align(TextAlignment)`, `MaxWidth(float)`.
- `MarginTop(float)`, `MarginBottom(float)`.
- `FlowDirection(FlowDirection)`: left-to-right or right-to-left paragraph flow.

SpanBuilder
-----------
`RichTextBuilder.Span(string text)` creates a new `RichRun` and returns a `SpanBuilder` with the following methods:
- `Bold()`, `Italic()`, `Underline()`, `Strike()`, `SmallCaps()`.
- `Size(float)`, `Color(string)`.
- `LinkUrl(string)` / `LinkAnchor(string)`: generate external hyperlinks or jump to anchors registered via `ColumnBuilder.Anchor`.
- `EndSpan()`: return to the parent builder (optional when fluent chaining).

Fallback fonts, letter spacing, and word spacing inherit from defaults unless overridden in the run.

Finalizing
----------
`Add()` pushes the resulting `RichTextElement` to the column and advances the flow height based on shaped content.

Example
-------
```csharp
col.RichText(page.MarginLeft, col.GetCurrentY(), page.ContentWidth)
   .LineHeight(1.5f)
   .Align(TextAlignment.Justified)
   .Span("To learn more about the release notes, visit ")
       .LinkUrl("https://contoso.example/releases")
       .Color("#2563EB")
       .Underline()
       .EndSpan()
   .Span(". Our latest feature, ")
       .EndSpan()
   .Span("Smart Tables")
       .Bold()
       .Color("#111827")
       .EndSpan()
   .Span(", now supports rich banding and per-side borders.")
       .EndSpan()
   .Add();
```

Expected Outcome
----------------
- A justified paragraph with 1.5 line height.
- The phrase "https://contoso.example/releases" (rendered as link text) appears blue and underlined, opening the browser when clicked.
- "Smart Tables" is bold, drawing emphasis.
- Complex scripts or mixing RTL/LTR text remain correctly ordered thanks to HarfBuzz shaping and Unicode bidi handling.
