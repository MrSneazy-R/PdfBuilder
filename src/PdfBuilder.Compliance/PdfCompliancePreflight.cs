using System.Text;
using System.Text.RegularExpressions;

namespace PdfBuilder.Compliance;

internal static partial class PdfCompliancePreflight
{
    internal static List<PdfComplianceFinding> Inspect(byte[] candidate, PdfComplianceProfile profile)
    {
        string pdf = Encoding.Latin1.GetString(candidate);
        var findings = new List<PdfComplianceFinding>();
        Require(pdf.Contains("/Type /Metadata", StringComparison.Ordinal), "metadata.xmp", "A conforming candidate requires an XMP metadata stream.", findings);
        Require(pdf.Contains("/Lang ", StringComparison.Ordinal), "metadata.language", "A conforming candidate requires a document language.", findings);
        Require(pdf.Contains("/Title ", StringComparison.Ordinal), "metadata.title", "A conforming candidate requires a document title.", findings);
        Reject(pdf.Contains("/Encrypt ", StringComparison.Ordinal), "feature.encryption", "Encrypted output is prohibited by the selected profile.", findings);
        Reject(pdf.Contains("/JavaScript", StringComparison.Ordinal) || pdf.Contains("/Launch", StringComparison.Ordinal), "feature.active-content", "Executable actions are prohibited.", findings);
        Require(!pdf.Contains("/Subtype /Type1", StringComparison.Ordinal), "font.base14", "Base-14 fonts are not embedded; register and use embeddable fonts for compliance candidates.", findings);
        if (pdf.Contains("/Type /Font", StringComparison.Ordinal))
            Require(pdf.Contains("/FontFile2", StringComparison.Ordinal) || pdf.Contains("/FontFile3", StringComparison.Ordinal), "font.embedded", "Every used font must have an embedded font program.", findings);

        if (profile is PdfComplianceProfile.PdfA2B or PdfComplianceProfile.PdfA3B)
        {
            Require(pdf.Contains("/OutputIntents [", StringComparison.Ordinal), "pdfa.output-intent", "PDF/A requires an output intent.", findings);
            Require(pdf.Contains("/DestOutputProfile", StringComparison.Ordinal) && pdf.Contains("acsp", StringComparison.Ordinal), "pdfa.icc", "PDF/A requires an embedded, recognizable ICC profile.", findings);
            string marker = profile == PdfComplianceProfile.PdfA2B ? "pdfaid:part=\"2\"" : "pdfaid:part=\"3\"";
            Require(pdf.Contains(marker, StringComparison.Ordinal), "pdfa.identification", "The XMP packet does not identify the requested PDF/A part.", findings);
        }
        else
        {
            Require(pdf.Contains("pdfuaid:part=\"1\"", StringComparison.Ordinal), "pdfua.identification", "The XMP packet does not identify PDF/UA-1.", findings);
            Require(pdf.Contains("/StructTreeRoot", StringComparison.Ordinal) && pdf.Contains("/MarkInfo << /Marked true >>", StringComparison.Ordinal), "pdfua.structure", "PDF/UA requires marked content and a structure tree.", findings);
            foreach (Match figure in FigureObjectExpression().Matches(pdf).Cast<Match>())
                Require(figure.Value.Contains("/Alt ", StringComparison.Ordinal), "pdfua.figure-alt", "Every Figure structure element requires alternative text.", findings);
            if (pdf.Contains("/S /Table", StringComparison.Ordinal))
            {
                Require(pdf.Contains("/S /TR", StringComparison.Ordinal), "pdfua.table-row", "Tagged tables require row structure elements.", findings);
                Require(pdf.Contains("/S /TH", StringComparison.Ordinal) || pdf.Contains("/S /TD", StringComparison.Ordinal), "pdfua.table-cell", "Tagged tables require header or data cell structure elements.", findings);
            }
            if (pdf.Contains("/Subtype /Link", StringComparison.Ordinal))
                Require(pdf.Contains("/StructParent ", StringComparison.Ordinal) && pdf.Contains("/Type /OBJR", StringComparison.Ordinal), "pdfua.link-association", "Link annotations must be associated with Link structure elements.", findings);
        }
        return findings;
    }

    private static void Require(bool condition, string code, string message, List<PdfComplianceFinding> findings)
    {
        if (!condition) findings.Add(new PdfComplianceFinding(code, PdfComplianceSeverity.Error, message));
    }

    private static void Reject(bool condition, string code, string message, List<PdfComplianceFinding> findings)
    {
        if (condition) findings.Add(new PdfComplianceFinding(code, PdfComplianceSeverity.Error, message));
    }

    [GeneratedRegex(@"/S /Figure\b(?:(?!endobj).)*endobj", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex FigureObjectExpression();
}
