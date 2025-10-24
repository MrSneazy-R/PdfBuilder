ListBuilder
===========

Purpose
-------
`ListBuilder` constructs multi-level ordered or unordered lists with rich inline formatting. It wraps `ListElement` and is available from `ColumnBuilder.List`.

Configuration
-------------
- `Marker(ListMarker)`: choose bullet, decimal, alpha, or roman numerals.
- `Font(string family, float size)`: set base typography for list items.
- `Colors(string hex)`: font color.
- `Indent(float points)`: indentation per nesting level.
- `ItemSpacing(float points)`: vertical spacing between items.
- `LineHeight(float multiplier)`: line spacing within items.

Adding Items
------------
- `Item(params RichRun[] runs)`: create an item from one or more rich runs. Use `RichRun` helpers to style text (bold, italic, links, etc.).
- `BeginNest()` / `EndNest()`: manage nested list levels manually. Nested items inherit indentation and marker style.

Finalization
------------
- `Add()`: emits the rendered list via the owning `ColumnBuilder` and advances the flow cursor.

Example
-------
```csharp
var list = new ListBuilder(col, page.MarginLeft, col.GetCurrentY(), page.ContentWidth)
    .Marker(ListMarker.Decimal)
    .Font("Helvetica", 11)
    .Indent(12)
    .ItemSpacing(6);

list.Item(new RichRun { Text = "Review backlog", Bold = true });
list.Item(new RichRun { Text = "Ship Smart Tables beta", Underline = true });
list.BeginNest();
list.Item(new RichRun { Text = "Enable banding presets" });
list.Item(new RichRun
{
    Text = "Document per-side borders",
    LinkAnchor = "tables-docs",
    Underline = true
});
list.EndNest();
list.Item(new RichRun { Text = "Collect customer feedback" });

list.Add();
```

Expected Outcome
----------------
- A numbered list with decimal markers.
- Nested items (two middle tasks) are indented and continue numbering in the nested scope.
- The "Document per-side borders" entry becomes an internal hyperlink targeting anchor `tables-docs`.
- Item spacing ensures readable gaps between entries.
