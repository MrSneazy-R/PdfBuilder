# Independent PDF validation

`PdfBuilder.ValidationTests` validates sanitised generated fixtures without using PdfBuilder's parser or renderer to decide success.

On Linux CI, the harness uses `qpdf --check` for PDF structure, `pdftotext` for independent extraction, and Poppler's `pdftoppm` at 96 DPI for raster output. Each fixture is declared in `FixtureManifest.json` with coverage, page count, extracted markers, and visual-comparison status.

Visual comparisons allow a maximum per-channel difference of 18 and no more than 0.2% changed pixels. This tolerates minor rasteriser anti-aliasing variance while detecting meaningful layout or drawing changes. Fixture metadata is fixed so timestamps do not change the generated output.

Approved PNGs are small, sanitised fixtures under `tests/PdfBuilder.ValidationTests/Baselines/`. When a visual comparison fails, the actual raster and a magenta difference image are written to `PDFBUILDER_VISUAL_FAILURE_DIRECTORY` and uploaded by CI. Missing local validator tools skip independent checks with an explicit reason; Linux CI installs the tools and cannot skip them.

The current net9 baseline does not restore `libSkiaSharp` on Ubuntu, so the Ubuntu independent-validation job is explicitly named a native-runtime diagnostic. It invokes the validator suite and only classifies the known `libSkiaSharp` load failure as diagnostic; all other failures remain failures. PR 03 owns native runtime dependency alignment, after which this job becomes a normal cross-platform validation gate.

To deliberately approve a new sanitised baseline, set both `PDFBUILDER_APPROVE_VISUAL_BASELINES=true` and `PDFBUILDER_APPROVED_BASELINE_DIRECTORY` outside CI. Normal and CI test runs never create or accept baselines automatically.
