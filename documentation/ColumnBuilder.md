ColumnBuilder
=============

Purpose
-------
`ColumnBuilder` manages flow layout inside a page column. It tracks Y position, handles column/page breaks, and exposes imperative builders for tables, text, images, lists, anchors, and rich text.

Creation
--------
You normally obtain a `ColumnBuilder` via `PageComposer.Column(...)` or inside `PageComposer.Content` when you need direct control. Columns automatically respect page margins and header/footer reservations.

Key Methods
-----------
- `Text(string content, float x, float y, float defaultWidth)` -> `TextBuilder`: create a text paragraph at arbitrary coordinates.
- `RichText(float x, float y, float defaultWidth)` -> `RichTextBuilder`: multi-span text builder with inline styling.
- `Image(byte[] data, float x, float y, float width, float height)` -> `ImageBuilder`: place raster images.
- `Table(float x, float y, float width, float estimatedRowHeight)` -> `TableBuilder`: build complex tables.
- `List(float x, float y, float defaultWidth)` -> `ListBuilder`: construct rich nested lists.
- `Canvas(float x, float y, float width, float height)` -> `CanvasBuilder`: issue low-level drawing commands.
- `Svg(float x, float y, float width, float height)` -> `SvgElement`: add vector graphics.
- `Anchor(string id)` -> `AnchorBuilder`: create link destinations.
- `ColumnBreak()` / `PageBreak()`: force flow into the next column or page.
- `TableOfContents(Action<TableOfContentsOptions>?)`: insert a dynamic TOC placeholder that populates during pagination.
- `GetCurrentY()`: current baseline position, useful when positioning subsequent elements.
- `DefaultTextStyle(Action<TextStyleDefaults>)`: override type defaults for this column.
- `ApplyTextDefaults/ApplyRichTextDefaults/ApplyRunDefaults`: used by element builders; exposed for custom components.

Flow Helpers
------------
- Automatic pagination is triggered when `EnsureSpace` detects overflow. Provide meaningful heights when adding custom components to keep flow accurate.
- `AddComponent(IMeasurable)` allows plugging in custom measure/draw components.

Example
-------
```csharp
page.Column(col =>
{
    float width = page.Width - page.MarginLeft - page.MarginRight;
    col.Text("Manual positioning").FontSize(14).Bold().Add();

    col.Table(page.MarginLeft, col.GetCurrentY(), width, 0)
       .Caption("Inventory")
       .HeaderRow(
           c => c.Text("Item"),
           c => c.Text("Qty").AlignRight())
       .Row(
           c => c.Text("Coffee Beans"),
           c => c.Text("42").AlignRight())
       .Add();

    col.Image(File.ReadAllBytes("logo.png"), page.MarginLeft, col.GetCurrentY() - 20, 96, 32)
       .Hyperlink("https://example.com")
       .Add();
});
```

Expected Outcome
----------------
- A two-column table titled "Inventory" follows the bold heading.
- The quantity column is right-aligned, and the table respects column width.
- The image appears below the table at the current flow position and links to the provided URL when clicked.
