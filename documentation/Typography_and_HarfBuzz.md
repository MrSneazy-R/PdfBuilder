# Typography and HarfBuzz

PdfBuilder has one canonical typography surface. `ITextStyleDescriptor` is used by ordinary text, rich-text paragraph defaults and spans, page defaults, theme styles, table-cell text, headers and footers, and the practical subset supported by chart labels. Internally, `TextStyleDefaults` is the shared style state; adapters copy it into legacy element models without creating another shaping path.

## Canonical style properties

The canonical descriptor supports font family and size, bold, italic, text and highlight colours, line height, letter and word spacing, underline, strikethrough, overline, decoration colour/thickness/style, superscript, subscript, left/centre/right/justify alignment, automatic/LTR/RTL direction, wrap/no-wrap/hyphenation, ellipsis, maximum lines, and an ordered fallback-font chain.

Named theme styles are inherited first and direct overrides are applied afterward. Named styles contain only explicitly configured values, so a style such as `Bold()` does not reset the inherited family, size, or colour.

```csharp
var document = PdfDocument.Create(document =>
{
    document.Theme(theme => theme.TextStyle("Body", style => style
        .FontFamily("Inter")
        .FontSize(11)
        .LineHeight(1.35f)
        .FallbackFonts("Noto Sans Arabic", "Noto Sans Hebrew", "Noto Sans CJK SC")));

    document.Page(page => page.Content().RichText(paragraph =>
    {
        paragraph.DefaultStyle().Style("Body");
        paragraph.Span("Invoice ").Bold();
        paragraph.Span("مرحبا").Direction(TextDirection.RightToLeft).Underline();
    }));
});
```

Rich text participates in normal flow measurement and page splitting. Each span retains its inherited style, fallback chain, decoration, and baseline shift through measurement and rendering. Links remain supported by the legacy rich-run model; the canonical link surface is intentionally deferred to Roadmap PR 24.

## Shaping and output guarantees

All ordinary text, rich spans, and table runs continue through `TextShaper`, SkiaSharp.HarfBuzz, `GlyphRunEncoder`, CID-to-GID mapping, ToUnicode generation, font subsetting, and resource deduplication. Font catalogue versions are part of typeface cache keys. A document captures an immutable font snapshot before canonical composition so concurrent registration cannot change its family or fallback choices midway through generation.

Automatic direction chooses the first strong script. Explicit LTR and RTL remain available. Logical Unicode stays in ToUnicode mappings so extracted text is independent of visual glyph order.

Full-font embedding after a subset failure is never silent: every fallback is retained in `FontDiagnostics.RecentMessages`, even when trace output and a custom diagnostic writer are disabled.

No font binaries are stored in the repository. Multilingual tests use fonts installed by the CI environment.
