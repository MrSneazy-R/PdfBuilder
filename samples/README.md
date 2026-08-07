# Samples

## Runnable samples

- `HelloPdf`: five-minute canonical API introduction.
- `Invoice`: immutable model, reusable template function, multi-page table, totals, header, and footer.
- `MultiPageReport`: flowing report content with repeating header/footer.
- `MultiLanguage`: multilingual text smoke sample.
- `AspNetCorePdfApi`: direct HTTP response streaming with cancellation.
- `LayoutDiagnostics`: JSON trace and selected-page preview.

## TablePerfSample

Generates multi-script table scenarios and reports average generation time and final PDF size.

```
dotnet run --project samples/TablePerfSample/TablePerfSample.csproj -c Release
```

Outputs are written to `samples/TablePerfSample/bin/<config>/net10.0/output/`.
