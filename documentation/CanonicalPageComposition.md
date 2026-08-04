# Canonical page composition

`IPageDescriptor` exposes `Header()`, `Content()`, `Footer()`, `Background()`, and `Columns()`. Each returns the same `IContainer` model used by normal document content, so rows, columns, borders, backgrounds, layout primitives, and reusable components compose consistently.

Headers and footers use the existing measure/draw pipeline. Their configured bands are reserved before the content flow begins and are copied to automatic continuation pages. The existing `HeaderFooterSpec` string templates, including `{page}` and `{pages}`, remain available for compatibility.

```csharp
document.Page(page =>
{
    page.Margin(40, 50, 40, 45);
    page.Columns(2, gutter: 12);
    page.Header().BorderBottom().Padding(4).Text("Operations report");
    page.Footer().AlignRight().Text("Page footer");
    page.Background().Background("#FFFFFF");
    page.Content().Column(column => column.Item().Text("Body"));
});
```

Existing master-page, watermark, anchor, bookmark, external-link, and section APIs remain the compatibility path while their canonical counterparts are expanded in follow-up work. Empty pages are retained when their background, header, or footer produces output.
