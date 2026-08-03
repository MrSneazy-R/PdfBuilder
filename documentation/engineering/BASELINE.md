# Engineering baseline

This is the initial production-hardening record for PdfBuilder. It describes the committed baseline, not local uncommitted changes.

## Repository snapshot

- Commit: `38499f18e888b2723aaf259867f952e49d424aab` (`Massive changes`)
- Solution projects: `PdfBuilder.sln` contains one project: `PdfBuilder`.
- Additional projects not in the solution: `tests/PdfBuilder.Tests`, `samples/SimpleRenderCheck`, `samples/TablePerfSample`, and `samples/HBSubsetProbe`.
- Target frameworks: the library, tests, and samples target `net9.0`.
- Package dependencies: `SkiaSharp` 2.88.6, `SkiaSharp.HarfBuzz` 2.88.6, `SkiaSharp.Svg` 1.60.0, `HarfBuzzSharp` 7.3.0, and `ZXing.Net` 0.16.9. Tests additionally use xUnit 2.7.0, xUnit VS runner 2.5.7, Microsoft.NET.Test.Sdk 17.11.1, and FluentAssertions 6.12.0.

## Environment and command results

Baseline collection machine: Windows 11 x64 with .NET SDK `9.0.316` and runtime `9.0.18`. The source targets .NET 9. A previously installed .NET 10 SDK was retained separately and was not used for the final validation.

| Command | Result |
| --- | --- |
| `dotnet restore PdfBuilder.sln` | Passed |
| `dotnet build PdfBuilder.sln -c Debug` | Passed, 4 warnings |
| `dotnet build PdfBuilder.sln -c Release` | Passed, 4 warnings |
| `dotnet test tests/PdfBuilder.Tests/PdfBuilder.Tests.csproj -c Release` | Passed: 74 passed, 0 failed, 0 skipped |
| `dotnet format PdfBuilder.sln --verify-no-changes` | Existing violations were normalized in this baseline PR; verification now passes. |
| `dotnet pack PdfBuilder.csproj -c Release -o ./artifacts` | Passed; package warns that it has no package readme. |

The test project contains 74 `[Fact]` tests across 18 test files. It is not included in `PdfBuilder.sln`, so `dotnet test PdfBuilder.sln` does not run those tests. CI invokes the test project explicitly on .NET 9.

### Existing warnings

1. `Document/ColumnBuilder.cs(47)`: CS8618, `_layoutOptions` is not initialized.
2. `Document/Layout/TableColumnWidthCalculator.cs(235)`: CS0162, unreachable code.
3. `Writer/PdfWriter.cs(462)`: CS8604, possible null argument to `TryRgb`.
4. `Writer/PdfWriter.cs(468)`: CS8604, possible null argument to `TryRgb`.

## Package and sample output

- Current package: `PdfBuilder.1.0.0.nupkg`; it was produced successfully during baseline packaging at 245,255 bytes (239.5 KiB). CI retains the package artefact for subsequent comparisons.
- There are no committed sample PDFs. Generated PDFs and `artifacts/` are ignored, so no stable sample-PDF byte-size baseline exists yet.
- `SimpleRenderCheck` produces one basic text PDF; `TablePerfSample` produces Latin/Arabic and CJK table PDFs with timing output; `HBSubsetProbe` inspects HarfBuzz APIs. None is currently run automatically.
- Existing tests include structural PDF and text extraction helpers, but no committed visual-regression fixture or automated performance threshold.

## Platform status

Windows is the required CI job. WebP decoding is implemented through Windows Imaging Component and explicitly throws `PlatformNotSupportedException` on non-Windows platforms. Font subsetting imports `libHarfBuzzSharp`; a missing native library or subset entry point falls back to embedding the full font. The `ubuntu-native-runtime-diagnostics` CI job attempts build and test, then only classifies failures with explicit native-runtime signatures as diagnostics; ordinary failures remain failures.

## Rendering and architecture status

### Current public entry points

The primary public construction and output types are `PdfDocument`, `PdfDocumentBuilder`, `PdfPageBuilder`, `DocumentComposer`, `ContentComposer`, `TextBuilder`, `TableBuilder`, `ImageBuilder`, `ChartBuilder`, and `PdfWriter`. The minimal package example is in the root README.

### Layout paths

`PdfDocumentBuilder` and `DocumentComposer` create document models. `ContentComposer` and `Document/Layout/Components` measure and draw layout components. `TablePaginator` and the table layout helpers split pages and resolve column widths. `PdfWriter` renders pages through `Writer/Renderers` and writes the PDF object graph through `PdfStreamWriter` and `PdfResourceManager`.

### HarfBuzz, images, and tables

- **HarfBuzz:** `TextShaper` uses `SkiaSharp.HarfBuzz` for shaped runs. Glyph runs are encoded through `GlyphRunEncoder`; embedded-font resources try native HarfBuzz subsetting and otherwise embed complete fonts. This fallback is a known PDF-size risk and has not been changed in this PR.
- **Images:** JPEG, PNG, WebP inspection/decoding, SVG, barcodes, clipping, and transparency paths exist. WebP remains Windows-only because it uses WIC.
- **Tables:** `TableBuilder`, `TableElement`, `TableRenderer`, `TableColumnWidthCalculator`, and `TablePaginator` support table layout, pagination, rich text styling, borders, and banding. Existing tests cover table columns and rendering; no behavior was changed here.

## Known risks and deferred work

- PDF size and content-stream bloat, particularly where font subsetting falls back to full-font embedding, remain unmeasured and unresolved.
- Cross-platform native dependency behavior requires CI evidence; no general Ubuntu support claim is made yet.
- The existing warnings, full whitespace cleanup, package readme metadata, visual regression fixtures, and performance budgets are intentionally deferred to later PRs.
