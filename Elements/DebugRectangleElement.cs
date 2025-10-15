using PdfBuilder.Document;

namespace PdfBuilder.Elements
{
    /// <summary>
    /// Lightweight rectangle used for layout diagnostics overlays.
    /// </summary>
    public sealed class DebugRectangleElement : PdfElement
    {
        public DebugRectangleElement(float x, float y, float width, float height)
            : base(x, y)
        {
            Width = width;
            Height = height;
        }

        public float Width { get; set; }
        public float Height { get; set; }
        public string StrokeColor { get; set; } = "#FF0000";
        public float StrokeWidth { get; set; } = 0.5f;
        public float Opacity { get; set; } = 0.25f;
        public float[]? DashPattern { get; set; }
        public string? FillColor { get; set; } = null;
    }
}
