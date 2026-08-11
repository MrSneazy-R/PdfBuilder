# Changelog

All notable changes to PdfBuilder are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the intended versioning
policy is [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- A shared canonical typography surface for ordinary text, rich spans, theme styles,
  table cells, headers/footers, and practical chart-label styling.
- Flow-aware rich-text pagination, logical RTL extraction with PDF `ActualText`, and
  wrapping, hyphenation, ellipsis, maximum-line, decoration, and baseline-shift controls.
- Thread-safe byte, stream, file, and directory font registration with deterministic
  fallback order, strict missing-font failures, size guardrails, retained diagnostics,
  versioned cache keys, and immutable per-document catalogue snapshots.

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
