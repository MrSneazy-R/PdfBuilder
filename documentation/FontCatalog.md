FontCatalog
===========

Purpose
-------
`FontCatalog` registers custom fonts for use by text, tables, charts, and other text-bearing elements. PdfBuilder uses SkiaSharp typefaces and HarfBuzz shaping, so every registered face becomes available via its family name.

## Deterministic registration and fallback

Register the exact font data used by an application when reproducible output matters. Registration is process-wide; configure it at application start-up and do not mutate it while documents are being generated.

```csharp
using PdfBuilder.Fonts;

FontCatalog.RegisterFontFile("assets/Inter-Regular.ttf", "Invoice Sans");
FontCatalog.RegisterFont(File.ReadAllBytes("assets/NotoSansArabic-Regular.ttf"), "Invoice Arabic");
FontCatalog.SetFallbackFonts("Invoice Arabic");
FontCatalog.StrictMatching = true;
```

`StrictMatching` throws `FontNotFoundException` if a requested family or glyph cannot be resolved. In the default non-strict mode PdfBuilder uses the explicit fallback chain first, then the platform fallback and emits a `FontDiagnostics` message for that substitution. The PDF base-14 families remain available without registration.

Registration Options
--------------------
- `RegisterFont(byte[] data, params string[] aliases)` and `RegisterFont(Stream, params string[] aliases)`: load stable in-memory font data.
- `RegisterFontFile(string path, params string[] aliases)`: load a font file and optionally expose it under friendly names.
- `RegisterFontDirectory(string path, SearchOption)`: load a directory of font files.
- `RegisterFile(string path, params string[] aliases)`: legacy spelling retained for compatibility.
- `RegisterFolder(string directory, SearchOption option = AllDirectories)`: scan a directory for `.ttf`, `.otf`, `.ttc`, `.otc`.
- `RegisterSystemFonts()`: convenience API that adds standard OS font directories.
- `RegisterTypeface(SKTypeface typeface, params string[] aliases)`: plug in an already-loaded typeface (useful for memory streams).

Diagnostics
-----------
Enable diagnostics via environment variable `PDFBUILDER_FONT_DIAGNOSTICS=1` before constructing `PdfDocumentBuilder`. Failed registrations are reported through `FontDiagnostics`.

Example
-------
```csharp
FontCatalog.RegisterFolder(@"C:\Fonts\Corporate", SearchOption.AllDirectories);
FontCatalog.RegisterFile("assets/Inter-Regular.ttf", "Inter");

var pdf = new PdfDocument();
new PdfDocumentBuilder(pdf)
    .DefaultTextStyle(defaults =>
    {
        defaults.FontFamily = "Inter";
    })
    .Compose(doc => doc.Page(page => page.Content(flow =>
        flow.Text("Hello Inter!").FontSize(18))));
```

Expected Outcome
----------------
- Inter becomes the default font for the document.
- Paragraphs render with proper shaping (ligatures, weights, italic) using the registered face.
- If a font fails to load, diagnostics output is emitted when `PDFBUILDER_FONT_DIAGNOSTICS` is enabled.
