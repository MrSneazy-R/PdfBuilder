# Page context and numbering

PdfBuilder resolves page-number values after pagination. Canonical content, headers, and
footers can use the same token-based API without executing a caller callback in the PDF
serialization pass:

```csharp
document.Page(page =>
{
    page.Header().PageText(
        $"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}");

    page.Content().PageText(
        $"Content page {PageTextTokens.CurrentPage}");
});
```

`PageText` accepts `{page}` and `{pages}` through `PageTextTokens.CurrentPage` and
`PageTextTokens.TotalPages`. The literal placeholders remain compatible with legacy
header/footer templates.

Page-aware text is conservatively measured before pagination and rendered with the final
one-based page number and total page count. This prevents a transition such as 9 to 10
pages from expanding the allocated layout during serialization.

The immutable `PageContext` contains the current and total page numbers, first/last and
odd/even flags, physical page dimensions, and available content dimensions. Repeated
content can access the final context through `HeaderFooterTokens.Context`.

Finalization is cancellation-aware and bounded by
`PdfRenderLimits.MaximumPaginationPasses`. A callback that changes the page collection on
every finalization pass raises `PdfPaginationStabilizationException` with corrective
guidance. `IContainer.Text(Func<string>)` remains for compatibility but is obsolete; use
`PageText` for pagination values.
