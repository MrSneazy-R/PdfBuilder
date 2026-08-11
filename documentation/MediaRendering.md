# Media rendering

PdfBuilder accepts PNG, JPEG, and still WebP images through the same cross-platform pipeline. Source dimensions are
validated before pixel decoding: sources are limited to 64 MB, 32,768 pixels on either axis, and
100 million decoded pixels. Malformed headers and impossible dimensions throw `InvalidDataException`.

WebP, EXIF orientation, resizing, and alpha-aware re-encoding use SkiaSharp rather than Windows
WIC. Animated WebP is rejected explicitly. The WebP release gate requires the same tests to pass
in retained Windows, Ubuntu, and macOS workflow runs before this branch is merged.

SVG is accepted only as inline markup. It is parsed with DTDs and entity resolution disabled.
Scripts, active embedded elements, event handlers, external `href` references, imported styles,
network/file URLs, oversized documents, excessive node counts, and excessive path data are
rejected before Skia parses sanitized markup. Local fragment references such as `url(#gradient)`
remain available.

The canonical API does not require coordinates or `System.Drawing`:

```csharp
document.Page(page => page.Content().Column(column =>
{
    var logo = ImageSource.FromFile("assets/logo.webp").Preload();
    column.Item().Image(logo, 120, 48)
        .Contain()
        .MaximumEffectiveDpi(220)
        .JpegQuality(88)
        .CornerRadius(4);
    column.Item().Svg("<svg viewBox='0 0 10 10'><circle cx='5' cy='5' r='5'/></svg>", 48, 48);
    column.Item().Barcode("INV-2026-001", BarcodeKind.Code128);
    column.Item().Chart(chart =>
    {
        chart.Size(420, 180);
        chart.Title("Revenue");
        chart.Categories("Jan", "Feb", "Mar");
        chart.Legend(ChartLegendPosition.TopRight);
        chart.Line("Actual", [10f, 12f, 15f]).Markers();
        chart.Area("Forecast", [9f, 13f, 16f]).Fill(PdfColor.Parse("#330057B8"));
    });
}));
```

`ImageSource` supports byte arrays, read-only memory, streams, local files, embedded resources,
preloaded/shared instances, and caller-owned lazy byte factories. Sources snapshot caller bytes
once and are safe for concurrent reuse. Images support stretch, contain, cover/crop, DPI-aware
original sizing, nine-point crop alignment, opacity, borders, rounded corners, circle clipping,
resampling quality, maximum effective DPI, opt-in downsampling, JPEG quality, and alpha-aware
encoding. Identical prepared content is deduplicated by SHA-256 content hash. Remote URLs are not
accepted. Barcode output remains vector PDF paths with the configured quiet zone.

Canonical charts expose line, area, grouped bar, stacked bar, 100% stacked bar, pie, donut,
and scatter series through typed descriptors. Numeric axes can set ranges, tick counts, and
formatters; cartesian series can target a secondary Y axis. Line and scatter descriptors expose
markers, lines can be smoothed, and every series supports labels where meaningful.

Chart colours use `PdfColor` exclusively. A document-scoped palette can reference literal colours
or named theme colour tokens, and each page receives the normal cloned theme snapshot:

```csharp
document.Theme(theme => theme
    .Color("Brand", "#0057B8")
    .Color("Accent", "#59A15D")
    .ChartPalette("Brand", "Accent"));
```

The canonical renderer is separated into layout, scale, tick, axis, legend, label/drawing, and
typed-series modules. The coordinate-based `ChartBuilder` remains available as a compatibility
surface and continues through the legacy renderer.
