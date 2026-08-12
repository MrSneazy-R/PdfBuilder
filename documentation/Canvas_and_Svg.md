# Images, canvas, and SVG

## Images

The canonical `IContainer.Image` API accepts an `ImageSource`. Sources can snapshot bytes,
read-only memory, streams, local files, embedded resources, shared preloaded content, or a
caller-owned lazy byte factory. `Image(source)` uses the image's DPI-aware intrinsic size;
the explicit-box overload supports contain, cover, stretch, crop alignment, downsampling,
JPEG quality, and alpha-aware encoding. Remote URLs are intentionally outside the core API.

```csharp
var logo = ImageSource.FromFile("assets/logo.png").Preload();
page.Content().Image(logo).MaximumEffectiveDpi(180);
```

## Canonical canvas

Use a fixed canvas when both dimensions are known:

```csharp
page.Content().Canvas(240, 90, canvas =>
{
    canvas.LinearGradient(0, 0, 240, 90, "BrandStart", "BrandEnd", angleDegrees: 20);
    canvas.StrokeColor("Ink").LineWidth(1.5f)
        .LinePattern(CanvasLinePattern.Dashed, dashLength: 5, gapLength: 3)
        .Line(12, 18, 228, 18);
});
```

Use the available-size overload when drawing must adapt to the final layout width:

```csharp
page.Content().Canvas(96, (canvas, available) =>
{
    canvas.RectangleShadow(12, 12, available.Width - 24, 56, "Shadow");
    canvas.FillColor("Surface").Rectangle(12, 12, available.Width - 24, 56, stroke: false, fill: true);
});
```

Canvas coordinates use PDF points with a bottom-left origin. `CanvasSize` contains the final
width and requested height after container and page constraints have been applied.

### Transforms and graphics state

`Transform`, `Translate`, `Rotate`, `Scale`, `FlipHorizontal`, and `FlipVertical` emit PDF
matrix concatenations in the same order as the API calls. For example,
`Translate(...).Rotate(...).Scale(...)` records translation, then rotation, then scaling.
Use `State(...)` to isolate a transform or clipping operation. Direct `Save()` and `Restore()`
are also available, but must balance; an unmatched operation throws `PdfDrawingException`.
Every canvas is additionally wrapped in an outer writer-owned save/restore pair, so canvas
state cannot leak into later document content. All matrix and geometry values must be finite;
scale factors must also be non-zero.

### Paths, clipping, effects, and layers

- Paths: `MoveTo`, `LineTo`, `CurveTo`, `ClosePath`, `Stroke`, `Fill`, and `FillAndStroke`.
- Shapes: `Line`, `Rectangle`, and `Circle`.
- Stroke patterns: solid, dashed, and dotted through `LinePattern`.
- Clipping: `ClipRectangle` applies until the current graphics state is restored.
- Effects: linear gradients, radial gradients, and rectangle shadows use bounded vector
  approximations. They do not introduce raster images or silently allocate unbounded paths.
- Layers: commands assigned to `Background`, `Content`, and `Foreground` are always painted
  in that order, regardless of the order in which the layer callbacks were registered.

`PdfRenderLimits.MaximumCanvasCommands`, `MaximumCanvasCommandBytes`, and
`MaximumCanvasEffectSteps` bound canvas work. Exceeding a limit throws a
`PdfRenderLimitException` naming the relevant limit.

The older `ContentComposer.Canvas` and `CanvasBuilder.Raw` APIs remain compatibility surfaces.
New reusable components should use `IContainer.Canvas`, which does not expose raw PDF commands
or writer types.

## SVG

`IContainer.Svg(markup, width, height)` adds sanitised inline SVG at a fixed size.
`DynamicSvg(height, factory)` supplies the final available `CanvasSize` to a deterministic
markup factory:

```csharp
page.Content().DynamicSvg(48, size =>
    $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {size.Width} {size.Height}'>" +
    $"<rect width='{size.Width}' height='{size.Height}' fill='#336699'/></svg>");
```

Dynamic SVG participates in normal measurement and pagination. The factory is cached for a
stable final size. Empty markup fails clearly, and source bytes are checked against
`MaximumSvgBytes` before rasterisation. The shared SVG sanitiser rejects scripts, event
handlers, DTDs, imported styles, active embedded content, network/file references, and sources
that exceed node, path, character, or byte limits. Safe local fragment paint references such as
`url(#gradient)` remain supported.
