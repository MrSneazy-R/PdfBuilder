using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalDocumentDescriptor : IDocumentDescriptor
    {
        private readonly PdfDocument _document;
        private readonly List<CanonicalPageDescriptor> _pages = new();
        public CanonicalDocumentDescriptor(PdfDocument document) => _document = document;
        public void Metadata(Action<DocumentMetadata> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_document.Metadata);
            _document.Title = _document.Metadata.Title;
        }
        public void Theme(Action<DocumentThemeBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(new DocumentThemeBuilder(_document.Theme));
            _document.TextDefaults.CopyFrom(_document.Theme.DefaultTextStyle);
        }
        public void Diagnostics(Action<Layout.PdfDiagnosticsOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_document.LayoutOptions.Diagnostics);
        }
        public void Page(Action<IPageDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalPageDescriptor(_document);
            configure(descriptor);
            _pages.Add(descriptor);
        }

        internal void Build()
        {
            foreach (CanonicalPageDescriptor page in _pages)
                page.Build();
        }
    }
}
