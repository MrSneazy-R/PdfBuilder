# Samples

## Runnable samples

- `HelloPdf`: five-minute canonical API introduction.
- `Invoice`: immutable typed template, reusable components, theme tokens, stable multi-page columns, nested rich cells, repeated table groups, spans, controlled row continuation, dates, tax, and totals.
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
