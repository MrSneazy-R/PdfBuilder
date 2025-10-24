FontCatalog
===========

Purpose
-------
`FontCatalog` registers custom fonts for use by text, tables, charts, and other text-bearing elements. PdfBuilder uses SkiaSharp typefaces and HarfBuzz shaping, so every registered face becomes available via its family name.

Registration Options
--------------------
- `RegisterFile(string path, params string[] aliases)`: load a font file and optionally expose it under friendly names.
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
