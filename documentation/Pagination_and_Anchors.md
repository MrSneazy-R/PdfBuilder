# Pagination, sections, anchors, and navigation

The canonical API resolves navigation after final pagination. A table of contents can
therefore appear before the sections it references, and page references remain correct
when content flows onto additional pages or tables continue.

## Canonical sections and tables of contents

Use an explicit, document-unique ID for every section. Titles do not have to be unique.
Section levels drive hierarchical numbering, table-of-contents indentation, and PDF
outline nesting.

```csharp
PdfDocument document = PdfDocument.Create(document =>
{
    document.Page(page =>
    {
        page.Content().Text("Contents").Bold();
        page.Content().TableOfContents(options =>
        {
            options.IncludeSectionNumbers();
            options.PageNumberFormat("page {0}");
        });
    });

    document.Page(page => page.Content().Section(
        "introduction",
        "Introduction",
        content => content.Text("Introduction body")));

    document.Page(page => page.Content().Section(
        "details",
        "Details",
        content => content.Text("Details body"),
        section =>
        {
            section.Level(2);
            section.StartOnNewPage();
        }));
});
```

`Numbered(false)` omits a section number. `IncludeInOutline(false)` and
`IncludeInTableOfContents(false)` independently control those two navigation surfaces.
`StartOnNewPage()` inserts a bounded layout page break before the section.

## Anchors, outlines, and page references

`Anchor(id)` adds a zero-height internal target. `Bookmark(id, title, level)` adds the
same target and exposes it in the PDF outline. `PageReference(id, format, pendingText)`
uses the final page number and reserves conservative width during layout.

```csharp
page.Content().Bookmark("appendix", "Appendix", level: 1);
page.Content().Text("See appendix on ");
page.Content().PageReference("appendix", "page {0}");
```

Duplicate IDs throw `PdfNavigationException` during composition. Missing internal-link
or page-reference targets add a `PDFNAV001` entry to
`document.NavigationDiagnostics.Entries`; dead link annotations are omitted.

## Links

Both ordinary linked text and independently styled rich-text spans are canonical:

```csharp
page.Content().InternalLink("Jump to appendix", "appendix").Underline();
page.Content().ExternalLink("Project site", "https://example.com").Underline();
page.Content().RichText(text =>
{
    text.Span("Read ");
    text.ExternalLink("the guide", "https://example.com/guide").Underline();
});
```

External links accept absolute `http`, `https`, and `mailto` URIs. Executable, file,
data, and other schemes are rejected by default. URI values and Unicode outline titles
reuse the central PDF string encoder.

## Legacy API

`ColumnBuilder.Section`, `AnchorBuilder`, `RichRun.LinkAnchor`, `RichRun.LinkUrl`, and
`ColumnBuilder.TableOfContents` remain supported. The canonical API is preferred for new
documents because it can discover later sections before building an earlier TOC.
