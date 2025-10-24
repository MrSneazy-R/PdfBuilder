PdfPageBuilder
==============

Purpose
-------
`PdfPageBuilder` is the lower-level API for configuring a `PdfPage` outside of the declarative composer. It is used extensively in tests and is available if you prefer imperative control over content.

Construction
------------
```csharp
var doc = new PdfDocument();
var page = doc.AddPage();
var builder = new PdfPageBuilder(page, doc);
```

Key Methods
-----------
- `Margin(float points)`: sets a uniform margin used by the internal `ColumnBuilder`.
- `AutoPaginate(PdfDocument doc)`: enable automatic page creation when the column overflows. The new pages inherit size, background, margins, and text defaults.
- `Background(string color)`: sets `PdfPage.BackgroundColor`.
- `Content(Action<ColumnBuilder>)`: execute a column action to place elements. You can call this once per page; inside the callback you have the full `ColumnBuilder` surface (tables, text, images, etc.).
- `Build()`: returns the underlying `PdfPage`.

Auto Pagination Notes
---------------------
- When `AutoPaginate` is set, the builder injects a factory that clones the current page when overflow occurs, appends it to the document, and continues the flow automatically.
- Headers, footers, and masters applied via `PdfDocumentBuilder` sections carry over to new pages.

Example
-------
```csharp
var doc = new PdfDocument();
var page = doc.AddPage();

new PdfPageBuilder(page, doc)
    .Margin(40)
    .AutoPaginate(doc)
    .Background("#FFFFFF")
    .Content(col =>
    {
        for (int i = 0; i < 30; i++)
        {
            col.Text($"Line {i + 1}", page.MarginLeft, col.GetCurrentY(), page.Width - page.MarginLeft - page.MarginRight)
               .Add();
        }
    });
```

Expected Outcome
----------------
- Content flows down the first page; when it reaches the bottom margin, new pages are cloned automatically.
- Each new page retains the 40pt margin and white background.
- The resulting document contains enough pages to host 30 lines of text without clipping.
