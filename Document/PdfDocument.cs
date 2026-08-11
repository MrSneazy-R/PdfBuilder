using System.Collections.Generic;
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
        public List<PdfPage> Pages { get; } = new();

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

        internal FontCatalogSnapshot FontSnapshot { get; } = FontCatalog.CaptureSnapshot();
        internal object GenerationSyncRoot { get; } = new();

        public PdfDocument()
        {
        }

        public PdfDocument(List<PdfPage> pages)
        {
            Pages = pages;
        }

        public PdfPage AddPage(float width = 612f, float height = 792f)
        {
            RenderLimits.ValidatePageCount(Pages.Count + 1);
            var page = new PdfPage(width, height)
            {
                LayoutOptions = LayoutOptions.Clone(),
                TextDefaults = TextDefaults.Clone(),
                Theme = Theme.Clone()
            };
            Pages.Add(page);
            page.Owner = this;
            page.Pagination = Pagination;
            page.ProfilerSession = ProfilerSession;
            return page;
        }

        public string? Title { get; set; } = null;
        public HeaderFooterSpec HeaderFooter { get; set; } = new();
        public MasterPageSpec Master { get; set; } = new();
    }
}



