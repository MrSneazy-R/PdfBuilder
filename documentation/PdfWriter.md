PdfWriter
=========

Purpose
-------
`PdfWriter` converts a fully composed `PdfDocument` into bytes, a file on disk, or preview images. It resolves fonts (base-14 and embedded), encodes page content, writes annotations/outlines, and finalizes cross-reference tables.

API Surface
-----------
- `byte[] GenerateBytes(PdfDocument doc)`: render the document into an in-memory byte array.
- `void GenerateStream(PdfDocument doc, Stream destination)`: write directly to an existing stream (e.g., ASP.NET response).
- `void Save(PdfDocument doc, string path)`: convenience wrapper that writes to disk.
- `IReadOnlyList<PdfPreviewPage> GeneratePreviewImages(PdfDocument doc, int dpi = 144)`: rasterize each page (SkiaSharp-based) for quick previews.

Expectations & Preconditions
----------------------------
- The document must contain at least one page; otherwise an `InvalidOperationException` is thrown.
- Table pagination is executed automatically (`TablePaginator.Paginate`) prior to rendering, so tables that overflow pages are fully resolved.
- Fonts collected from text runs are registered before encoding. Embedded fonts are included when required, while base-14 fonts use WinAnsi encoding for compatibility.
- Outlines and link annotations are emitted when anchors or rich text links are present.

Example
-------
```csharp
var pdf = BuildReport(); // returns a populated PdfDocument
var writer = new PdfWriter();

byte[] bytes = writer.GenerateBytes(pdf);
File.WriteAllBytes("report.pdf", bytes);

var previews = writer.GeneratePreviewImages(pdf, dpi: 96);
foreach (var page in previews)
{
    File.WriteAllBytes($"preview-{page.PageNumber}.png", page.ImageBytes);
}
```

Expected Outcome
----------------
- `report.pdf` is written with compressed streams (respecting `PdfOutputOptions`) and fully populated metadata, outlines, and links.
- Preview PNG files show each page at 96 DPI, useful for thumbnail galleries in dashboards or tests.
