namespace PdfBuilder.Compliance;

/// <summary>Controls compliance preparation and independent validation.</summary>
public sealed class PdfComplianceOptions
{
    private byte[]? _iccProfile;

    /// <summary>Required BCP 47 document language.</summary>
    public string Language { get; set; } = string.Empty;
    /// <summary>Output-condition identifier for caller-approved ICC data.</summary>
    public string OutputConditionIdentifier { get; set; } = "Custom";
    /// <summary>Optional human-readable output-condition description.</summary>
    public string? OutputConditionInfo { get; set; }
    /// <summary>ICC registry name.</summary>
    public string IccRegistryName { get; set; } = "http://www.color.org";
    /// <summary>Native veraPDF executable. Shell scripts and command files are rejected.</summary>
    public string? VeraPdfExecutablePath { get; set; }
    /// <summary>Java executable used with <see cref="VeraPdfJarPath"/> when no native launcher is available.</summary>
    public string? JavaExecutablePath { get; set; }
    /// <summary>veraPDF CLI JAR passed to Java with an argument list.</summary>
    public string? VeraPdfJarPath { get; set; }
    /// <summary>Maximum independent-validator runtime.</summary>
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Maximum candidate PDF size.</summary>
    public long MaximumOutputBytes { get; set; } = 256L * 1024L * 1024L;
    /// <summary>Requires successful veraPDF validation before <c>IsConformant</c> can be true.</summary>
    public bool RequireIndependentValidation { get; set; } = true;

    /// <summary>Gets a defensive copy of the caller-supplied ICC profile.</summary>
    public ReadOnlyMemory<byte> IccProfile => _iccProfile?.ToArray() ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Copies caller-approved ICC profile bytes into this configuration.</summary>
    public void SetIccProfile(ReadOnlyMemory<byte> profile)
        => _iccProfile = profile.IsEmpty
            ? throw new ArgumentException("An ICC profile is required.", nameof(profile))
            : profile.ToArray();

    internal byte[]? GetIccProfileBytes() => _iccProfile?.ToArray();
}
