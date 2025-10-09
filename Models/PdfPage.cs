using PdfBuilder.Document;
using PdfBuilder.Elements;
using System.Collections.Generic;

namespace PdfBuilder.Models
{
    public class PdfPage
    {
        // Dimensions
        public float Width { get; private set; }
        public float Height { get; private set; }

        // Content
        public List<PdfElement> Elements { get; } = new();

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

        // 🔁 Preset Sizes (in points)
        public static PdfPage A4() => new PdfPage(595f, 842f);
        public static PdfPage A4Landscape() => new PdfPage(842f, 595f);

        public static PdfPage A3() => new PdfPage(842f, 1191f);
        public static PdfPage A3Landscape() => new PdfPage(1191f, 842f);

        public static PdfPage Letter() => new PdfPage(612f, 792f);
        public static PdfPage LetterLandscape() => new PdfPage(792f, 612f);

        public static PdfPage Custom(float width, float height) => new PdfPage(width, height);

        // inside class PdfPage
        public HeaderFooterSpec? HeaderFooterOverride { get; set; } = null;
        public MasterPageSpec? MasterOverride { get; set; } = null;
        public ColumnLayoutSpec? Columns { get; set; } = null;
    }

}
