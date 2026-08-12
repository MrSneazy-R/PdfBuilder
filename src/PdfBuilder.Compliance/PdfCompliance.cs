using PdfBuilder.Document;
using PdfBuilder.Models;

namespace PdfBuilder.Compliance;

/// <summary>Builds compliance candidates and retains preflight plus independent validation evidence.</summary>
public static class PdfCompliance
{
    public static async Task<PdfComplianceResult> GenerateAsync(
        PdfComplianceProfile profile,
        PdfComplianceOptions options,
        Action<IDocumentDescriptor> compose,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(compose);
        ValidateOptions(profile, options);

        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            compose(descriptor);
            ApplyProfile(descriptor, profile, options);
        });
        long? configuredMaximum = document.RenderLimits.MaximumOutputBytes;
        document.RenderLimits.MaximumOutputBytes = configuredMaximum.HasValue
            ? Math.Min(configuredMaximum.Value, options.MaximumOutputBytes)
            : options.MaximumOutputBytes;
        byte[] candidate = document.GenerateBytes(cancellationToken);
        List<PdfComplianceFinding> findings = PdfCompliancePreflight.Inspect(candidate, profile);

        VeraPdfValidationResult validation = findings.Any(finding => finding.Severity == PdfComplianceSeverity.Error)
            ? new VeraPdfValidationResult(false, false, null, "Independent validation was not run because local preflight failed.")
            : await VeraPdfValidator.ValidateAsync(candidate, profile, options, cancellationToken).ConfigureAwait(false);
        if (!validation.Performed && options.RequireIndependentValidation)
            findings.Add(new PdfComplianceFinding("validator.required", PdfComplianceSeverity.Error, validation.Failure ?? "Independent veraPDF validation is required."));
        else if (validation.Performed && !validation.Passed)
            findings.Add(new PdfComplianceFinding("validator.failed", PdfComplianceSeverity.Error, validation.Failure ?? "veraPDF reported non-conformance."));
        else if (!validation.Performed)
            findings.Add(new PdfComplianceFinding("validator.not-run", PdfComplianceSeverity.Warning, validation.Failure ?? "Independent validation was not performed."));

        var report = new PdfComplianceReport(profile, findings.AsReadOnly(), validation.Performed, validation.Passed, validation.Report);
        return new PdfComplianceResult(candidate, report);
    }

    private static void ApplyProfile(IDocumentDescriptor descriptor, PdfComplianceProfile profile, PdfComplianceOptions options)
    {
        descriptor.Output(output => output.PdfVersion = PdfVersion.Pdf17);
        descriptor.Metadata(metadata =>
        {
            metadata.Language = options.Language;
            metadata.Creator ??= "PdfBuilder";
            metadata.Producer ??= "PdfBuilder";
            metadata.CreatedUtc ??= DateTimeOffset.UtcNow;
            metadata.ModifiedUtc ??= metadata.CreatedUtc;
            metadata.CustomXmp = ComplianceXmpBuilder.Build(profile, metadata);
        });
        if (profile == PdfComplianceProfile.PdfUa1)
            descriptor.Tagged(tagged => { tagged.Language(options.Language); tagged.Enabled(); });
        if (profile is PdfComplianceProfile.PdfA2B or PdfComplianceProfile.PdfA3B)
        {
            byte[] profileBytes = options.GetIccProfileBytes()!;
            descriptor.OutputIntent(intent =>
            {
                intent.Profile(profileBytes);
                intent.Identifier(options.OutputConditionIdentifier);
                if (!string.IsNullOrWhiteSpace(options.OutputConditionInfo)) intent.Info(options.OutputConditionInfo!);
                intent.RegistryName(options.IccRegistryName);
            });
        }
    }

    private static void ValidateOptions(PdfComplianceProfile profile, PdfComplianceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Language)) throw new ArgumentException("A BCP 47 document language is required.", nameof(options));
        _ = new DocumentMetadata { Language = options.Language }.InvokingValidate();
        if (options.ValidationTimeout <= TimeSpan.Zero || options.ValidationTimeout > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(options), "Validation timeout must be greater than zero and no more than one hour.");
        if (options.MaximumOutputBytes <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Maximum output bytes must be positive.");
        if (profile is PdfComplianceProfile.PdfA2B or PdfComplianceProfile.PdfA3B)
        {
            if (options.IccProfile.IsEmpty) throw new ArgumentException("PDF/A requires a caller-approved ICC profile; no profile is bundled or selected implicitly.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.OutputConditionIdentifier)) throw new ArgumentException("PDF/A requires an output-condition identifier.", nameof(options));
        }
    }

    private static bool InvokingValidate(this DocumentMetadata metadata)
    {
        metadata.Validate();
        return true;
    }
}
