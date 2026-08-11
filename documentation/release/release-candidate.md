# Release candidate guide

## Package status

`0.1.0-preview.2` is a pre-release package. It is intentionally not labelled
`1.0.0-rc.1`: the production-readiness report records the gates still awaiting
evidence. The package is suitable only for controlled evaluation until those gates
are passed and the repository owner approves a licence.

## Upgrade guide

New applications should use `PdfDocument.Create` and the `IContainer` composition
API. Legacy builders remain functional but are obsolete with `PDFB00x` diagnostics.
Replace terminal `Add()` calls with the canonical terminal operation, for example
`container.Text("Hello").Bold()`. See
[legacy-to-canonical-api.md](../migration/legacy-to-canonical-api.md) for mappings.

The package targets `net8.0` and `net10.0`. Release validation must retain clean consumer runs for both target frameworks and confirm the `.nupkg`/`.snupkg` contain both assemblies, XML documentation, portable PDBs with SourceLink data, and repository metadata.
The package also contains `THIRD-PARTY-NOTICES.md`; the draft workflow emits a CycloneDX dependency SBOM and a SHA-256 manifest covering the package, symbols, and SBOM.

## Supported platforms and formats

The maintained targets are .NET 8 and .NET 10. CI covers Windows, Ubuntu, and macOS. PNG, JPEG, and
still WebP use the shared Skia pipeline; WebP must not be released until its tests have a
retained green run on all three platforms. Inline SVG is parsed with DTDs disabled,
sanitised, and prevented from resolving network or file resources.
QR Code and Code 128 are supported. The package does not claim PDF/A, PDF/UA,
encryption, signatures, forms, HTML rendering, or PDF merging support.

## Security model

Apply caller-owned cancellation tokens and configure `PdfDocument.RenderLimits` for
untrusted workloads. Image dimensions and decoded pixels are bounded before resource
embedding; SVG scripts and external resources are blocked. See
[rendering-limits.md](../security/rendering-limits.md). Do not submit private PDFs,
fonts, credentials, or customer data in issues or security reports.

## Performance and support policy

BenchmarkDotNet reports are produced by the scheduled/manual benchmark workflow;
see [performance.md](../engineering/performance.md). The project currently supports
the latest pre-release development line only. Security reports are handled under
[SECURITY.md](../../SECURITY.md); no response SLA beyond that policy is promised.

## Versioning and publication

Semantic Versioning is the intended policy. Pre-release identifiers signal that
breaking changes remain possible before 1.0. The `Internal pre-release draft` workflow
is manual, creates `.nupkg`, `.snupkg`, a CycloneDX SBOM, and SHA-256 checksums, and
attaches them to a draft GitHub release. It rejects versions that differ from the
project file, stable versions, and `1.0.0-rc.*` labels while mandatory gates remain
unresolved. It never publishes to NuGet.org. Public publishing is blocked until the
owner approves a licence; private/internal distribution remains an owner decision.

Build and consume the current private package with the project version unchanged:

```bash
dotnet pack PdfBuilder.csproj -c Release -p:Version=0.1.0-preview.2 -o ./artifacts
dotnet add package PdfBuilder --version 0.1.0-preview.2 --source ./artifacts
```

## Known limitations

See [PRODUCTION-READINESS.md](../engineering/PRODUCTION-READINESS.md) for the
authoritative gate table. In particular, a package version alone is not evidence that
cross-platform visual, native-concurrency, or production-fixture gates have passed.
The current release decision remains `BLOCKED` because exact-commit retained CI and an approved PdfBuilder licence are missing.
