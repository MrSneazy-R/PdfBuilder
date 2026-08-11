# PR #7 supersession record

Stale PR #7 (`codex/pr07-typography-fonts`) is reference material only. It must not be merged or cherry-picked.

Roadmap PR 21 recreates and extends its useful behaviour on the current components/themes/serializer foundation:

- byte-array and stream font registration;
- file and directory registration;
- deterministic fallback ordering, strict matching, and `FontNotFoundException`;
- versioned typeface cache keys;
- canonical rich-text spans;
- expanded canonical typography descriptors;
- HarfBuzz shaping, subsetting, ToUnicode extraction, and bloat regression coverage.

PR #7 behaviour intentionally omitted or replaced:

- its global mutable generation view is replaced by immutable document snapshots;
- its normal-text-only no-wrap/ellipsis flags are replaced by shared style tokens used by normal, rich, themed, and table text;
- its small test-only rich-text surface is replaced by flow-aware, paginating canonical rich text;
- no `THIRD-PARTY-NOTICES.md` font licence assertion is copied, because no font binary is committed and application fonts remain the consumer's licensing responsibility;
- canonical link methods remain deferred to Roadmap PR 24 rather than copying the old link-bearing span API into the new surface.

PR #7 may be closed as superseded only after the Roadmap PR 21 replacement pull request is open and linked in its closing comment. Its branch must not be deleted without owner approval.
