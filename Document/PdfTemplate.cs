using System;
using System.IO;
using System.Threading;

namespace PdfBuilder.Document
{
    /// <summary>
    /// Stateless base class for strongly typed document templates. Every generation call creates
    /// a fresh document, so a template instance may be reused concurrently.
    /// </summary>
    public abstract class PdfTemplate<TModel>
    {
        public abstract void Compose(IDocumentDescriptor document, TModel model);

        public PdfDocument Create(TModel model)
            => PdfDocument.Create(document => Compose(document, model));

        public byte[] GenerateBytes(TModel model, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = Create(model).GenerateBytes(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return bytes;
        }

        public void Generate(Stream destination, TModel model, CancellationToken cancellationToken = default)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            cancellationToken.ThrowIfCancellationRequested();
            Create(model).Generate(destination, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
