# PdfBuilder.Compliance

`PdfBuilder.Compliance` prepares bounded PDF/A-2b, PDF/A-3b, and PDF/UA-1 candidates,
runs local fail-closed preflight, and can invoke veraPDF without a shell. A result is marked
conformant only when both preflight and independent veraPDF validation pass.

The package never selects or bundles an ICC profile. Applications must supply an approved
profile for PDF/A. Every used font must be registered and embedded; Base-14 fallback fails
preflight. PDF/UA candidates require tagged structure, language, alternative text for figures,
table semantics, reading order, and annotation association.

Configure either a native veraPDF executable or a Java executable plus veraPDF CLI JAR.
`.bat`, `.cmd`, and `.sh` launchers are rejected because validation uses argument-list process
startup with no shell. Temporary directories are random, bounded to one candidate, cancelled
on timeout, killed as a process tree, and cleaned in `finally`.

```csharp
var options = new PdfComplianceOptions
{
    Language = "en-ZA",
    JavaExecutablePath = javaPath,
    VeraPdfJarPath = veraPdfJarPath
};
options.SetIccProfile(File.ReadAllBytes(approvedSrgbProfile));

PdfComplianceResult result = await PdfCompliance.GenerateAsync(
    PdfComplianceProfile.PdfA2B,
    options,
    document => document.Page(page => page.Content().Text("Archive copy").FontFamily("Registered Font")),
    cancellationToken);

File.WriteAllBytes("archive.pdf", result.EnsureConformant());
```

`Candidate` remains available for diagnostics when validation fails, but
`EnsureConformant()` throws. Metadata declarations alone never make `IsConformant` true.
