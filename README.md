# PdfBuilder

> **Pre-release:** PdfBuilder is under active production-hardening. APIs and rendering behavior should be validated against your own documents before production deployment.

PdfBuilder is a .NET library for generating invoices, statements, operational documents, and management reports. It uses its own PDF writer, layout engine, and rendering architecture.

## Prerequisites

Build and run PdfBuilder with the stable .NET 10 SDK pinned in [global.json](global.json). Native SkiaSharp and HarfBuzz assets are restored through NuGet for Windows, Ubuntu, and macOS; no native binaries are committed to this repository.

## Install

```powershell
dotnet add package PdfBuilder --prerelease
```

## Minimal example

```csharp
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Writer;

var document = new PdfDocument();
var page = document.AddPage();
page.Elements.Add(new TextElement
{
    X = page.MarginLeft,
    Y = page.Height - page.MarginTop - 24,
    Text = "Hello from PdfBuilder",
    FontFamily = "Helvetica",
    FontSize = 12
});

new PdfWriter().Save(document, "hello.pdf");
```

See [documentation/Overview.md](documentation/Overview.md) for the current API areas and [documentation/engineering/BASELINE.md](documentation/engineering/BASELINE.md) for the repository baseline.
