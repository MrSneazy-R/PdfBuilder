ContentComposer
===============

Purpose
-------
`ContentComposer` wraps `LayoutComponentCollection` and provides a fluent DSL for declarative layouts inside `PageComposer.Compose`, `PageComposer.Content`, or `ColumnBuilder.Content`.

Layout Primitives
-----------------
- `Text(string, Action<TextElement>?)`: adds a paragraph. Configure font size, weight, alignment, etc., within the callback.
- `List(Action<ListElement>)`: bullet or numbered lists with nested children.
- `Column(Action<ColumnComponentBuilder>)`: vertical stack with customizable spacing.
- `Column(Action<LayoutComponentCollection>, float spacing)`: convenience overload for inline declaration.
- `Row(Action<RowComponentBuilder>)`: horizontal layout with fixed, auto, or relative widths.
- `Row(Action<LayoutComponentCollection>, float gap)`: inline row declaration.
- `Stack(Action<StackComponentBuilder>)`: overlay items without altering flow height until children measure.
- `Grid(Action<GridComponentBuilder>)` or `Grid(int columns, Action<LayoutComponentCollection>, float rowGap, float columnGap)`: grid layout with equal column distribution.
- `Padding(...)`: wrap content with padding.
- `Align(horizontal, vertical, Action<ContentComposer>, float? minHeight)`: align inner content inside the reserved box.
- `Absolute(float offsetX, float offsetY, Action<ContentComposer>)`: place content relative to the current flow position without affecting layout height.
- `Layer(Action<LayerBuilder>)`: separate background/content/foreground regions.
- `Decorate(Action<DecorationBuilder>, Action<ContentComposer>)`: inject drawing callbacks before/after child rendering.
- `Border(Action<BorderOptions>?, Action<ContentComposer>)`: draw a border rectangle around content.
- `Background(string color, Action<ContentComposer>, float opacity)`: paint a solid background before rendering children.
- `Size(...)`, `MinHeight`, `MaxHeight`, `Height`, `MinWidth`, `MaxWidth`, `Width`, `AspectRatio`, `Extend`, `ExtendHeight`, `ExtendWidth`, `Shrink`, `ShrinkHeight`, `ShrinkWidth`: impose sizing constraints or stretching rules.
- `Image(ImageSource source, float width, float height, Action<ImageElement>?)`: place reusable raster imagery in an explicit box.
- `Image(ImageSource source, Action<ImageElement>?)`: place raster imagery at its DPI-aware intrinsic size.
- `Image(byte[] data, float width, float height, Action<ImageElement>?)`: compatibility convenience that snapshots the bytes into an `ImageSource`.
- `Canvas(float width, float height, Action<CanvasBuilder>, Action<CanvasElement>?)`: custom drawing using PDF path commands.
- `Barcode(...)`: insert QR, Code128, and other barcode types.
- `Svg(float width, float height, Action<SvgElement>)`: embed parsed SVG content.

Control Flow Helpers
--------------------
- `ShowOnce(string key, Action<ContentComposer>)`: render the block only on the first encounter with the given key per document.
- `When(bool condition, Action<ContentComposer> whenTrue, Action<ContentComposer>? whenFalse)`: conditional rendering.
- `Repeat<T>(IEnumerable<T>, Action<T,int,ContentComposer>)` / `Repeat(int count, Action<int, ContentComposer>)`: loops with index awareness.
- `Dynamic<T>(IEnumerable<T>, Action<T, LayoutComponentCollection>)`: low-level variant that lets you add arbitrary components.
- `DefaultTextStyle(Action<TextStyleDefaults>)`: temporarily override typography defaults for downstream elements.

Example
-------
```csharp
page.Compose(flow =>
{
    // Assume logoBytes contains the raw PNG/JPEG data for your logo.
    flow.Image(logoBytes, 120, 40, img =>
        img.MarginBottom(12).Hyperlink("https://contoso.example"));

    flow.Background("#1F2933", inner =>
    {
        inner.Padding(24, padded =>
        {
            padded.Text("Weekly Digest")
                  .FontSize(20)
                  .Color("#FFFFFF")
                  .Bold();
        });
    }, opacity: 1f);

    flow.Row(row =>
    {
        row.Relative(2, left =>
        {
            left.Column(col =>
            {
                col.Spacing(8);
                col.Item(inner => inner.Text("Highlights").FontSize(16).Bold());
                col.Item(inner => inner.Text("• Revenue up 12%\n• NPS improved by 4 points"));
            });
        });
        row.Relative(1, right =>
        {
            right.Block(comp =>
            {
                comp.Padding(12, chip =>
                {
                    chip.Align(LayoutHorizontalAlignment.Center, LayoutVerticalAlignment.Middle,
                        centered => centered.Text("92%").FontSize(24).Bold());
                });
            });
        });
    });

    flow.Barcode("https://contoso.example/report", BarcodeKind.QrCode, moduleSize: 3)
        .Configure(code => code.Caption = "Scan for details");
});
```
*(Note: `Block` in the sample represents `ColumnComponentBuilder.Item` returning a nested composer; you can inline the `Padding` call there.)*

Expected Outcome
----------------
- A dark header band with white "Weekly Digest" text.
- Below the band, a two-column row: the left column contains a headline and bullet points; the right column shows a centered metric badge.
- A QR code labeled "Scan for details" is rendered underneath the row, sized according to the `moduleSize`, ready to open the linked report when scanned.
