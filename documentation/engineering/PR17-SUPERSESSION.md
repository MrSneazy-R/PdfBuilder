# PR 17 supersession record

This record compares draft [PR #17](https://github.com/MrSneazy-R/PdfBuilder/pull/17), `codex/pr19-components-theming` at `ad1c013`, with master at `d10b5cc`. The branches diverged from `3744311`; PR #17 is not to be merged.

## Functionality now on master

Current master contains canonical replacements for the useful PR #17 foundation:

- `IPdfComponent` and `IPdfComponent<TModel>` with reusable container composition;
- `PdfTemplate<TModel>` with fresh-document byte and stream generation;
- document-scoped themes configured through `DocumentThemeBuilder`;
- named colours, named text styles, page defaults, and named spacing storage;
- component reuse across documents and concurrent template generation;
- named styles for normal text and table-cell text;
- page theme cloning; and
- component cycle detection, nesting limits, component-path diagnostics, and preserved inner exceptions.

Roadmap PR 20 completes the remaining canonical usability: cancellation-aware typed-template file saving, a real typed Invoice template, named spacing consumption, complete colour-token coverage, and stronger component/theme tests.

## Functionality moved to Roadmap PR 21

The stale branch's direct canonical text-colour method and any broader canonical typography surface belong with Roadmap PR 21. PR 21 will reconcile them with the typography work retained for reference in draft [PR #7](https://github.com/MrSneazy-R/PdfBuilder/pull/7), including rich text, decorations, wrapping, direction, font fallback, and related text APIs. PR 20 does not cherry-pick those typography changes.

## Exact differences intentionally left unmerged

- PR #17 exposes `IDocumentDescriptor.Theme(Action<DocumentTheme>)` and a publicly mutable theme. Master keeps `Action<DocumentThemeBuilder>` so components cannot obtain or mutate the theme object.
- PR #17 combines components, templates, theme state, and a public `TextStyle` into `ComponentsAndTheming.cs`. Master keeps focused `Components.cs`, `PdfTemplate.cs`, and `DocumentTheme.cs` types and uses the existing `TextStyleDefaults` model internally.
- PR #17 uses `ConfigureDefaultTextStyle`, `SetSpacing`, and `SpacingTheme.Get`. Master keeps the canonical builder spellings `DefaultTextStyle`, `Spacing`, and token overloads on composition descriptors.
- PR #17 labels components with `DebugLabel` but has no recursive-cycle or nested exception-path safety. Master keeps the explicit `PdfComponentCompositionException` model.
- PR #17's direct `ITextStyleDescriptor.Color` change is intentionally deferred to Roadmap PR 21; PR 20 resolves named text colours through named styles.
- PR #17's old documentation and test files use the superseded API and are not copied. Their canonical replacements are `documentation/Components_Templates_and_Themes.md` and `ComponentsAndThemeTests.cs`.

PR #17 may be closed as superseded after this record and the Roadmap PR 20 implementation are retained. Its branch must not be deleted without owner approval. PR #7 remains open only as a typography reference until Roadmap PR 21 is ready; its branch must also remain intact unless the owner approves deletion.
