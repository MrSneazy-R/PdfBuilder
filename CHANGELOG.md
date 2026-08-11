# Changelog

## Unreleased

- Added opt-in tagged-PDF semantics with canonical document/section/heading/paragraph/list/table/header/footer/figure/caption/link roles, alternative text, decorative artefacts, explicit reading order, marked content and MCIDs, structure and parent trees, role mapping, and link-annotation association. This is not a PDF/UA conformance claim.
- Added the isolated `PdfBuilder.Operations` package for bounded, cancellation-aware qpdf inspection, selection, extraction, merge, split, overlay, underlay, attachments, encryption/permissions, authorised decryption, and linearisation without shell invocation.
- Added a separate loopback-only preview and diagnostics host with `dotnet watch`, thumbnails, selected-page previews, PDF download, hierarchy/trace/timing panels, structured errors, debug labels, margins, flow guides, and clean/diagnostic render switching without telemetry.
- Prepared blocked internal pre-release artifacts with canonical maintained samples, packaged third-party notices, CycloneDX SBOM generation, SHA-256 manifests, strict preview-version/tag validation, and an exact-commit evidence checklist; public or 1.0 RC publication remains blocked by retained CI and licence approval.
- Established a checked-in .NET 10 Windows benchmark baseline for 15 generation scenarios, with deterministic CI gates, isolated scheduled captures, timing comparisons, allocation/output/resource metrics, and retained hardware/runtime metadata.
- Multi-targeted the package for .NET 8 and .NET 10, with clean consumer checks for both frameworks and package archive validation for assemblies, XML documentation, portable symbols, and repository metadata.
- Added a sanitised deterministic production fixture corpus covering business documents, multilingual shaping, image-heavy output, 1,000-row and split-row tables, navigation, repeated-content variants, concurrent batches, and serializer edge cases, with retained page counts, extraction markers, selected visual baselines, and CI PDF artifacts.
- Added `Debug`, `Balanced`, `SmallFile`, `PrintQuality`, and `Deterministic` output presets while preserving compressed-by-default and readable-debug output.
- Added PDF 1.4 through 2.0 header selection, document language, validated custom XMP, and an explicit stable trailer identifier.
- Exposed read-only generation metrics and enforced output-size limits for byte-array, stream, and file generation.
- Changed `PdfDocument.Pages` and `PdfPage.Elements` to read-only views. Obsolete `MutablePages` (`PDFB008`) and `MutableElements` (`PDFB009`) shims retain an explicit migration path for legacy direct mutation.
- Strengthened deterministic resource ordering and resource-rich output regression coverage.

All notable changes to PdfBuilder are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the intended versioning
policy is [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- An available-size-aware canonical canvas with isolated graphics state, finite transforms,
  clipping, dashed and dotted strokes, bounded vector gradients and shadows, deterministic
  background/content/foreground layers, dynamic SVG, and render-limit integration.

- Thread-safe `ImageSource` loading from bytes, read-only memory, streams, local files,
  embedded resources, reusable preloaded instances, and caller-owned lazy factories; DPI-aware
  intrinsic sizing, crop alignment, quality controls, effective-DPI downsampling, JPEG quality,
  alpha-aware encoding, EXIF orientation, content-hash deduplication, and still WebP decoding.
- XML-based SVG sanitisation that prohibits DTDs, active content, event handlers, imported styles,
  and external network/file references while retaining safe local paint references.
- A PdfColor-only canonical chart model with line, area, grouped/stacked/100%-stacked bar,
  pie, donut, and scatter series; typed series options; numeric and secondary axes; labels,
  formatters, markers, smoothing, legends, and document-scoped theme palettes.

- Canonical table cells now implement the normal `IContainer` surface, so rich text,
  images, SVG, barcodes, nested layouts, layers, and reusable components share the
  ordinary measurement, rendering, theme, resource, and diagnostic paths inside cells.
- Canonical tables now support explicit row/column placement, row and column spans,
  header/body/footer groups, explicit footer repetition modes, constrained column modes,
  continuous banding, collapsed and independent borders, row pagination controls, and
  stable continuation widths.
- Opt-in controlled continuation for oversized canonical table rows, with structured
  failure diagnostics for unsplittable content and unsupported spans, defined continuation
  edges, repeated-group preservation, and bounded zero-progress termination.
- Immutable final-pagination page context and canonical current-page/total-page text tokens
  with conservative measurement, bounded stabilization, and deterministic parallel generation.
- A shared canonical typography surface for ordinary text, rich spans, theme styles,
  table cells, headers/footers, and practical chart-label styling.
- Flow-aware rich-text pagination, logical RTL extraction with PDF `ActualText`, and
  wrapping, hyphenation, ellipsis, maximum-line, decoration, and baseline-shift controls.
- Thread-safe byte, stream, file, and directory font registration with deterministic
  fallback order, strict missing-font failures, size guardrails, retained diagnostics,
  versioned cache keys, and immutable per-document catalogue snapshots.
- Canonical sections, forward tables of contents, anchors, external and internal links,
  hierarchical outlines, final-pagination page references, URI safety policy, and
  retained broken-target diagnostics.
- Deterministic page-aware visibility for first, last, odd, even, one-time, and
  continuation content, including explicit first-page and continuation header/footer variants.
- A canonical Phase 1 report sample combining final page numbers, forward TOC entries,
  links, outlines, and repeated-content variants without raw element usage.

### Changed

- Split canonical chart rendering into focused layout, scales, ticks, axes, legends,
  drawing/labels, and typed-series renderers while retaining the legacy raw chart path.
- Split the canonical public contracts and private composition adapters into focused
  `Document/Api` and `Document/Canonical` source files without changing signatures or output.

## [0.1.0-preview.2] - 2026-08-11

### Added

- Canonical document composition, reusable components, theming, page composition,
  typography, flowing tables, media, diagnostics, deterministic serialization,
  streaming resources, validation fixtures, and rendering limits.
- A manual-only release-candidate workflow that creates package artefacts and a draft
  GitHub release without publishing to NuGet.org.
- Typed-template file saving, usable named spacing tokens, complete canonical colour-token
  resolution, and retained serializer-encoding audit coverage.

### Changed

- The package is versioned `0.1.0-preview.2` because the documented v1
  production gates are not all proven yet.
