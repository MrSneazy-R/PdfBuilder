using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PdfBuilder.Document.Layout;
using PdfBuilder.Fonts;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    /// <summary>
    /// Internal representation of a PDF document.
    /// Use PdfDocumentBuilder for fluent API creation.
    /// </summary>
    public partial class PdfDocument
    {
        private readonly List<PdfPage> _pages = new();
        private readonly ReadOnlyCollection<PdfPage> _readOnlyPages;

        /// <summary>Gets the generated pages as a read-only view.</summary>
        public IReadOnlyList<PdfPage> Pages => _readOnlyPages;
        /// <summary>Compatibility shim for legacy direct page-list mutation. Prefer AddPage or canonical builders.</summary>
        [Obsolete("Direct page-list mutation is deprecated. Use AddPage or the canonical document builder.", false, DiagnosticId = "PDFB008")]
        public IList<PdfPage> MutablePages => _pages;
        internal List<PdfPage> PageList => _pages;

        public LayoutOptions LayoutOptions { get; } = new();

        public PdfOutputOptions OutputOptions { get; } = new();

        public PdfGenerationOptions GenerationOptions { get; } = new();

        public DocumentMetadata Metadata { get; } = new();

        public TextStyleDefaults TextDefaults { get; } = new TextStyleDefaults();

        public DocumentTheme Theme { get; internal set; } = new();

        public PaginationRegistry Pagination { get; } = new();

        public LayoutProfilerSession ProfilerSession { get; } = new();

        /// <summary>Gets the structured layout trace recorded when diagnostics are enabled.</summary>
        public PdfLayoutTrace LayoutTrace { get; } = new();

        /// <summary>Gets configurable rendering safeguards for this document.</summary>
        public PdfRenderLimits RenderLimits { get; } = new();

        /// <summary>Gets document-scoped diagnostics for unresolved navigation targets.</summary>
        public PdfNavigationDiagnostics NavigationDiagnostics { get; } = new();
        /// <summary>Gets diagnostics captured by the most recent successful generation.</summary>
        public PdfGenerationMetrics? LastGenerationMetrics { get; internal set; }

        internal FontCatalogSnapshot FontSnapshot { get; } = FontCatalog.CaptureSnapshot();
        internal object GenerationSyncRoot { get; } = new();
        internal int CompositionTotalPagesHint { get; set; }

        public PdfDocument()
        {
            _readOnlyPages = _pages.AsReadOnly();
        }

        public PdfDocument(List<PdfPage> pages) : this()
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            foreach (PdfPage page in pages)
            {
                _pages.Add(page);
                page.Owner = this;
            }
        }

        public PdfPage AddPage(float width = 612f, float height = 792f)
        {
            RenderLimits.ValidatePageCount(_pages.Count + 1);
            var page = new PdfPage(width, height)
            {
                LayoutOptions = LayoutOptions.Clone(),
                TextDefaults = TextDefaults.Clone(),
                Theme = Theme.Clone()
            };
            _pages.Add(page);
            page.CompositionPageNumber = _pages.Count;
            page.Owner = this;
            page.Pagination = Pagination;
            page.ProfilerSession = ProfilerSession;
            return page;
        }

        /// <summary>Applies a named output preset. The deterministic preset also enables deterministic generation.</summary>
        public void ApplyOutputPreset(PdfOutputPreset preset)
        {
            OutputOptions.ApplyPreset(preset);
            if (preset == PdfOutputPreset.Deterministic)
                GenerationOptions.Deterministic = true;
        }

        public string? Title { get; set; } = null;
        public HeaderFooterSpec HeaderFooter { get; set; } = new();
        public MasterPageSpec Master { get; set; } = new();
    }
}



