# Tagged PDF semantics

PdfBuilder can emit an opt-in semantic structure tree while retaining the canonical layout
and rendering path. Tagged output is disabled by default and does **not** claim PDF/UA
conformance.

## Enable tagged output

```csharp
var pdf = PdfDocument.Create(document =>
{
    document.Tagged(tagged => tagged.Language("en-ZA"));
    document.Page(page =>
    {
        page.Header().Text("Quarterly report");
        page.Content().Semantic(PdfSemanticRole.Section).Column(column =>
        {
            column.Item().Heading(1).Text("Performance");
            column.Item().Semantic(PdfSemanticRole.Paragraph)
                .Text("Revenue increased during the quarter.");
            column.Item().Semantic(PdfSemanticRole.Figure)
                .AlternativeText("Revenue increased from January to March")
                .Canvas(160, 60, canvas => canvas.Line(0, 55, 160, 5));
            column.Item().Semantic(PdfSemanticRole.Caption).Text("Figure 1");
            column.Item().ExternalLink("Supporting data", "https://example.com/data");
        });
        page.Footer().PageText("Page {page} of {pages}");
    });
});
```

`Language` accepts a non-empty BCP 47 language tag and enables tagged output. Calling
`Enabled()` without setting document metadata language causes generation to fail with an
actionable exception.

## Roles and inheritance

`Semantic(PdfSemanticRole)` establishes a structure element around the container's normal
content. Nested semantic containers become nested structure elements. `Heading(1)` through
`Heading(6)` select the corresponding heading role. `ReadingOrder(int)` orders semantic
siblings without changing visual paint order.

Canonical text defaults to paragraph semantics; tables, figures, and links default to their
matching roles. Page header/footer variants default to header/footer roles. Table cells use
the normal container API, so header and data cells can be labelled explicitly with
`TableHeaderCell` and `TableCell`.

Figures should carry concise `AlternativeText`. `Decorative()` excludes a container from the
structure tree and emits its painted content as an artifact. Page backgrounds and purely
visual decoration are artifacts automatically.

## Writer output

Each page uses page-local MCIDs derived from final layout. The writer emits marked-content
sequences, `/StructParents`, a parent number tree, a document structure root, deterministic
role mappings, and object references connecting link structure elements to their annotations.
Structure order follows semantic nesting and optional sibling reading-order keys; pagination
does not execute callbacks in the low-level writer.

## Scope and validation

Tests retain qpdf structural validation, Poppler text extraction, deterministic concurrent
generation, and checks for structure roles, artifacts, parent-tree entries, and link
association. These features are prerequisites for later accessibility work, but they do not
on their own establish correct reading order for every document, complete table semantics,
or PDF/UA-1 conformance. Conformance requires independent validation and all mandatory
semantic, font, metadata, and annotation requirements.

The complete runnable example is in [`samples/TaggedReport`](../samples/TaggedReport).
