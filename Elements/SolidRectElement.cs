using PdfBuilder.Document;

namespace PdfBuilder.Elements
{
    /// <summary>
    /// Draws a solid or stroked rectangle in the final PDF output.
    /// </summary>
    public sealed class SolidRectElement : PdfElement
    {
        public SolidRectElement(float x, float y, float width, float height)
            : base(x, y)
        {
            Width = width;
            Height = height;
        }

        public SolidRectElement() : base(0, 0)
        {
        }

        public float Width { get; set; }
        public float Height { get; set; }
        public string? FillColor { get; set; }
        public string? StrokeColor { get; set; }
        public float StrokeWidth { get; set; } = 0f;
        public float Opacity { get; set; } = 1f;
        public float[]? DashPattern { get; set; }
    }
}
