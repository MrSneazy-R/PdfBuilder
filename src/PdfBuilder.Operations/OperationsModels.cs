namespace PdfBuilder.Operations;

/// <summary>Identifies an existing local PDF and an optional password supplied by its authorised caller.</summary>
public sealed record PdfInput
{
    /// <summary>Creates an input backed by a local file.</summary>
    public PdfInput(string path, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A PDF path is required.", nameof(path));
        Path = System.IO.Path.GetFullPath(path);
        Password = password;
    }

    /// <summary>Gets the absolute local path.</summary>
    public string Path { get; }
    /// <summary>Gets the password explicitly supplied by the authorised caller.</summary>
    public string? Password { get; }
}

/// <summary>Represents one input and page range in a merge operation.</summary>
public sealed record PdfMergeSource(PdfInput Input, string Pages = "1-z");

/// <summary>Describes an attachment to add to a PDF.</summary>
public sealed record PdfAttachment(string Path, string? DisplayName = null);

/// <summary>Controls standard qpdf encryption permissions.</summary>
public sealed class PdfEncryptionOptions
{
    /// <summary>Gets or sets the password used to open the PDF. Empty allows opening without a password.</summary>
    public string UserPassword { get; set; } = string.Empty;
    /// <summary>Gets or sets the required non-empty owner password.</summary>
    public string OwnerPassword { get; set; } = string.Empty;
    /// <summary>Gets or sets the printing permission.</summary>
    public PdfPrintPermission Print { get; set; } = PdfPrintPermission.Full;
    /// <summary>Gets or sets the modification permission.</summary>
    public PdfModifyPermission Modify { get; set; } = PdfModifyPermission.All;
    /// <summary>Gets or sets whether text and graphics extraction is allowed.</summary>
    public bool AllowExtraction { get; set; } = true;
    /// <summary>Gets or sets whether accessibility extraction is allowed.</summary>
    public bool AllowAccessibility { get; set; } = true;
}

/// <summary>Printing permission used for encrypted output.</summary>
public enum PdfPrintPermission { None, LowResolution, Full }
/// <summary>Modification permission used for encrypted output.</summary>
public enum PdfModifyPermission { None, Assembly, Form, Annotate, All }

/// <summary>Read-only inspection details returned for an existing PDF.</summary>
public sealed record PdfInspection(
    string Path,
    int PageCount,
    string? PdfVersion,
    bool IsEncrypted,
    bool IsLinearized,
    IReadOnlyList<string> AttachmentNames);

/// <summary>Settings that bound qpdf execution and temporary resource use.</summary>
public sealed class QpdfBackendOptions
{
    /// <summary>Gets or sets the qpdf executable path or executable name.</summary>
    public string QpdfPath { get; set; } = Environment.GetEnvironmentVariable("PDFBUILDER_QPDF_PATH") ?? "qpdf";
    /// <summary>Gets or sets the maximum duration of each qpdf process.</summary>
    public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Gets or sets the maximum combined temporary bytes produced by one operation.</summary>
    public long MaximumTemporaryBytes { get; set; } = 2L * 1024 * 1024 * 1024;
    /// <summary>Gets or sets the maximum number of input or split-output files in one operation.</summary>
    public int MaximumFiles { get; set; } = 512;
    /// <summary>Gets or sets the maximum characters captured from each process stream.</summary>
    public int MaximumCapturedCharacters { get; set; } = 1_000_000;
    /// <summary>Gets or sets an optional dedicated temporary root.</summary>
    public string? TemporaryRoot { get; set; }
}
