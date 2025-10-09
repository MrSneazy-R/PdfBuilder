using PdfBuilder.Document;

public sealed class LinkRectElement : PdfElement
{
    public float Width { get; set; }
    public float Height { get; set; }

    // Choose one
    public string? Url { get; set; }
    public string? Anchor { get; set; }

    public LinkRectElement(float x, float y, float w, float h) : base(x, y) { Width = w; Height = h; }
}
