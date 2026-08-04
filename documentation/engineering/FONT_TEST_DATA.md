# Font test-data strategy

PdfBuilder does not commit font binaries and does not package fonts as library content. Typography tests use platform fonts for base-14 compatibility and exercise the multilingual code path through the Noto font families installed by the Linux CI image when present.

Any future committed font fixture must be small, sanitised, redistributable, and recorded in `THIRD-PARTY-NOTICES.md`. The preferred family is Noto Sans, Noto Sans Arabic, and Noto Sans CJK under the SIL Open Font License 1.1. Fixtures must contain no business text or private branding.

The text corpus deliberately covers accented Latin, combining marks, Arabic, Hebrew, mixed RTL/LTR text, Devanagari, Chinese, Japanese, Korean, ligatures, fallback selection, and monochrome symbols. Platform availability is a diagnostic rather than a reason to silently replace a requested font: strict matching turns missing fonts and glyphs into `FontNotFoundException`.
