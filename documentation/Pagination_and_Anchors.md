Pagination, Sections, and Anchors
=================================

ColumnBuilder Sections
----------------------
- `Section(string title, Action<SectionContext>? configure = null, int level = 1, bool startOnNewPage = false, bool includeInToc = true)`:
  - Assigns an anchor id (auto-slugged), registers a `SectionEntry` with the `PaginationRegistry`, and inserts an invisible anchor at the current flow position.
  - `level` controls numbering (e.g., 1.2.3) and indentation within the table of contents.
  - `startOnNewPage` triggers a page break before the section if the current column already contains content.
  - `includeInToc` toggles whether the section appears in the generated TOC.
  - `SectionContext` callback lets you add headings, numbering, or side effects inside the section boundary.

AnchorBuilder
-------------
- `ColumnBuilder.Anchor(string id)`: returns an `AnchorBuilder`.
- Chain `.Title(string)` and `.Level(int)` to describe the anchor for outlines/TOC.
- Call `.Add()` to insert the anchor at the current flow position.

Table of Contents
-----------------
- `ColumnBuilder.TableOfContents(Action<TableOfContentsOptions>? configure = null)`:
  - Generates a table listing sections recorded so far.
  - Default columns: left column for title, right column for page number stub.
  - Options: `IncludeNumbers`, `IndentPerLevel`, `PageNumberColumnWidth`, `PageNumberFormat`, `PendingPageText`, `NumberSeparator`.
  - During final rendering, `PaginationRegistry` resolves page numbers and replaces placeholders.

Linking Sections from Text
--------------------------
- In `RichTextBuilder` or `TableCellBuilder`, set `RichRun.LinkAnchor = "<anchor-id>"` to jump to anchors registered earlier (sections or manual anchors).
- `LinkUrl` supports external URIs.

Example
-------
```csharp
builder.Compose(doc =>
{
    doc.Page(page =>
    {
        page.Content(col =>
        {
            col.TableOfContents(options =>
            {
                options.PageNumberColumnWidth = 56;
                options.PendingPageText = "...";
            });

            col.Section("Overview", section =>
            {
                col.Text(section.TitleWithNumber).FontSize(18).Bold().Add();
                col.Text("High-level summary of findings.").Add();
            });

            col.Section("Detailed Metrics", section =>
            {
                col.Text(section.TitleWithNumber).FontSize(18).Bold().Add();
                col.Text("Charts and tables go here.").Add();
            });
        });
    });
});
```

Expected Outcome
----------------
- First page shows a two-column table of contents with entries "1 Overview" and "2 Detailed Metrics". Initial page numbers render as the pending token (`...`) during layout, but pagination replaces them with actual numbers in the final PDF.
- Section headings inside the flow inherit numbering (`1 Overview`, `2 Detailed Metrics`) due to `SectionContext.TitleWithNumber`.
- Clicking TOC entries or outlines jumps to the respective sections, thanks to the anchors created automatically by `Section`.
