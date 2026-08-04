# Typography hardening

## Text path

Canonical and legacy text both enter `TextElementLayouter` or `RichTextLayouter`, then `TextShaper`. The shaped glyphs are retained by the element and consumed by `TextRenderer`/`RichTextRenderer`; rendering does not re-measure text. Glyph registration assigns CIDs, `GlyphRunEncoder` emits PDF text commands, and `FontResourceWriter` writes CID widths, the CID-to-GID map, and a ToUnicode map. HarfBuzz subsetting is attempted for embedded fonts; its explicit native fallback is a full-font embed with a diagnostic.

## Determinism and fallback

Use `FontCatalog.RegisterFont`, `RegisterFontFile`, or `RegisterFontDirectory` before generation, followed by `SetFallbackFonts` for an ordered fallback chain. The typeface cache includes the catalog version, so a registration change cannot reuse a prior resolution. In strict mode missing font families and glyphs throw `FontNotFoundException`; otherwise each platform fallback is reported through `FontDiagnostics`.

## Bloat guardrail

`SimpleEmbeddedText_DoesNotCreateBloatedPdf` protects a normal line containing one non-ASCII glyph. It caps the decoded content stream at 2,000 characters, permits at most two text objects, and caps the generated PDF at 1.5 MB. This is intentionally a structural guardrail rather than a compression-only metric: a change that expands PDF commands will fail even when a stream compresses well.

The remaining risk is native HarfBuzz subsetting availability. When a compatible native subset API is not available, the library preserves valid output by embedding the full font; this can exceed the typical size of a subset and emits a font diagnostic for investigation.
