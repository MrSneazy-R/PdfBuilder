Images, Canvas & Svg
====================

Images
------
`ContentComposer.Image(ImageSource source, ...)` adds PNG, JPEG, or still WebP artwork from reusable bytes, memory, streams, files, resources, or lazy factories. The byte-array overload and coordinate-based `ColumnBuilder.Image` remain compatibility conveniences.

API Highlights:
- Set `width`/`height` to control the rendered size.
- Use the optional `configure` callback to adjust margins, padding, borders, corner radius, rotation, opacity, hyperlinks, or clipping (`Clip(ImageClipShape)`).
- All image positioning is handled by the flow; no manual coordinates required unless you use `ColumnBuilder.Image` directly.

Example:
```csharp
var signature = File.ReadAllBytes("assets/signature.png");

page.Compose(flow =>
{
    flow.Image(signature, width: 140, height: 48, img =>
    {
        img.MarginTop(12);
        img.Hyperlink("https://contoso.example");
        img.CornerRadius(8);
    });
});
```

Expected Outcome:
The signature graphic appears inline with the surrounding content, respecting margins set by the callback. Pagination/columns handle the image just like text or tables.

CanvasBuilder
-------------
Use `ContentComposer.Canvas(width, height, drawAction, configure?)` or `ColumnBuilder.Canvas` to issue raw PDF drawing commands via `CanvasBuilder`. Typical usage:

```csharp
page.Compose(flow =>
{
    flow.Canvas(120, 80, canvas =>
    {
        canvas.Margin(4)
              .StrokeColor("#1F2933")
              .Line(0, 0, 120, 80, width: 1.5f)
              .Rect(10, 10, 100, 60, stroke: true, fill: false);
    });
});
```

API Highlights:
- `Margin(float all)` / `Margin(left, top, right, bottom)`: padding inside the canvas bounds.
- `AvoidBreakInside(bool)`: prevent pagination from splitting the canvas.
- `Raw(string command)`: append literal PDF operators (useful for complex sequences).
- Path helpers: `MoveTo`, `LineTo`, `ClosePath`, `Stroke`, `Fill`, `Rect`.
- Color helpers: `StrokeColor(string hex)`, `FillColor(string hex)`.
- Convenience drawing: `Line(x1, y1, x2, y2, width, color?)`.

Expected Outcome:
A 120x80pt canvas containing a diagonal line and rectangle is rendered inline within the flow, honoring any margins you set.

SvgElement
----------
`ContentComposer.Svg(width, height, Action<SvgElement>)` inserts vector graphics using SkiaSharp SVG parsing.

Key properties on `SvgElement`:
- `Source`: provide raw SVG markup (string).
- `SourcePath`: load from file.
- `FillColor`, `StrokeColor`, `StrokeWidth`, `Opacity`.
- `BackgroundColor`: optional solid fill.
- `ScaleToFit` and `PreserveAspectRatio`: maintain proportions.
- `FallbackText`: message when the SVG fails to load.

Example:
```csharp
flow.Svg(200, 120, svg =>
{
    svg.Source = File.ReadAllText("assets/logo.svg");
    svg.ScaleToFit = true;
    svg.BackgroundColor = "#FFFFFF";
});
```

Expected Outcome:
The SVG is rendered within a 200x120pt viewport, scaled to fit while preserving aspect ratio. If the source cannot be parsed, `FallbackText` is displayed instead.
