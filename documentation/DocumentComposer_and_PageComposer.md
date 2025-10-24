DocumentComposer & PageComposer
===============================

DocumentComposer
----------------
`DocumentComposer` is the callback surface you receive inside `PdfDocumentBuilder.Compose`. It creates pages with optional custom dimensions.

Key members:
- `Page(Action<PageComposer> configure)`: adds a page using default width/height.
- `Page(float width, float height, Action<PageComposer> configure)`: adds a page with custom size.

Every call to `Page`:
1. Adds a new `PdfPage` to the document.
2. Applies the current section headers, footers, master, and text defaults.
3. Invokes the provided `PageComposer`, then finalizes pending content.

PageComposer
------------
`PageComposer` configures margins, backgrounds, pagination behavior, and delegates to column or content builders.

Primary methods:
- `Margin(float)` / `Margins(float left, float top, float right, float bottom)`: set page margins.
- `DefaultTextStyle(Action<TextStyleDefaults>)`: override defaults for this page only.
- `Background(string hexColor)`: paints the background within page bounds.
- `AutoPaginate(bool enabled = true)`: enables automatic creation of additional pages when the column overflows.
- `Compose(Action<LayoutComponentCollection>)`: declarative content flow using the layout DSL.
- `Content(Action<ContentComposer>)`: direct access to the DSL without intermediate column builders.
- `Column(Action<ColumnBuilder>)`: imperative column builder for finer control (tables, anchors, lists).
- `HeaderText`, `FooterText`: quick string templates.
- `Header` / `Footer`: declarative header/footer layout using `ContentComposer`.
- `HeaderFooter(Action<HeaderFooterSpec>)`: mutate header/footer copy for this page only.
- `PageNumbering(PageNumberPlacement placement, string template)`: inject page numbering tokens.

Example
-------
```csharp
builder.Compose(doc =>
{
    doc.Page(page =>
    {
        page
            .Margin(36)
            .AutoPaginate()
            .Header(content => content.Text("{title} Report").FontSize(10))
            .Footer(content => content.Align(LayoutHorizontalAlignment.Right, LayoutVerticalAlignment.Middle,
                inner => inner.Text("Page {page}/{pages}")))
            .Compose(flow =>
            {
                flow.Text("This is a declarative block.").FontSize(16).Bold();
                flow.Stack(stack =>
                {
                    stack.Item(inner => inner.Text("Item 1"));
                    stack.Item(inner => inner.Text("Item 2"));
                });
            });
    });
});
```

Expected Outcome
----------------
- A single page with 36-point margins.
- Header shows `{title} Report` centered at the top (because the default alignment is center for the header block).
- Footer text `Page 1/1` appears right-aligned.
- Body content lists the heading text followed by a stacked list of two items. Automatic pagination is enabled, so additional content would spill onto new pages automatically.
