# Page-aware visibility and repeated content

The canonical API can select content using the final one-based page number. The
selection is deterministic and does not execute user callbacks from the PDF writer.

```csharp
PdfDocument document = PdfDocument.Create(document =>
{
    document.Page(page =>
    {
        page.FirstPageHeader().Text("Annual report").Bold();
        page.ContinuationHeader().Text("Annual report - continued");
        page.Footer()
            .PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}")
            .AlignRight();

        page.Content().Column(column =>
        {
            column.Item().ShowOnce().Text("Opening note");
            column.Item().LastPageOnly().Text("End of report");
        });
    });
});
```

## Canonical selectors

- `ShowOnce()` selects page 1; `SkipOnce()` selects continuation pages.
- `FirstPageOnly()` and `LastPageOnly()` select the corresponding final page.
- `OddPagesOnly()` and `EvenPagesOnly()` select by one-based page parity.
- `ContinuationPagesOnly()` selects every page after page 1.
- `FirstPageHeader()`, `ContinuationHeader()`, `FirstPageFooter()`, and
  `ContinuationFooter()` provide explicit repeated-band variants.

Selectors can be combined. Contradictory selectors deterministically hide the
branch. A hidden branch is not measured or drawn, so it emits no annotations,
anchors, images, fonts, or other PDF resources. Page predicates are especially
useful in headers, footers, and other repeated containers; an ordinary flow item
that is hidden on its encounter page consumes no layout space.

## Pagination and diagnostics

Final-page selection can change pagination, so canonical composition repeats with
the newly observed total page count until it stabilises. Each pass counts against
`PdfRenderLimits.MaximumPaginationPasses`. If the layout does not converge,
`PdfPaginationStabilizationException` reports the configured limit and retained
component/debug-label paths. Configure a lower application-specific limit before
composition when appropriate:

```csharp
PdfDocument.Create(document =>
{
    document.RenderLimits(limits => limits.MaximumPaginationPasses = 8);
    document.Page(page => page.Content().LastPageOnly().Text("Certification"));
});
```

Header and footer space is reserved only on pages where the corresponding band is
visible. First-page and continuation variants therefore do not leave empty bands
on pages where they are excluded.

See `samples/CanonicalReport` for the complete Phase 1 report using these controls
with page-number tokens, a forward TOC, internal and external links, and outlines.
