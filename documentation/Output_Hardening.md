# Output hardening

PdfBuilder keeps content streams compressed by default. Choose a coherent preset through the canonical API:

```csharp
PdfDocument document = PdfDocument.Create(descriptor =>
{
    descriptor.OutputPreset(PdfOutputPreset.Deterministic);
    descriptor.Output(output => output.PdfVersion = PdfVersion.Pdf17);
    descriptor.Generation(generation =>
        generation.DocumentIdentifier = "00112233445566778899AABBCCDDEEFF");
    descriptor.Metadata(metadata =>
    {
        metadata.Language = "en-ZA";
        metadata.CustomXmp = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\" />";
    });
    descriptor.Page(page => page.Content().Text("Stable report"));
});
```

The presets are starting points: `Debug` writes readable content streams; `Balanced` retains the normal compressed defaults; `SmallFile` favours stronger compression and lower effective image DPI; `PrintQuality` retains more image detail; and `Deterministic` applies balanced output plus deterministic timestamps, identifiers, and ordering. Explicit options applied after a preset win.

`DocumentMetadata.Validate()` checks metadata length, BCP 47-style language tags, XMP byte limits, well-formed XML, and rejects DTD/external-resource processing. `PdfRenderLimits.MaximumMetadataCharacters`, `MaximumXmpBytes`, and `MaximumOutputBytes` let applications apply stricter limits. All ordinary strings, dates, names, URIs, and identifiers continue through the central encoders.

After a successful write, inspect `PdfWriter.LastGenerationMetrics` or `PdfDocument.LastGenerationMetrics` for page/object/resource counts, output bytes, elapsed time, and retained page-stream counts. The metrics are observations, not mutable rendering inputs.
