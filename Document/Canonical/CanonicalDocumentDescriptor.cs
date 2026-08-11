using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalDocumentDescriptor : IDocumentDescriptor
    {
        private readonly PdfDocument _document;
        private readonly List<CanonicalPageDescriptor> _pages = new();
        private readonly CanonicalCompositionState _compositionState = new();
        public CanonicalDocumentDescriptor(PdfDocument document) => _document = document;
        public void Metadata(Action<DocumentMetadata> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_document.Metadata);
            _document.Title = _document.Metadata.Title;
        }
        public void OutputPreset(PdfOutputPreset preset) => _document.ApplyOutputPreset(preset);
        public void Output(Action<PdfOutputOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_document.OutputOptions);
        }
        public void Generation(Action<PdfGenerationOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_document.GenerationOptions);
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
        public void RenderLimits(Action<Layout.PdfRenderLimits> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_document.RenderLimits);
        }
        public void Page(Action<IPageDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalPageDescriptor(_document, _compositionState);
            configure(descriptor);
            _pages.Add(descriptor);
        }

        internal void Build()
        {
            int totalPagesHint = Math.Max(1, _pages.Count);
            int pass = 0;
            while (true)
            {
                if (_compositionState.UsesPageAwareVisibility)
                    _document.RenderLimits.ValidatePaginationPass(++pass, _compositionState.DiagnosticPaths);

                _document.PageList.Clear();
                _document.LayoutTrace.Clear();
                _document.CompositionTotalPagesHint = totalPagesHint;
                foreach (CanonicalPageDescriptor page in _pages)
                    page.Build();

                int actualPageCount = _document.Pages.Count;
                _document.CompositionTotalPagesHint = Math.Max(1, actualPageCount);
                if (!_compositionState.UsesPageAwareVisibility || actualPageCount == totalPagesHint)
                    return;
                totalPagesHint = Math.Max(1, actualPageCount);
            }
        }
    }

    private sealed class CanonicalCompositionState
    {
        private readonly HashSet<string> _diagnosticPaths = new(StringComparer.Ordinal);
        internal bool UsesPageAwareVisibility { get; private set; }
        internal IReadOnlyList<string> DiagnosticPaths => _diagnosticPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        internal void RegisterPageAwareVisibility(string? diagnosticPath = null)
        {
            UsesPageAwareVisibility = true;
            if (!string.IsNullOrWhiteSpace(diagnosticPath))
                _diagnosticPaths.Add(diagnosticPath);
        }
    }
}
