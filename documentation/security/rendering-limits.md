# Rendering limits

`PdfDocument.RenderLimits` provides per-document safeguards. Defaults permit normal business documents: 10,000 pages and 32 layout attempts. Applications may set lower page, layout-iteration, output-size, image-pixel, SVG-byte, table-row, and related media limits for untrusted workloads.

```csharp
var document = PdfDocument.Create(d => d.Page(p => p.Content().Text("Bounded output")));
document.RenderLimits.MaximumPages = 250;
document.RenderLimits.MaximumOutputBytes = 10 * 1024 * 1024;
var bytes = document.GenerateBytes();
```

Limits throw `PdfRenderLimitException` with the failing limit name; PdfBuilder never silently truncates output. Existing media validation rejects empty, malformed, unsupported, oversized, and excessive-pixel PNG/JPEG data before decode. SVG rejects external resources and bounded complexity. Always pass caller-owned cancellation tokens to generation paths under load.
