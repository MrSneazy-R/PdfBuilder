using System.Collections.Generic;
using System.Collections.ObjectModel;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;

namespace PdfBuilder.Models
{
    public class PdfPage
    {
        private readonly List<PdfElement> _elements = new();
        private readonly ReadOnlyCollection<PdfElement> _readOnlyElements;
        public const float DefaultWidth = 612f;
        public const float DefaultHeight = 792f;

        // Dimensions
        public float Width { get; private set; }
        public float Height { get; private set; }

        // Content
        /// <summary>Gets page elements as a read-only view.</summary>
        public IReadOnlyList<PdfElement> Elements => _readOnlyElements;
        /// <summary>Compatibility shim for legacy direct element-list mutation. Prefer AddElement or document builders.</summary>
        [Obsolete("Direct element-list mutation is deprecated. Use AddElement or canonical container builders.", false, DiagnosticId = "PDFB009")]
        public IList<PdfElement> MutableElements => _elements;
        internal List<PdfElement> ElementList => _elements;
        internal List<PdfElement> HeaderElements { get; } = new();
        internal List<PdfElement> FooterElements { get; } = new();

        public LayoutOptions LayoutOptions { get; internal set; } = new();

        public TextStyleDefaults TextDefaults { get; set; } = new TextStyleDefaults();
        public DocumentTheme Theme { get; internal set; } = new();
        internal PdfDocument? Owner { get; set; }
        internal PaginationRegistry? Pagination { get; set; }
        internal LayoutProfilerSession? ProfilerSession { get; set; }
        internal int CompositionPageNumber { get; set; }

        // Styling
        public string? BackgroundColor { get; set; } = "#FFFFFF"; // Default: white

        // Margins (optional, can be applied in builder logic)
        public float MarginTop { get; set; } = 40;
        public float MarginBottom { get; set; } = 40;
        public float MarginLeft { get; set; } = 40;
        public float MarginRight { get; set; } = 40;

        // Constructor
        public PdfPage(float width, float height)
        {
            Width = width;
            Height = height;
            _readOnlyElements = _elements.AsReadOnly();
        }

        // Element Add
        public void AddElement(PdfElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            _elements.Add(element);
        }

        // Preset Sizes (in points)
        public static PdfPage A4() => new PdfPage(595f, 842f);
        public static PdfPage A4Landscape() => new PdfPage(842f, 595f);

        public static PdfPage A3() => new PdfPage(842f, 1191f);
        public static PdfPage A3Landscape() => new PdfPage(1191f, 842f);

        public static PdfPage Letter() => new PdfPage(612f, 792f);
        public static PdfPage LetterLandscape() => new PdfPage(792f, 612f);

        public static PdfPage Custom(float width, float height) => new PdfPage(width, height);

        public HeaderFooterSpec? HeaderFooterOverride { get; set; } = null;
        public MasterPageSpec? MasterOverride { get; set; } = null;
        public ColumnLayoutSpec? Columns { get; set; } = null;

        internal void SetHeaderElements(IEnumerable<PdfElement> elements)
        {
            HeaderElements.Clear();
            if (elements == null)
                return;
            HeaderElements.AddRange(elements);
        }

        internal void SetFooterElements(IEnumerable<PdfElement> elements)
        {
            FooterElements.Clear();
            if (elements == null)
                return;
            FooterElements.AddRange(elements);
        }
    }
}



