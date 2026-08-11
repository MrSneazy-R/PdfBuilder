using PdfBuilder.Writer;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    /// <summary>Creates a document using the canonical composition API.</summary>
    /// <param name="configure">Configures metadata and pages.</param>
    /// <returns>The composed document.</returns>
    public static PdfDocument Create(Action<IDocumentDescriptor> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var document = new PdfDocument();
        using (PdfBuilder.Fonts.FontCatalog.EnterSnapshot(document.FontSnapshot))
            configure(new CanonicalDocumentDescriptor(document));
        return document;
    }

    /// <summary>Generates the document as PDF bytes.</summary>
    public byte[] GenerateBytes() => new PdfWriter().GenerateBytes(this);

    /// <summary>Generates selected PNG preview images from the resolved document layout.</summary>
    public IReadOnlyList<PdfPreviewPage> GeneratePreviewImages(int dpi = 144, IEnumerable<int>? pageNumbers = null)
        => new PdfWriter().GeneratePreviewImages(this, dpi, pageNumbers, CancellationToken.None);

    /// <summary>Generates selected PNG preview images and observes cancellation between page renders.</summary>
    public IReadOnlyList<PdfPreviewPage> GeneratePreviewImages(int dpi, IEnumerable<int>? pageNumbers, CancellationToken cancellationToken)
        => new PdfWriter().GeneratePreviewImages(this, dpi, pageNumbers, cancellationToken);

    /// <summary>Generates the document as PDF bytes and observes cancellation between layout and page writes.</summary>
    /// <param name="cancellationToken">Cancels generation before the next expensive operation.</param>
    public byte[] GenerateBytes(CancellationToken cancellationToken) => new PdfWriter().GenerateBytes(this, cancellationToken);

    /// <summary>Generates the document into a writable stream.</summary>
    public void Generate(Stream destination) => new PdfWriter().GenerateStream(this, destination);

    /// <summary>Generates the document directly into a writable stream and observes cancellation between layout and page writes.</summary>
    /// <param name="destination">The stream that receives the PDF.</param>
    /// <param name="cancellationToken">Cancels generation before the next expensive operation.</param>
    public void Generate(Stream destination, CancellationToken cancellationToken) => new PdfWriter().GenerateStream(this, destination, cancellationToken);

    /// <summary>Generates and saves the document to a file path.</summary>
    public void Save(string path) => new PdfWriter().Save(this, path);

    /// <summary>Generates and saves the document to a file path while observing cancellation.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="cancellationToken">Cancels generation before the next expensive operation.</param>
    public void Save(string path, CancellationToken cancellationToken) => new PdfWriter().Save(this, path, cancellationToken);
}
