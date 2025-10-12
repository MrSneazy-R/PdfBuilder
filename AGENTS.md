# Repository Guidelines

## Project Structure & Module Organization
- `Document/`, `Builders/`, `Elements/`, `Writer/`, `Imaging/`, `Encoder/`, `Models/` (e.g., `Writer/Renderers/TextRenderer.cs`, `Writer/PdfStreamWriter.cs`, `Writer/PdfResourceManager.cs`, `Document/PdfDocument.cs`)
- Entry project: `PdfBuilder.csproj` (targets `net9.0`). Solution: `PdfBuilder.sln`.
- Assets/samples are git-ignored; add temporary fixtures under `samples/` when needed.

## Build, Test, and Development Commands
- Build library: `dotnet build PdfBuilder.sln -c Debug`
- Format code: `dotnet format` (run before committing)
- Pack for NuGet: `dotnet pack PdfBuilder.csproj -c Release -o ./artifacts`
- Run analyzers only: `dotnet build -warnaserror`
- Run tests: `dotnet test tests/PdfBuilder.Tests/PdfBuilder.Tests.csproj` (or `dotnet test` at the solution root)

## Coding Style & Naming Conventions
- C#: 4 spaces, UTF-8, `Nullable` enabled, implicit usings on.
- Namespaces match folders; filenames match public types.
- PascalCase: public types/members. camelCase: locals/parameters. `_camelCase`: private fields; prefer `readonly`.
- Prefer immutability, small focused classes, and explicit models over dictionaries.
- Public API changes require rationale and examples in PR description.

## Testing Guidelines
- Tests live under `tests/` with `*.Tests.csproj` targeting `net9.0`.
- Use `xUnit` with `FluentAssertions`. Name tests `ClassName_Scenario_ExpectedBehavior`.
- Aim for coverage of renderers, **layout utilities**, pagination, and PDF dictionary assembly.

## Commit & Pull Request Guidelines
- Use Conventional Commits: `feat:`, `fix:`, `refactor:`, `perf:`, `docs:`, `test:`.
- Keep commits scoped and descriptive (imperative mood). Link issues with `Fixes #123` when applicable.
- PRs must include: purpose, summary of changes, screenshots/PDF samples if output changes, and breaking-change notes.

## Security & Configuration Tips
- Requires .NET 9 SDK: verify with `dotnet --version`.
- Imaging may rely on OS codecs (e.g., WIC on Windows). Avoid bundling native binaries.
- Do not commit secrets or sample PDFs with sensitive data.

---

## Agent-Specific Instructions (Document layout & pagination)
- Problem
  - Overlapping elements and inconsistent placement indicate column/flow measurement gaps and missing “advance Y” coordination between builders.
- Goals
  - Provide reliable **flow layout** helpers, predictable **column grids**, and safe **Y-position management** to avoid overlap.
  - Make watermark opacity honor `WatermarkSpec.Opacity`.
  - Deepen **table** borders/banding and rich text rendering.

### Page Flow & Columns
- Add a simple layout engine in `Builders/`:
  - `FlowColumn { X, Y, Width, BottomY }` with methods:
    - `Reserve(height)` → advances `Y`, returns rect.
    - `Advance(pixels)` → manual nudge.
    - `SwitchTo(nextColumn)` → returns the next column when current would overflow.
  - `FlowGrid.Create(page, margin, columns, gutter)` → returns `FlowColumn[]`.
- Update `PdfPageBuilder.Content(...)` to expose the active column and a `GetFlow()` accessor so builders can:
  - Read current `Y` and `BottomY`.
  - Reserve vertical space after adding content.
- All high-level builders (`TextBuilder`, `ImageBuilder`, `TableBuilder`, `ChartBuilder`, `ListBuilder`) must:
  - Return the final drawn height.
  - Call `flow.Reserve(height + spacing)` to move the cursor.
  - Respect `BottomY` and request a page break via the existing `OnPageBreak` when needed.

### Watermark Opacity Fix
- Register and apply a reusable ExtGState for watermarks.
- Touch points
  - `Writer/PdfResourceManager.cs` — add `/GSwm` with `/ca {opacity}`, `/CA {opacity}`, `/BM /Normal`.
  - Watermark renderer (master page draw) — wrap with `q /GSwm gs ... Q` and use fill text operators so `/ca` applies.
  - Respect `WatermarkLayer`:
    - `BehindContent`: background → watermark → page content.
    - `AboveContent`: page content → watermark.
- Tests
  - `Watermark_Opacity_GraphicsStateApplied`.
  - `Watermark_Layer_Order_IsRespected`.

### Tables: Borders, Banding, Rich Text
- API
  - `BorderStyle { Color, Width, DashPattern?, DashPhase?, LineJoin?, LineCap?, MiterLimit? }`.
  - Per-side borders on cells: `BorderTop/Right/Bottom/Left` plus optional per-side color/width/style overrides.
  - Table-level `OuterBorder` and `InnerBorder` with independent styles.
  - `BorderCollapseMode`: `Separate` or `Collapse` (resolve conflicts).
  - Corner radius for table and cells (all four corners).
  - Banding:
    - `RowBandingSpec { Step, Fills, BorderOverride? }`.
    - `ColumnBandingSpec { Step, Fills, BorderOverride? }`.
- Cell text styling
  - `TextStyle` supports: `FontFamily`, `FontSize`, `Bold`, `Italic`, `SmallCaps`, `TextColor`, `BackgroundColor`, `HorizontalAlign`, `VerticalAlign`, `LineHeight`, `LetterSpacing`, `WordSpacing`.
  - Decorations: `Underline`, `Strikethrough`, `DecorationColor?`, `DecorationThickness?`, `DecorationStyle (Solid|Dotted|Dashed|Double)`.
  - Positioning: `RotationDegrees`, `Superscript`, `Subscript`.
  - Wrapping: `Wrap`, `NoWrap`, `Hyphenate`, `EllipsisWhenClipped`.
  - `Cell.TextRuns : List<InlineRun>` to mix styles within a cell.
- Rendering rules
  - Paint order: cell background → banding fills → text → inner borders → outer border.
  - Collapse resolution: prefer thicker border; if equal, favor higher z-order (spans) or `Top > Left > Bottom > Right`.
  - Repeat header backgrounds/borders on each page; continue banding index across page breaks.
- Tests
  - `Table_PerSideBorders_Render_Correctly`.
  - `Table_OuterVsInnerBorders_AreIndependent`.
  - `Table_BorderCollapse_PrefersThicker_WhenConflicting`.
  - `Table_RowBanding_PersistsAcrossPages`.
  - `TableCell_Text_Underline_And_Strikethrough_Positioned_Correctly`.
  - `TableCell_RichRuns_MixStyles_And_FallbackFonts`.

### Diagnostics for Layout Work
- Add `LayoutDebug` toggles (env var or flag on `PdfDocumentBuilder`):
  - `DrawBoundingBoxes` — strokes the rect each builder reserved.
  - `ShowFlowGuides` — shows grid/column boundaries and current cursor `Y`.
  - `TraceLayout` — logs reservations and page breaks to console/test output.

## Acceptance Criteria
- No overlapping elements when using `FlowGrid` and builder heights.
- Watermark opacity visibly matches `WatermarkSpec.Opacity`; layer order is correct; graphics state is restored.
- Tables render per-side borders, inner/outer styles, alternating banding, and rich text (including strikethrough) with correct metrics.
- Pagination keeps headers and banding across pages without double-stroking.
- All new and existing tests pass with `dotnet build -warnaserror` and `dotnet test`.
