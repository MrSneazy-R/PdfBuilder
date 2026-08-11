# Serializer encoding audit

Roadmap PR 20 audited caller-controlled PDF strings and names on master at `d10b5cc`. The existing central encoders are sufficient; no second serializer abstraction is introduced.

| Caller-controlled value | Writer call site | Central handling |
| --- | --- | --- |
| title, author, subject, keywords, creator, producer | `PdfWriter` information dictionary | `PdfStringEncoder.Encode` |
| external URI actions | `AnnotationWriter` | `PdfStringEncoder.Encode` |
| outline titles | `OutlineWriter` | `PdfStringEncoder.Encode` |
| base-14 font names | `PdfWriter` | `PdfNameEncoder.Encode` |
| embedded font `/FontName` and `/BaseFont` values | `FontResourceWriter` | `PdfNameEncoder.Encode` |
| caller-supplied stream dictionary keys | `PdfStreamWriter.WriteStream` | `PdfNameEncoder.Encode` |
| creation and modification dates | `PdfWriter` | `PdfDateEncoder.Encode` |
| trailer/document IDs | `PdfWriter.BuildDocumentId` and `PdfStreamWriter.WriteXRefAndTrailer` | SHA-256 converted to fixed uppercase hexadecimal before insertion into a PDF hex string |

The audit found no remaining direct interpolation of caller-controlled metadata, URI, outline-title, font-name, or PDF-name text into writer syntax. Numeric layout values and internally generated resource identifiers are formatted separately and are not PDF string/name inputs.

Regression coverage includes parentheses, backslashes, all PDF literal control escapes, non-Latin metadata, non-Latin outline titles and URIs, malformed UTF-16 surrogate input, empty values, long metadata bounded by `MaximumOutputBytes`, date offsets, unsafe UTF-8 PDF-name bytes, and deterministic trailer IDs. Compressed-default and readable-debug content streams remain covered independently.
