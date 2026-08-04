# Production readiness

Status is assessed for `0.1.0-preview.1` at commit `0a06a79` before PR 17 changes.
This is a release-candidate checklist, not a claim of stable production readiness.

| Gate | Status | Evidence or reason |
| --- | --- | --- |
| Public API baseline and XML documentation | PASS | Public API analyzers, shipped/unshipped baselines, and XML docs are enabled. |
| Windows Debug/Release build | PASS | Local .NET 10 build succeeds after PR 17 warning fixes. |
| Zero maintained-project warnings | PENDING | Requires the full CI matrix after the warning fixes. |
| Windows package consumer | PENDING | Configured in CI; must run on this PR. |
| Ubuntu build, package consumer, qpdf, pdftotext, and visual validation | PENDING | Required Linux CI jobs install qpdf/Poppler; not executable locally on Windows. |
| macOS build and tests | PENDING | Configured in CI; native rendering must be proven by the macOS job. |
| Typography corpus | PENDING | Covered by existing tests/fixtures; requires cross-platform CI evidence. |
| Table corpus including 1,000 rows | PENDING | Existing table and stress tests require CI evidence and an explicit production fixture. |
| Image and SVG corpus | PENDING | Existing media tests require cross-platform CI evidence. |
| Visual regression | PENDING | Linux Poppler baselines must pass in CI. |
| Concurrency, cancellation, and native lifetime stress | PENDING | Unit stress tests exist; CI evidence is required. |
| Deterministic output and streaming bounds | PENDING | Existing tests exist; release validation must retain results. |
| Production business fixture suite | FAIL | Sanitised reference fixtures exist, but the complete invoice/credit-note/statement/report suite is not yet implemented. |
| Benchmark baseline | PENDING | Scheduled/manual BenchmarkDotNet workflow exists; no stable checked-in numerical baseline yet. |
| Approved public licence | FAIL | Owner approval has not been recorded; public NuGet publication remains blocked. |
| Public stable/RC label | FAIL | The failed/pending gates require a `0.1.0-preview.1` package, not `1.0.0-rc.1`. |

## Release decision

Generate a private, draft-release `0.1.0-preview.1` package only. Do not mark it
stable, do not publish it to NuGet.org, and do not promote it to a release candidate
until every pending gate has evidence and the two failed gates are resolved.

## Required evidence before 1.0.0-rc.1

1. Green Windows, Ubuntu, and macOS CI including package consumers.
2. Green independent structural, extraction, and visual validation on Linux.
3. Sanitised production fixtures for invoices, credit notes, statements, management,
   multilingual, image-heavy, table, section, and navigation documents.
4. Reproducible benchmark numbers and concurrency/native-resource stress evidence.
5. An owner-approved licence and explicit publication approval.
