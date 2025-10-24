using PdfBuilder.Document;
using System.Collections.Generic;

public class CanvasElement : PdfElement
{
    public CanvasElement(float x, float y, float width, float height) : base(x, y)
    {
        Width = width;
        Height = height;
    }

    public float Width { get; set; }
    public float Height { get; set; }

    public float? MarginTop { get; set; }
    public float? MarginBottom { get; set; }
    public float? MarginLeft { get; set; }
    public float? MarginRight { get; set; }

    public bool AvoidBreakInside { get; set; }

    public IList<string> Commands { get; } = new List<string>();
}
