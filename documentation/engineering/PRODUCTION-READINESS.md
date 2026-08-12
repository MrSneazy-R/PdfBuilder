# Production readiness

Status is reconciled for project version `0.1.0-preview.2` on `codex/pr37-internal-rc`, descended from the checked-in production corpus and benchmark baseline through `0d3abf4`. This is an evidence register for a pre-release package. It is not a claim of stable production readiness or a 1.0 release candidate.

The latest retained external matrix evidence remains [CI run 30921263250](https://github.com/MrSneazy-R/PdfBuilder/actions/runs/30921263250) for ancestor `5f626a3`. It proves the historical Windows, Ubuntu, macOS, qpdf/Poppler, and package-consumer jobs listed below, but it does not prove the exact PR 37 commit. Local Windows verification for the roadmap line is recorded in [INTERNAL-RC-EVIDENCE.md](INTERNAL-RC-EVIDENCE.md). A new retained run is mandatory before any release decision.

| Gate | Status | Retained evidence or unresolved reason |
| --- | --- | --- |
| Windows build and test | HISTORICAL PASS / EXACT COMMIT PENDING | Run 30921263250 passed `Windows build and test`; the PR 37 commit still needs its own retained run. |
| Ubuntu build and test | HISTORICAL PASS / EXACT COMMIT PENDING | Run 30921263250 passed `Ubuntu build and test`; the expanded fixture and benchmark gates need a retained current run. |
| macOS build and test | HISTORICAL PASS / EXACT COMMIT PENDING | Run 30921263250 passed `macOS build and test`; the PR 37 commit still needs its own retained run. |
| qpdf, Poppler, and visual validation | LOCAL PASS / RETAINED PENDING | All six validation tests passed locally against the 17-fixture corpus, including qpdf, UTF-8 extraction, page counts, and selected visual baselines. CI retention for the exact commit is pending. |
| Windows package consumer | LOCAL PASS / RETAINED PENDING | Clean local .NET 8 and .NET 10 consumers restored the generated package and produced PDFs; exact-commit workflow evidence must be retained. |
| Ubuntu package consumer | HISTORICAL PASS / EXACT COMMIT PENDING | Historical job passed; current .NET 8/.NET 10 package consumer evidence must be retained. |
| Zero-warning maintained build | LOCAL PASS / RETAINED PENDING | Release solution build completed locally with zero warnings; the exact-commit matrix result is pending. |
| Production business fixture corpus | IMPLEMENTED / RETAINED PENDING | Seventeen sanitised deterministic fixtures, declared page counts, extraction markers, visual baselines, and concurrent generation checks are checked in. Ubuntu artifact retention is pending. |
| Checked-in benchmark baseline | PASS | `benchmarks/PdfBuilder.Benchmarks/baselines/windows-x64-net10.json` records all 15 required scenarios and deterministic CI gates. |
| Concurrency, cancellation, deterministic output, and native lifetime | LOCAL PASS / RETAINED PENDING | Unit, validation, fixture-concurrency, cancellation, and deterministic checks pass locally; cross-platform exact-commit evidence remains pending. |
| Canonical maintained samples | PASS | Maintained samples build without direct `PdfWriter`, `PdfElement`, raw table/chart types, `AddElement`, or `System.Drawing` usage. |
| Migration documentation | PASS | The canonical migration guide covers document creation, builder mutation, and compatibility diagnostics `PDFB001` through `PDFB009`. |
| Dependency and third-party notices | PASS | `THIRD-PARTY-NOTICES.md` is packaged, and release preparation generates a CycloneDX dependency SBOM. |
| Approved PdfBuilder licence | FAIL | Owner approval has not been recorded and the package declares no project licence. Public distribution remains blocked. |
| Public stable or 1.0 RC label | FAIL | `0.1.0-preview.2` remains a preview. The release workflow rejects stable and `1.0.0-rc.*` labels. |

## Known performance evidence

The checked-in Windows baseline records a compact 107,497-byte, 48-page result for the 1,000-row table, but approximately 58.4 GB of cumulative managed allocation during that measurement. This does not indicate PDF byte bloat or retained page-stream growth (`MaximumRetainedStreams` remains 1), but it is a material optimization target and must remain visible in baseline reviews.

## Release decision

**BLOCKED.** Local `.nupkg`, `.snupkg`, CycloneDX SBOM, and SHA-256 files may be generated for controlled evaluation. The manual workflow may create an unpublished draft for version `0.1.0-preview.2`; it does not publish to NuGet.org and must not use a stable or `1.0.0-rc.*` label.

Do not promote this package to controlled production use until a retained green run exists for the exact release commit and the repository owner records an approved PdfBuilder licence and explicit release decision.
