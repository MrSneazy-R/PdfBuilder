namespace PdfBuilder.Operations;

/// <summary>Isolates the backend used for existing-PDF operations.</summary>
public interface IPdfOperationsBackend
{
    Task<PdfInspection> InspectAsync(PdfInput input, CancellationToken cancellationToken = default);
    Task SelectPagesAsync(PdfInput input, string pages, string outputPath, CancellationToken cancellationToken = default);
    Task MergeAsync(IReadOnlyList<PdfMergeSource> sources, string outputPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SplitAsync(PdfInput input, string outputDirectory, int pagesPerFile = 1, CancellationToken cancellationToken = default);
    Task OverlayAsync(PdfInput input, PdfInput overlay, string outputPath, CancellationToken cancellationToken = default);
    Task UnderlayAsync(PdfInput input, PdfInput underlay, string outputPath, CancellationToken cancellationToken = default);
    Task AddAttachmentAsync(PdfInput input, PdfAttachment attachment, string outputPath, CancellationToken cancellationToken = default);
    Task EncryptAsync(PdfInput input, string outputPath, PdfEncryptionOptions options, CancellationToken cancellationToken = default);
    Task DecryptAsync(PdfInput input, string outputPath, CancellationToken cancellationToken = default);
    Task LinearizeAsync(PdfInput input, string outputPath, CancellationToken cancellationToken = default);
}
