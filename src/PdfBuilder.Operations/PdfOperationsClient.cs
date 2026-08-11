namespace PdfBuilder.Operations;

/// <summary>High-level entry point for bounded operations on existing local PDFs.</summary>
public sealed class PdfOperationsClient
{
    private readonly IPdfOperationsBackend _backend;

    /// <summary>Creates a client using the qpdf backend.</summary>
    public PdfOperationsClient(QpdfBackendOptions? options = null)
        : this(new QpdfBackend(options)) { }

    /// <summary>Creates a client using an explicitly supplied isolated backend.</summary>
    public PdfOperationsClient(IPdfOperationsBackend backend)
        => _backend = backend ?? throw new ArgumentNullException(nameof(backend));

    public Task<PdfInspection> InspectAsync(PdfInput input, CancellationToken cancellationToken = default)
        => _backend.InspectAsync(input, cancellationToken);
    public Task ExtractAsync(PdfInput input, string pages, string outputPath, CancellationToken cancellationToken = default)
        => _backend.SelectPagesAsync(input, pages, outputPath, cancellationToken);
    public Task SelectPagesAsync(PdfInput input, string pages, string outputPath, CancellationToken cancellationToken = default)
        => _backend.SelectPagesAsync(input, pages, outputPath, cancellationToken);
    public Task MergeAsync(IReadOnlyList<PdfMergeSource> sources, string outputPath, CancellationToken cancellationToken = default)
        => _backend.MergeAsync(sources, outputPath, cancellationToken);
    public Task<IReadOnlyList<string>> SplitAsync(PdfInput input, string outputDirectory, int pagesPerFile = 1, CancellationToken cancellationToken = default)
        => _backend.SplitAsync(input, outputDirectory, pagesPerFile, cancellationToken);
    public Task OverlayAsync(PdfInput input, PdfInput overlay, string outputPath, CancellationToken cancellationToken = default)
        => _backend.OverlayAsync(input, overlay, outputPath, cancellationToken);
    public Task UnderlayAsync(PdfInput input, PdfInput underlay, string outputPath, CancellationToken cancellationToken = default)
        => _backend.UnderlayAsync(input, underlay, outputPath, cancellationToken);
    public Task AddAttachmentAsync(PdfInput input, PdfAttachment attachment, string outputPath, CancellationToken cancellationToken = default)
        => _backend.AddAttachmentAsync(input, attachment, outputPath, cancellationToken);
    public Task EncryptAsync(PdfInput input, string outputPath, PdfEncryptionOptions options, CancellationToken cancellationToken = default)
        => _backend.EncryptAsync(input, outputPath, options, cancellationToken);
    public Task DecryptAsync(PdfInput input, string outputPath, CancellationToken cancellationToken = default)
        => _backend.DecryptAsync(input, outputPath, cancellationToken);
    public Task LinearizeAsync(PdfInput input, string outputPath, CancellationToken cancellationToken = default)
        => _backend.LinearizeAsync(input, outputPath, cancellationToken);
}
