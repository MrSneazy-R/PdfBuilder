using System;
using System.IO;
using System.Threading;
using PdfBuilder.Document.Layout;
using PdfBuilder.Models;
using PdfBuilder.Writer;

namespace PdfBuilder.Document
{
    /// <summary>Canonical document descriptor consumed by strongly typed templates.</summary>
    public interface IDocumentDescriptor
    {
        IDocumentDescriptor Theme(Action<DocumentThemeBuilder> configure);
        IDocumentDescriptor Compose(Action<DocumentComposer> configure);
        IDocumentDescriptor Metadata(Action<DocumentMetadata> configure);
        IDocumentDescriptor OutputOptions(Action<PdfOutputOptions> configure);
    }

    /// <summary>
    /// Stateless base class for strongly typed document templates. Every generation call creates
    /// a fresh document, so a template instance may be reused concurrently.
    /// </summary>
    public abstract class PdfTemplate<TModel>
    {
        public abstract void Compose(IDocumentDescriptor document, TModel model);

        public PdfDocument Create(TModel model)
        {
            var document = new PdfDocument();
            var descriptor = new PdfDocumentBuilder(document);
            Compose(descriptor, model);
            return descriptor.Build();
        }

        public byte[] GenerateBytes(TModel model, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = new PdfWriter().GenerateBytes(Create(model));
            cancellationToken.ThrowIfCancellationRequested();
            return bytes;
        }

        public void Generate(Stream destination, TModel model, CancellationToken cancellationToken = default)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            cancellationToken.ThrowIfCancellationRequested();
            new PdfWriter().GenerateStream(Create(model), destination);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
