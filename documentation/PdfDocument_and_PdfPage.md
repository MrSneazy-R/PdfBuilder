PdfDocument & PdfPage
=====================

PdfDocument
-----------
- `Pages`: list of `PdfPage` instances; use `AddPage(width?, height?)` to append new pages. Defaults to US Letter (612x792 pt).
- `LayoutOptions`: shared `LayoutOptions` cloned into each new page.
- `OutputOptions`: default `PdfOutputOptions`.
- `Metadata`: global `DocumentMetadata`.
- `TextDefaults`: base `TextStyleDefaults`.
- `Pagination`: `PaginationRegistry` storing sections, anchors, and TOC references.
- `ProfilerSession`: shared session for the layout profiler.
- `Title`: optional document title used in headers and metadata.
- `HeaderFooter`: default `HeaderFooterSpec`.
- `Master`: default `MasterPageSpec`.

Direct usage:
```csharp
var doc = new PdfDocument
{
    Title = "Invoice #1042"
};

doc.Metadata.Author = "Contoso Billing";
doc.OutputOptions.CompressContentStreams = true;

var page = doc.AddPage(PdfPage.DefaultWidth, PdfPage.DefaultHeight);
```

PdfPage
-------
- Dimensions: `Width`, `Height`; factory methods for common sizes (`PdfPage.A4()`, `.LetterLandscape()`, etc.).
- Margins: `MarginTop`, `MarginBottom`, `MarginLeft`, `MarginRight`.
- Styling: `BackgroundColor`.
- Layout: `LayoutOptions` (inherited but customizable per page), `TextDefaults`.
- Overrides: `HeaderFooterOverride`, `MasterOverride`.
- `Columns`: optional `ColumnLayoutSpec` for multi-column layouts.
- Content: `Elements` (rendered body elements). Header/footer elements are stored internally once composed.

Manual element insertion:
```csharp
var page = doc.AddPage();
page.MarginLeft = 48;
page.MarginRight = 48;
page.Columns = new ColumnLayoutSpec { Columns = 2, Gutter = 18 };

page.AddElement(new TextElement("Direct element", page.MarginLeft, page.Height - page.MarginTop)
{
    FontSize = 14,
    Bold = true
});
```

Expected Outcome
----------------
- Document-level settings (metadata, output options, defaults) propagate to pages automatically.
- Manually added pages inherit the current defaults, but you can override margins, layout options, or masters on a per-page basis.
- Directly added elements (outside the column builder) render at the specified coordinates, enabling low-level control when needed.
