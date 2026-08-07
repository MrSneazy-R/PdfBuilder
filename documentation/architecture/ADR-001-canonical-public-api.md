# ADR-001: Canonical public API

## Status

Accepted.

## Decision

PdfBuilder exposes `PdfDocument.Create` as the canonical entry point. Documents are composed through document, page, container, column, row, and text descriptors. The descriptors defer into the existing `ContentComposer` component tree and existing writer.

## Consequences

New consumers need no PDF coordinates, `PdfElement`, writer construction, or `Add()` finalisation. Existing builders and document APIs remain supported. This decision deliberately does not add tables, media, headers/footers, or a second rendering pipeline.
