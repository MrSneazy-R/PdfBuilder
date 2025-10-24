PdfDocumentBuilder
==================

Purpose
-------
`PdfDocumentBuilder` wires up a `PdfDocument` with global layout options, metadata, headers/footers, masters, and the declarative composer pipeline.

Construction
------------
```csharp
var doc = new PdfDocument();
var builder = new PdfDocumentBuilder(doc);
```
The builder mutates the provided `PdfDocument` instance. Call `Build()` on the document when you are ready to hand it to a writer.

Configuration Methods
---------------------
- `UseLayout(Action<LayoutOptions>)`: configure measure/draw mode, caching, profiler, and debug toggles.
- `DefaultContentMargin(float points)`: default margin applied to newly created pages in the composer.
- `LayoutDebug(Action<LayoutDebugOptions>)`: enable bounding boxes, guide overlays, or trace logging.
- `Profiler(Action<LayoutProfilerConfig>)`: hook the layout profiler (outputs measurement timing and events).
- `DefaultTextStyle(Action<TextStyleDefaults>)`: set global type defaults cloned into each page or section.
- `Metadata(Action<DocumentMetadata>)`: fill the PDF info dictionary (Author, Subject, etc.).
- `Title(string)`: shortcut for assigning `PdfDocument.Title` (used in templates and metadata).
- `HeaderFooter(Action<HeaderFooterSpec>)`: set document-wide header/footer templates or layouts.
- `Header/ Footer(Action<ContentComposer>, float? spacing)`: supply rich header/footer layout DSL blocks.
- `Master(Action<MasterPageSpec>)`: configure page backgrounds or watermarks.
- `OutputOptions(Action<PdfOutputOptions>)`: tweak compression, image predictor, and stream filters.
- `EnablePageNumbers(PageNumberPlacement placement, string template)`: inject page numbering tokens.
- `StartSection(...)`: clone current header/footer/master defaults, mutate them for the upcoming pages.
- `ApplySectionTo(PdfPage)`: internal helper used by the composer; call this only when constructing pages manually.
- `SetHeader(string)` / `SetFooter(string)`: legacy template setters retained for compatibility.
- `DefaultContentMargin`, `DefaultTextStyle`, and section settings are persisted until changed.

Composition
-----------
- `Compose(Action<DocumentComposer>)`: entry point for creating pages, columns, and content.
- `_ = builder.Compose(doc => doc.Page(...));` ensures the column flow is set up and paginated.

Complete Example
----------------
```csharp
var pdf = new PdfDocument();
new PdfDocumentBuilder(pdf)
    .Title("Quarterly Summary")
    .Metadata(info =>
    {
        info.Author = "Reporting Bot";
        info.Subject = "Q2 Highlights";
    })
    .DefaultContentMargin(48)
    .HeaderFooter(hf =>
    {
        hf.HeaderTemplate = "{title}";
        hf.FooterTemplate = "Page {page} of {pages}";
        hf.HeaderAlign = TextAlignment.Center;
        hf.FooterAlign = TextAlignment.Right;
    })
    .Master(master =>
    {
        master.BackgroundColor = "#F8F8F8";
        master.Watermark = new WatermarkSpec
        {
            Text = "CONFIDENTIAL",
            RotationDegrees = 45,
            Opacity = 0.06f
        };
    })
    .Compose(doc =>
    {
        doc.Page(page =>
        {
            page.Content(col => col.Text("Executive summary goes here").FontSize(18).Add());
        });
    });

new PdfWriter().GenerateBytes(pdf);
```

Expected Outcome
----------------
The generated PDF contains a single page with a light gray background, the title centered in the header, page numbering in the footer, and the text "Executive summary goes here" at the top of the content column. A faint diagonal "CONFIDENTIAL" watermark appears behind the content.
