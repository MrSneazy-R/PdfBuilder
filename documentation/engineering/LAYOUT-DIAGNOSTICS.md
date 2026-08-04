# Layout diagnostics and previews

PdfBuilder diagnostics are opt-in and document-scoped. Enable them before composing canonical pages:

```csharp
var pdf = PdfDocument.Create(document =>
{
    document.Diagnostics(options => options.EnableLayoutTrace = true);
    document.Page(page => page.Content()
        .DebugLabel("InvoiceTotals")
        .Text("Total"));
});

File.WriteAllText("layout-trace.json", pdf.LayoutTrace.ToJson());
```

Trace events contain component identity, available geometry, measure/draw results, page or column transitions, cache hits, warnings, and timing when the profiler is enabled. Text content is deliberately excluded. `DebugLabel` adds a domain-specific segment to paths such as `Document > Page[1] > Content > InvoiceTotals`.

`LayoutOptions.Debug.DrawBoundingBoxes`, `ShowFlowGuides`, and `TraceLayout` remain supported. `PDFBUILDER_LAYOUT_DEBUG=boxes,guides,trace` configures the same legacy visual/trace aids for `PdfDocumentBuilder`; structured traces are controlled with `PdfDiagnosticsOptions`.

`GeneratePreviewImages` renders selected one-based pages at a requested DPI from the document's resolved page layout. It accepts a cancellation token and does not generate a second PDF. Previews are a development aid; final visual regression continues to rasterise generated PDFs independently in CI.

When a component cannot make progress, PdfBuilder throws `PdfLayoutException`. Its `Context` includes the component path, page and column, available and measured sizes, break policy, iteration count, constraints, and corrective suggestions.
