using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using System.Collections.Generic;

namespace PdfBuilder.Models
{
    public class PdfPage
    {
        public const float DefaultWidth = 612f;
        public const float DefaultHeight = 792f;

        // Dimensions
        public float Width { get; private set; }
        public float Height { get; private set; }

        // Content
        public List<PdfElement> Elements { get; } = new();
        internal List<PdfElement> HeaderElements { get; } = new();
        internal List<PdfElement> FooterElements { get; } = new();

        public LayoutOptions LayoutOptions { get; internal set; } = new();

        public TextStyleDefaults TextDefaults { get; set; } = new TextStyleDefaults();
        internal PdfDocument? Owner { get; set; }
        internal PaginationRegistry? Pagination { get; set; }
        internal LayoutProfilerSession? ProfilerSession { get; set; }

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
        }

        // Element Add
        public void AddElement(PdfElement element)
        {
            Elements.Add(element);
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



