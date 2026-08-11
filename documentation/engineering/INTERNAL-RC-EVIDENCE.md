# Internal pre-release evidence

This file records local preparation evidence for `0.1.0-preview.2` on `codex/pr37-internal-rc`. It supplements, but never replaces, retained GitHub Actions evidence for the exact commit.

## Local verification completed on 2026-08-11

- Release solution build: zero warnings and zero errors for the multi-targeted library and maintained projects.
- Unit tests: 317 passed on .NET 8 and 317 passed on .NET 10 on the roadmap line.
- Independent validation: 6 passed with qpdf 12.3.2 and Poppler 25.07, covering structure, UTF-8 extraction, exact page counts, and selected visual baselines.
- Production corpus: 17 fixtures generated deterministically; the concurrent batch uses 24 parallel independent compositions.
- Benchmark corpus: 15 checked-in scenarios with timing, allocation, output, page-throughput, retained-stream, resource, font, image, and deduplication metrics; 9 bounded deterministic gates pass locally.
- Maintained samples: raw writer/element API audit is empty after converting the render-check and table-performance samples to the canonical API.
- Package: `.nupkg` and `.snupkg` contain .NET 8/.NET 10 assemblies, XML documentation, portable SourceLink PDBs, repository metadata, README, and third-party notices.
- Package consumers: clean local .NET 8 and .NET 10 console consumers restored the generated package and produced valid PDFs.
- Release artifacts: the local preparation script generated a CycloneDX 1.6 SBOM with 15 NuGet components plus SHA-256 checksums for the package, symbols, and SBOM.
- Sample PDFs: qpdf reported no syntax or stream-encoding errors; Poppler renderings were visually inspected after canonical conversion.

## Evidence still required

- A retained green workflow run for the exact PR 37 commit across Windows, Ubuntu, and macOS.
- Retained Windows and Ubuntu package-consumer jobs for both .NET 8 and .NET 10.
- Retained Ubuntu qpdf, Poppler extraction, fixture PDF artifacts, and visual-comparison artifacts.
- Review of the high 1,000-row allocation baseline and any production-specific performance threshold the owner requires.
- An owner-approved PdfBuilder licence and explicit controlled-production/publication decision.

Until those items are complete, generated artifacts are evaluation-only and the release status is `BLOCKED`.
