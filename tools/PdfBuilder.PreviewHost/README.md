# PdfBuilder PreviewHost

This is a separate local development tool. It is not packed with `PdfBuilder`, does not accept non-loopback connections, and sends no telemetry.

Run it with file watching:

```powershell
dotnet watch --project tools/PdfBuilder.PreviewHost/PdfBuilder.PreviewHost.csproj
```

Open `http://localhost:5080`. The host exposes page thumbnails, selected-page PNG previews, the generated PDF download, component-path trace events, debug labels, profiling data, bounding boxes, margins, and flow guides. The preview toggle switches between separately composed clean and diagnostic snapshots so diagnostics never mutate the clean layout. Replace `PreviewDocumentFactory.Create` with the document factory being developed.

For a non-interactive generation check:

```powershell
dotnet run --project tools/PdfBuilder.PreviewHost/PdfBuilder.PreviewHost.csproj -- --self-test
```

The self-test writes the PDF plus clean and diagnostic page-one PNGs beside the built host for structural and visual comparison.
