# Independent PDF validation

`PdfBuilder.ValidationTests` validates sanitised generated fixtures without using PdfBuilder's parser or renderer to decide success.

On Linux CI, the harness uses `qpdf --check` for PDF structure, `pdftotext` for independent extraction, and Poppler's `pdftoppm` at 96 DPI for raster output. Each fixture is declared in `FixtureManifest.json` with coverage, page count, extracted markers, and visual-comparison status.

Visual comparisons allow a maximum per-channel difference of 18 and no more than 0.2% changed pixels. The Ubuntu Poppler runner uses a documented 0.6% threshold for its Linux baseline because its repeatable anti-aliasing variance exceeds the default tolerance; both thresholds still detect meaningful layout or drawing changes. Fixture metadata is fixed so timestamps do not change the generated output.

Approved PNGs are small, sanitised fixtures under `tests/PdfBuilder.ValidationTests/Baselines/`. The root set is the default baseline; narrowly scoped Linux overrides live in `Baselines/linux/` where the pinned CI rasterisation differs by more than the documented tolerance. When a visual comparison fails, the actual raster and a magenta difference image are written to `PDFBUILDER_VISUAL_FAILURE_DIRECTORY` and uploaded by CI. Missing local validator tools skip independent checks with an explicit reason; Linux CI installs the tools and cannot skip them.

PR 03 restores the SkiaSharp and HarfBuzz native assets through NuGet on Ubuntu, so this is a required Linux validation gate. All validator-tool and generated-PDF failures fail CI.

To deliberately approve a new sanitised baseline, set both `PDFBUILDER_APPROVE_VISUAL_BASELINES=true` and `PDFBUILDER_APPROVED_BASELINE_DIRECTORY` outside CI. Normal and CI test runs never create or accept baselines automatically.
