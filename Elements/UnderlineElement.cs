using PdfBuilder.Document;
using PdfBuilder.Models;

namespace PdfBuilder.Elements
{
    public class UnderlineElement : PdfElement
    {
        public UnderlineElement(float x, float y) : base(x, y) { }

        public float Width { get; set; } = 100;
        public float Thickness { get; set; } = 1;
        public string Color { get; set; } = "#000000";
        public float Rotation { get; set; } = 0;
        public LineStyle Style { get; set; } = LineStyle.Solid;
    }

    public enum LineStyle
    {
        Solid,
        Dashed
    }
}
