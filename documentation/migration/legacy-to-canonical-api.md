# Migrating from legacy builders to the canonical API

The preferred entry point is `PdfDocument.Create`. It creates pages and flowing containers without coordinates, `PdfElement`, `PdfWriter`, or `Add()` finalisers.

| Legacy pattern | Canonical replacement |
| --- | --- |
| `new PdfDocument()` + `new PdfWriter()` | `PdfDocument.Create(...)` + `GenerateBytes`, `Generate`, or `Save` |
| `new PdfPageBuilder(page).Content(...)` | `document.Page(page => page.Content()...)` |
| `column.Text(...).Add()` | `container.Text(...)` |
| `column.Table(...).Add()` | `container.Table(...)` |
| coordinate-based images/charts | `container.Image`, `container.Chart` |

Legacy terminal `Add()` methods are still functional but issue `PDFB001` through `PDFB007` warnings. Migrate new code first; do not suppress these warnings globally. PdfBuilder will not remove legacy APIs in this pre-release.

Before and after migration, validate representative PDFs with the independent validation suite and inspect layout traces for complex documents.
