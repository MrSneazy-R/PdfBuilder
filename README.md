# PdfBuilder

> **Pre-release:** PdfBuilder `0.1.0-preview.2` is under active production hardening. Validate output against your own documents before production use. It is neither stable nor a 1.0 release candidate.

PdfBuilder is a cross-platform .NET PDF library for invoices, statements, operational documents, and reports. It keeps its own PDF writer and layout engine; it is not a wrapper around another PDF generator.

## Installation

```bash
dotnet add package PdfBuilder --version 0.1.0-preview.2
```

Use that command with the owner-approved private or local package source. PdfBuilder is not currently published to NuGet.org.

Use the stable .NET 10 SDK specified in [global.json](global.json).

The production-readiness gates, supported platforms, security model, support policy,
and release limitations are recorded in [the release-candidate guide](documentation/release/release-candidate.md).

## Five-minute quick start

```csharp
using PdfBuilder.Document;

var pdf = PdfDocument.Create(document =>
{
    document.Metadata(metadata => { metadata.Title = "Hello"; metadata.Author = "PdfBuilder"; });
    document.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(10));
        page.Content().Column(column =>
        {
            column.Item().Text("PdfBuilder").FontSize(22).Bold();
            column.Item().Text("Coordinate-free PDF generation.");
        });
    });
});
pdf.Save("hello.pdf");
```

The examples above compile in [samples/HelloPdf](samples/HelloPdf).

## ASP.NET Core stream response

```csharp
app.MapGet("/report", (HttpResponse response, CancellationToken cancellationToken) =>
{
    var document = PdfDocument.Create(d => d.Page(p => p.Content().Text("Report")));
    response.ContentType = "application/pdf";
    document.Generate(response.Body, cancellationToken);
});
```

See [samples/AspNetCorePdfApi](samples/AspNetCorePdfApi).

## Page layout

Use `PageSizes`, `Margin`, `Column`, `Row`, `Grid`, `Stack`, and `PageBreak`; all canonical layout uses `IContainer` and points internally. Use `Units.Millimeters`, `Centimeters`, or `Inches` for explicit conversions.

## Text and fonts

Use `Text`, `FontSize`, `Bold`, and `DefaultTextStyle`. Font registration and fallback must be configured deliberately in applications that need non-base fonts; test multilingual output on every target platform. See [samples/MultiLanguage](samples/MultiLanguage).

## Tables

`container.Table` supports constrained fixed/relative/auto columns, explicit placement and spans, header/body/footer groups, configurable repeated headers and footers, continuous banding, border-collapse controls, stable multi-page widths, and opt-in controlled continuation for oversized rows. Cells are normal canonical containers: `Cell().Text(...)` remains convenient, while rich text, images, SVG, barcodes, nested layouts, layers, and reusable components use the same composition path as page content. [samples/Invoice](samples/Invoice) demonstrates the complete Phase 2 surface, including a controlled split row.

[samples/CanonicalReport](samples/CanonicalReport) demonstrates the canonical Phase 1 surface: first-page and continuation headers, final `Page X of Y` tokens, a forward table of contents, internal and external links, outlines, and last-page-only content.

## Images and SVG

Use `ImageSource` to load PNG, JPEG, or still WebP data from bytes, read-only memory,
streams, local files, embedded resources, preloaded shared instances, or a caller-owned lazy
factory. `container.Image(source)` uses DPI-aware intrinsic size; explicit boxes support
contain, cover, stretch, crop alignment, effective-DPI downsampling, JPEG quality, and
alpha-aware encoding. Remote URLs remain outside the core API. `container.Svg` accepts
sanitised inline SVG and blocks scripts, active content, DTDs, event handlers, and network/file
resources.

## Canvas and graphics

`container.Canvas` provides an available-size-aware vector surface with isolated graphics
state, finite translation/rotation/scaling/flipping transforms, rectangular clipping,
solid/dashed/dotted strokes, bounded linear and radial gradients, bounded vector shadows,
and explicit background/content/foreground paint ordering. `container.DynamicSvg` generates
sanitised SVG from the final available size. Canvas command bytes, command counts, and effect
steps participate in document render limits. See
[documentation/Canvas_and_Svg.md](documentation/Canvas_and_Svg.md).

## Headers and footers

`page.Header()`, `page.Content()`, and `page.Footer()` all accept normal containers. Existing `{page}` and `{pages}` footer templates remain compatible.

## Reusable components

Keep templates typed and side-effect free. The invoice sample subclasses `PdfTemplate<Invoice>`, accepts an immutable model, and performs no database or service access.

## Diagnostics

Enable structured traces before canonical composition and label important containers:

```csharp
document.Diagnostics(options => options.EnableLayoutTrace = true);
page.Content().DebugLabel("InvoiceTotals").Text("Total");
```

See [documentation/engineering/LAYOUT-DIAGNOSTICS.md](documentation/engineering/LAYOUT-DIAGNOSTICS.md) and [samples/LayoutDiagnostics](samples/LayoutDiagnostics).

## Cross-platform requirements

Windows, Ubuntu, and macOS are exercised in CI. Native SkiaSharp and HarfBuzz assets restore from NuGet; no native binaries are committed. Ubuntu CI installs Noto fonts, Poppler, and qpdf for validation.

## Limitations

This pre-release does not claim PDF/A or PDF/UA conformance and does not support encryption, signatures, forms, attachments, HTML rendering, remote images, or browser SVG.

## Security considerations

Treat PDFs and media as untrusted inputs. Enforce application-level size limits, avoid placing secrets in diagnostics, and use inline SVG only. Generated PDFs can contain business data and require the same storage/access controls as their source records.

## Versioning and release policy

Pre-release versions may add or refine APIs. Deprecated legacy APIs remain functional with `PDFB00x` migration warnings until an announced major release. **No public NuGet publishing is enabled until the repository owner approves a licence.** Local/private packages remain supported.

## Migration

See [legacy-to-canonical-api.md](documentation/migration/legacy-to-canonical-api.md). Every maintained sample uses the canonical API; legacy `Add()` finalisers are compatibility-only.

Output can be selected coherently with `Debug`, `Balanced`, `SmallFile`, `PrintQuality`, or `Deterministic` presets. PDF version, BCP 47 document language, explicit stable identifiers, validated custom XMP, generation metrics, and bounded output size are configurable without bypassing the central serializer. See [Output hardening](documentation/Output_Hardening.md).
