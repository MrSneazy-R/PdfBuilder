PdfBuilder exposes a fluent API for declarative PDF composition. Start with `PdfDocumentBuilder` to configure document-wide settings, then use the composer DSL to lay out content with automatic pagination, table rendering, and HarfBuzz-backed text shaping.

Key entry points covered in this documentation set:
1. `PdfDocumentBuilder` and document-level configuration
2. `DocumentComposer` and `PageComposer` for page orchestration
3. Flow composition via `ColumnBuilder` and `ContentComposer`
4. Element builders (`TextBuilder`, `RichTextBuilder`, `ImageBuilder`, `TableBuilder`, `ListBuilder`, `ChartBuilder`, `CanvasBuilder`)
5. Output services (`PdfWriter`, `FontCatalog`) and supporting models

Each file in this folder documents a public surface area with usage notes, code examples, and the expected PDF outcome.
