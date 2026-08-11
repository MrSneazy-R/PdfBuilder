using PdfBuilder.Compliance;
using PdfBuilder.Document;
using PdfBuilder.Fonts;

if (args.Length != 3)
    throw new ArgumentException("Usage: ComplianceCandidates <approved-icc-profile> <font-file> <output-directory>");

string iccPath = Path.GetFullPath(args[0]);
string fontPath = Path.GetFullPath(args[1]);
string outputDirectory = Path.GetFullPath(args[2]);
Directory.CreateDirectory(outputDirectory);
FontCatalog.RegisterFile(fontPath, "ComplianceSans");

foreach (PdfComplianceProfile profile in Enum.GetValues<PdfComplianceProfile>())
{
    var options = new PdfComplianceOptions
    {
        Language = "en-ZA",
        RequireIndependentValidation = false,
        OutputConditionIdentifier = "sRGB"
    };
    if (profile is PdfComplianceProfile.PdfA2B or PdfComplianceProfile.PdfA3B)
        options.SetIccProfile(await File.ReadAllBytesAsync(iccPath));

    PdfComplianceResult result = await PdfCompliance.GenerateAsync(profile, options, document =>
    {
        document.Metadata(metadata =>
        {
            metadata.Title = $"{profile} validation fixture";
            metadata.Author = "PdfBuilder test fixture";
        });
        document.Page(page =>
        {
            page.DefaultTextStyle(style => style.FontFamily("ComplianceSans"));
            page.Content().Semantic(PdfSemanticRole.Section).Column(column =>
            {
                column.Spacing(8);
                column.Item().Heading(1).Text("Compliance validation fixture");
                column.Item().Text("Sanitised synthetic content for independent validation.");
                column.Item().Semantic(PdfSemanticRole.Figure)
                    .AlternativeText("A diagonal line rising from left to right")
                    .Canvas(180, 50, canvas => canvas.Line(5, 45, 175, 5));
                column.Item().Semantic(PdfSemanticRole.Caption).Text("Figure 1 - Synthetic trend");
            });
        });
    });
    if (!result.Report.PreflightPassed)
        throw new InvalidOperationException(string.Join(Environment.NewLine, result.Report.Findings.Select(finding => $"{finding.Code}: {finding.Message}")));

    string name = profile switch
    {
        PdfComplianceProfile.PdfA2B => "pdfa-2b.pdf",
        PdfComplianceProfile.PdfA3B => "pdfa-3b.pdf",
        PdfComplianceProfile.PdfUa1 => "pdfua-1.pdf",
        _ => throw new ArgumentOutOfRangeException()
    };
    await File.WriteAllBytesAsync(Path.Combine(outputDirectory, name), result.Candidate);
}
