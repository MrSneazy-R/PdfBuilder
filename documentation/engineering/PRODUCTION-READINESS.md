# Production readiness

Status is reconciled for project version `0.1.0-preview.2` at current master commit `d10b5cc`. This is a pre-release evidence register, not a claim of stable production readiness or a 1.0 release candidate.

The retained cross-platform evidence is [CI run 30921263250](https://github.com/MrSneazy-R/PdfBuilder/actions/runs/30921263250) for `5f626a3`, which is in the ancestry of current master. [PR #16](https://github.com/MrSneazy-R/PdfBuilder/pull/16) records that all five matrix jobs passed and that Debug/Release builds completed with zero warnings. Later master commits added the canonical component/theme and serializer foundation; their PR 20 changes still require a new retained CI run before their additional assertions can be promoted from pending.

| Gate | Status | Retained evidence or unresolved reason |
| --- | --- | --- |
| Windows build and test | PASS | Run 30921263250: `Windows build and test` passed. |
| Ubuntu build and test | PASS | Run 30921263250: `Ubuntu build and test` passed. |
| macOS build and test | PASS | Run 30921263250: `macOS build and test` passed. |
| qpdf and Poppler validation | PASS | Run 30921263250: the Ubuntu build-and-test job installed qpdf/Poppler and passed independent structural, text-extraction, and visual validation. |
| Windows package consumer | PASS | Run 30921263250: `Windows package consumer smoke test` passed. |
| Ubuntu package consumer | PASS | Run 30921263250: `Ubuntu package consumer smoke test` passed. |
| Zero-warning maintained build | PASS | PR #16 retains the zero-warning Debug/Release result associated with run 30921263250. |
| Formatting | PASS | Formatting ran as a step inside every build-and-test matrix job in run 30921263250. |
| Public API baseline and XML documentation | PASS | Public API analyzers, shipped/unshipped baselines, and XML documentation generation remain enabled on current master. |
| Typography, tables, media, visual regression, concurrency, cancellation, and native lifetime tests | PASS | The maintained suites present at `5f626a3` passed across the retained matrix; Ubuntu retained independent PDF validation. |
| Components, typed templates, theme tokens, deterministic resource-rich generation, and expanded serializer edges | PENDING | Current master contains the foundation; Roadmap PR 20 adds reconciliation coverage. A retained PR 20 matrix run is required before this gate is complete. |
| Complete production business fixture suite | FAIL | The complete invoice, credit-note, statement, management-report, multilingual, image-heavy, table, section, and navigation fixture corpus is not implemented. |
| Checked-in benchmark baseline | PENDING | Scheduled/manual BenchmarkDotNet reporting exists, but no approved numerical baseline is checked in. |
| Approved public licence | FAIL | Owner approval has not been recorded; public NuGet publication remains blocked. |
| Public stable-release label | FAIL | `0.1.0-preview.2` is a preview. It must not be labelled stable or a 1.0 release candidate. |

## Release decision

Only generate a private or local `0.1.0-preview.2` package. Do not publish it to NuGet.org, mark it stable, or promote it to a 1.0 release candidate. The four unresolved gates above remain release blockers; PR 20 additionally requires a retained green CI/package-consumer run for its new coverage.

## Evidence required for any future stable or 1.0 RC label

1. A retained green run for the exact release commit, including the three build-and-test jobs and two package-consumer jobs.
2. Green independent structural, extraction, and visual validation through qpdf and Poppler.
3. The complete sanitised production fixture suite.
4. A reviewed, checked-in numerical benchmark baseline.
5. An owner-approved licence and explicit publication approval.
