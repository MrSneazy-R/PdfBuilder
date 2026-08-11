# Media rendering

PdfBuilder accepts PNG and JPEG image bytes on every supported platform. Source dimensions are
validated before pixel decoding: sources are limited to 64 MB, 32,768 pixels on either axis, and
100 million decoded pixels. Malformed headers and impossible dimensions throw `InvalidDataException`.

WebP is intentionally unsupported in this release. PdfBuilder no longer invokes Windows WIC, so
the same input has the same explicit `NotSupportedException` outcome on Windows, Linux, and macOS.

SVG is accepted only as inline markup. Scripts, `href`/`xlink:href` references, `url(...)`
resources, oversized documents, excessive node counts, and excessive path data are rejected before
Skia parses markup. SVG is rasterized with aspect ratio preserved and never resolves network or
local files.

The canonical API does not require coordinates or `System.Drawing`:

```csharp
document.Page(page => page.Content().Column(column =>
{
    column.Item().Image(logoBytes, 120, 48).Contain().CornerRadius(4);
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

Images support stretch, contain, cover/crop, intrinsic DPI sizing, opacity, borders, rounded
corners, circle clipping, alignment, rotation, and existing shadow rendering. PNG and JPEG are
deduplicated per document by the resource manager. Barcode output is vector PDF paths with the
configured quiet zone; the canonical API exposes QR Code and Code 128.

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
