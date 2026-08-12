# PdfBuilder.Operations

`PdfBuilder.Operations` is an isolated, optional package for authorised operations on existing local PDFs. It uses a caller-installed qpdf executable and is not a dependency of the core `PdfBuilder` package.

```csharp
var operations = new PdfOperationsClient(new QpdfBackendOptions
{
    QpdfPath = @"C:\Program Files\qpdf 12.3.2\bin\qpdf.exe",
    ProcessTimeout = TimeSpan.FromSeconds(30)
});

await operations.MergeAsync(
    new[]
    {
        new PdfMergeSource(new PdfInput("invoice.pdf")),
        new PdfMergeSource(new PdfInput("terms.pdf"), "1")
    },
    "combined.pdf",
    cancellationToken);
```

Supported operations are inspection, page selection/extraction, merge, split, overlay, underlay, attachments, AES-256 password encryption and permissions, authorised decryption, and linearisation.

## Security boundary

- qpdf is launched directly with `UseShellExecute = false` and `ProcessStartInfo.ArgumentList`; no shell or command string is used.
- All operations support cancellation and a process timeout. Timed-out processes are terminated with their process tree.
- Process output, temporary file count, and temporary bytes are bounded.
- Each operation owns a random temporary directory and performs best-effort cleanup.
- Page-range and single-line values are validated before qpdf receives them.
- Generated files must pass qpdf `--check` plus independent `%PDF-` header and `%%EOF` checks before being copied to the requested destination.
- Passwords are supplied only by the authorised caller. As with qpdf's command-line interface, encryption passwords may be visible to privileged operating-system process inspection while qpdf runs.

Remote URLs and shell invocation are intentionally outside the package.
