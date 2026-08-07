# Repository Guidelines

## Project Structure & Module Organization
- `Document/`, `Builders/`, `Elements/`, `Writer/`, `Imaging/`, `Encoder/`, `Models/` (e.g., `Writer/Renderers/TextRenderer.cs`, `Writer/PdfStreamWriter.cs`, `Writer/PdfResourceManager.cs`, `Document/PdfDocument.cs`)
- Entry project: `PdfBuilder.csproj` (targets `net10.0`). Solution: `PdfBuilder.sln`.
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
- Tests live under `tests/` with `*.Tests.csproj` targeting `net10.0`.
- Use `xUnit` with `FluentAssertions`. Name tests `ClassName_Scenario_ExpectedBehavior`.
- Aim for coverage of renderers, **layout utilities**, pagination, and PDF dictionary assembly.

## Commit & Pull Request Guidelines
- Use Conventional Commits: `feat:`, `fix:`, `refactor:`, `perf:`, `docs:`, `test:`.
- Keep commits scoped and descriptive (imperative mood). Link issues with `Fixes #123` when applicable.
- PRs must include: purpose, summary of changes, screenshots/PDF samples if output changes, and breaking-change notes.

## Security & Configuration Tips
- Requires the .NET 10 SDK pinned in `global.json`: verify with `dotnet --version`.
- Imaging may rely on OS codecs (e.g., WIC on Windows). Avoid bundling native binaries.
- Do not commit secrets or sample PDFs with sensitive data.

---

## Agent-Specific Instructions (Document layout & pagination)
- Problem
  - Decoded Pdf shows alot of maybe corrupt data, with current harfbuzz integration 1 line of normal text rendering shows almost 20k lines of code behind pdf
- Goals
  - Fix the bloated Pdf Issue
  - verify the Harfbuzz integration that it works fully as intended

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

## Acceptance Criteria
- No Bloated pdf
- Pdf's Generate fast if there is little items to render
- Harfbuzz integration is verified and working as intended
