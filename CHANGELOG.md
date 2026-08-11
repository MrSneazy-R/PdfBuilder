# Changelog

All notable changes to PdfBuilder are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the intended versioning
policy is [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Canonical table cells now implement the normal `IContainer` surface, so rich text,
  images, SVG, barcodes, nested layouts, layers, and reusable components share the
  ordinary measurement, rendering, theme, resource, and diagnostic paths inside cells.
- Canonical tables now support explicit row/column placement, row and column spans,
  header/body/footer groups, explicit footer repetition modes, constrained column modes,
  continuous banding, collapsed and independent borders, row pagination controls, and
  stable continuation widths.
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
