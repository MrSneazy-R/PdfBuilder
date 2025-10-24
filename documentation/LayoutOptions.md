Layout Options & Profiling
==========================

LayoutOptions (`Document/Layout/LayoutOptions.cs`)
-------------------------------------------------
- `Mode`: choose between `LayoutMode.SinglePass` (legacy immediate rendering) and `LayoutMode.MeasureDraw` (default, enables the new pipeline with reflow and pagination awareness).
- `EnableMeasurementCaching`: cache measurement results for repeated components to improve performance.
- `Debug`: `LayoutDebugOptions` with `DrawBoundingBoxes`, `ShowFlowGuides`, `TraceLayout`.
- `Profiler`: `LayoutProfilerConfig` (see below).
- `Clone()`: creates a deep copy for per-page customization.

LayoutDebugOptions
------------------
- `DrawBoundingBoxes`: renders rectangles around measured components for visual debugging.
- `ShowFlowGuides`: draws column guides, header/footer boundaries, and content flow markers.
- `TraceLayout`: emits diagnostics via `System.Diagnostics.Trace`.

LayoutProfilerConfig & Session (`Document/Layout/LayoutProfiler.cs`)
-------------------------------------------------------------------
- `Enabled`: toggles profiling.
- `OutputPath`: when set, writes a JSON snapshot after the document renders.
- `OnCompleted`: callback invoked with `LayoutProfileSnapshot` for custom ingestion.

Snapshot contents (`LayoutProfileSnapshot`):
- Total measure/draw time and per-component breakdown (`LayoutProfileEntry`).
- Each entry includes counts, totals, averages, max times, and cache hits.

Example
-------
```csharp
new PdfDocumentBuilder(doc)
    .UseLayout(options =>
    {
        options.Mode = LayoutMode.MeasureDraw;
        options.EnableMeasurementCaching = true;
        options.Debug.DrawBoundingBoxes = false;
    })
    .Profiler(cfg =>
    {
        cfg.Enabled = true;
        cfg.OutputPath = "artifacts/layout-profile.json";
    })
    .Compose(...);
```

Expected Outcome
----------------
- Layout engine uses the measure/draw pipeline with caching.
- After rendering, a `layout-profile.json` file appears, listing time spent measuring/drawing each component type. You can analyze slow components and optimize templates accordingly.

Environment Shortcuts
---------------------
You can enable layout debugging without changing code:
- `PDFBUILDER_LAYOUT_DEBUG=boxes,guides,trace`
- `PDFBUILDER_FONT_DIAGNOSTICS=on`

These variables are consumed during `PdfDocumentBuilder` construction, making it easy to toggle diagnostics in CI or local runs.
