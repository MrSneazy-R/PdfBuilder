# PDF/A and PDF/UA validation

`PdfBuilder.Compliance` is an optional package for preparing and validating PDF/A-2b,
PDF/A-3b, and PDF/UA-1 candidates. It does not convert arbitrary PDFs and it never treats an
XMP conformance identifier as proof of conformance.

## Pipeline

The package applies the requested PDF version, BCP 47 document language, profile-specific
XMP identification, synchronized core metadata, and—where PDF/A requires it—a caller-supplied
ICC output intent. It then generates through the ordinary PdfBuilder writer and performs a
local fail-closed preflight for prohibited active content, encryption, font embedding,
metadata, output intent, tagged structure, figure alternative text, table semantics, and link
annotation association.

If preflight succeeds, the package invokes veraPDF using `ProcessStartInfo.ArgumentList` with
no shell. Configure either a native executable or a Java executable plus CLI JAR. Command and
shell scripts are rejected. Input is written to a random bounded temporary directory; timeout
and cancellation kill the validator process tree, output is capped, XML parsing prohibits DTDs
and external resolution, and cleanup occurs in `finally`.

`PdfComplianceReport.IsConformant` is true only when local preflight and the independent
validator both pass. `EnsureConformant()` throws for candidates without that evidence.

## Fonts, colour, and semantics

No ICC profile or font binary is bundled or committed. The application owns the profile
approval decision and supplies its bytes explicitly. PDF/A and PDF/UA candidates must use
registered embeddable fonts; silent Base-14 or full-font fallback fails preflight.

PDF/UA candidates use the core tagged-PDF structure tree. Page tabs follow structure order,
table rows and cells receive semantic roles, figures require alternative text, and link
annotations are connected through structure-parent and object-reference entries. Applications
remain responsible for meaningful content, reading order, headings, captions, table headers,
and alternative descriptions.

## CI evidence

Ubuntu CI generates sanitised candidates from the distribution's Noto font and sRGB ICC
profile, validates the three requested profiles with pinned veraPDF 1.30.2, checks the
validator's explicit `isCompliant="true"` result, and retains both candidates and XML reports.
The executable arguments follow veraPDF's documented `--flavour` and `--format` interface.

See the [veraPDF CLI documentation](https://docs.verapdf.org/cli/help/) and the runnable
[`ComplianceCandidates`](../samples/ComplianceCandidates) fixture generator.
