# Font catalogue

`FontCatalog` is the process-level, thread-safe registry used by HarfBuzz shaping. Register fonts before creating a document.

## Registration

```csharp
FontCatalog.RegisterFont(fontBytes, "Corporate Sans");
FontCatalog.RegisterFont(fontStream, "Corporate Sans Italic");
FontCatalog.RegisterFontFile("assets/CorporateSans-Regular.ttf", "Corporate Sans");
FontCatalog.RegisterFontDirectory("assets/fonts", SearchOption.AllDirectories);
```

`RegisterFontDirectory` accepts `.ttf`, `.otf`, `.ttc`, and `.otc` files and processes paths in ordinal order. The default per-font guardrail is 64 MiB and can be changed through `MaximumFontFileBytes`. Streams remain owned by the caller.

No font binaries should be committed to this repository. Applications are responsible for font licences and deployment.

## Fallback and strict matching

```csharp
FontCatalog.SetFallbackFonts("Noto Sans", "Noto Sans Arabic", "Noto Sans CJK SC");
FontCatalog.StrictMatching = true;
```

Per-style fallback families take precedence over the global chain. Strict matching is opt-in and throws `FontNotFoundException` when a requested family or glyph cannot be resolved. Non-strict matching records the selected platform fallback through `FontDiagnostics`; missing glyphs and full-font subset fallbacks are retained in `FontDiagnostics.RecentMessages`.

## Immutable generation snapshots

`FontCatalog.CaptureSnapshot()` returns a versioned, immutable view of registered faces, fallback order, and strictness. `PdfDocument.Create` captures one snapshot and uses it from composition through serialization. Cache keys include the snapshot version, so later registrations do not reuse stale family matches and do not alter a generation already in progress.
