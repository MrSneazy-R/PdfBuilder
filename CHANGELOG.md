# Changelog

All notable changes to PdfBuilder will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project intends to follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Repository baseline documentation and continuous integration coverage.
- Reusable typed and untyped PDF components with cycle-aware diagnostic paths.
- Strongly typed `PdfTemplate<TModel>` generation and a canonical document descriptor.
- Document-scoped themes with named colors, text styles, and spacing tokens.
- Central PDF string, name, date, and URI encoding.
- Deterministic generation options and stable trailer document identifiers.

### Changed

- Content-stream compression is enabled by default; readable streams remain available through `ReadableContentStreams`.
- The solution now includes maintained tests and samples and targets .NET 10 throughout.
