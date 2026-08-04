# PdfBuilder

> **Pre-release:** PdfBuilder `0.1.0-alpha.1` is under active production hardening. Validate output against your own documents before production use.

PdfBuilder is a cross-platform .NET PDF library for invoices, statements, operational documents, and reports. It keeps its own PDF writer and layout engine; it is not a wrapper around another PDF generator.

## Installation

```bash
dotnet add package PdfBuilder --prerelease
```

Use the stable .NET 10 SDK specified in [global.json](global.json).

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

`container.Table` supports relative/fixed columns, headers, repeating continuation headers, and normal flow pagination. [samples/Invoice](samples/Invoice) demonstrates a multi-page invoice table.

## Images and SVG

Use `container.Image` for PNG/JPEG and `container.Svg` for sanitised inline SVG. Do not pass untrusted oversized media; PdfBuilder rejects unsafe data and blocks SVG external resources by default.

## Headers and footers

`page.Header()`, `page.Content()`, and `page.Footer()` all accept normal containers. Existing `{page}` and `{pages}` footer templates remain compatible.

## Reusable components

Keep templates typed and side-effect free. The invoice sample’s `InvoiceTemplate.Create(Invoice)` accepts an immutable model and performs no database or service access.

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
