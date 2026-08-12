namespace PdfBuilder.Compliance;

public enum PdfComplianceSeverity { Information, Warning, Error }

public sealed record PdfComplianceFinding(string Code, PdfComplianceSeverity Severity, string Message);

/// <summary>Evidence retained for a compliance candidate.</summary>
public sealed class PdfComplianceReport
{
    internal PdfComplianceReport(
        PdfComplianceProfile profile,
        IReadOnlyList<PdfComplianceFinding> findings,
        bool independentValidationPerformed,
        bool independentValidationPassed,
        string? validatorReport)
    {
        Profile = profile;
        Findings = findings;
        IndependentValidationPerformed = independentValidationPerformed;
        IndependentValidationPassed = independentValidationPassed;
        ValidatorReport = validatorReport;
    }

    public PdfComplianceProfile Profile { get; }
    public IReadOnlyList<PdfComplianceFinding> Findings { get; }
    public bool PreflightPassed => Findings.All(finding => finding.Severity != PdfComplianceSeverity.Error);
    public bool IndependentValidationPerformed { get; }
    public bool IndependentValidationPassed { get; }
    public bool IsConformant => PreflightPassed && IndependentValidationPerformed && IndependentValidationPassed;
    public string? ValidatorReport { get; }
}

/// <summary>A generated candidate and its validation evidence.</summary>
public sealed class PdfComplianceResult
{
    internal PdfComplianceResult(byte[] candidate, PdfComplianceReport report)
    {
        Candidate = candidate;
        Report = report;
    }

    public byte[] Candidate { get; }
    public PdfComplianceReport Report { get; }

    public byte[] EnsureConformant()
        => Report.IsConformant
            ? Candidate.ToArray()
            : throw new PdfComplianceException("The generated PDF has not passed all preflight and independent conformance checks.", Report);
}

public sealed class PdfComplianceException : Exception
{
    public PdfComplianceException(string message, PdfComplianceReport report) : base(message) => Report = report;
    public PdfComplianceReport Report { get; }
}
