ImageBuilder
============

Purpose
-------
`ImageBuilder` wraps `ImageElement` to place raster images (PNG, JPEG, etc.) with sizing, clipping, borders, and hyperlinks.

Creating an Image
-----------------
```csharp
var bytes = File.ReadAllBytes("logo.png");
col.Image(bytes, page.MarginLeft, col.GetCurrentY(), width: 128, height: 48)
   .Add();
```

Sizing & Position
-----------------
- `X(float)`, `Y(float)`: manual positioning relative to page origin.
- `Width(float)`, `Height(float)`: rendered size in points.
- `MaxWidth(float)`, `MaxHeight(float)`: auto-scale down when the image exceeds the limit.
- `Rotation(float degrees)`: rotate about the image center.
- `Opacity(float)`: alpha blending (0 to 1).

Margins & Padding
-----------------
- `Margin(...)`, `MarginTop(...)`, etc.: control spacing within the flow.
- `Padding(...)`: add transparent space around the bitmap before rendering.

Borders & Shapes
----------------
- `Border(string color, float width)` or set color/width independently.
- `CornerRadius(float)` for rounded rectangles.
- `Clip(ImageClipShape)`: choose `RoundedRect`, `Circle`, or `Ellipse`.
- `ClipEllipse(EllipseOrientation orientation, float squash)`: elliptical clipping with optional distortion.

Shadows
-------
- `Shadow(string color, float offsetX, float offsetY, float? blur)`: drop shadow effect.
- Individual setters for color, offsets, and blur are also available.

Hyperlinks & Metadata
---------------------
- `Hyperlink(string url)`: make the image clickable.
- `MimeType(string)`: specify image format if detection is ambiguous.
- `ImageId(string)`: deduplicate identical images within the document.

Quality Helpers
---------------
- `FitToMinDpi(float minDpi)`: scales the image down to maintain the requested minimum DPI based on intrinsic pixel dimensions.

Example
-------
```csharp
col.Image(heroBytes, page.MarginLeft, col.GetCurrentY(), 400, 220)
   .CornerRadius(12)
   .Shadow("#000000", offsetX: 0, offsetY: -2, blur: 6)
   .Hyperlink("https://contoso.example")
   .FitToMinDpi(180)
   .Add();
```

Expected Outcome
----------------
- The image appears with rounded corners and a soft drop shadow.
- Clicking the image opens the specified URL.
- If the bitmap resolution is too low for 180 DPI at 400x220pt, the builder scales it down to avoid blur.
