using PdfBuilder.Document;
using PdfBuilder.Models;
using System.Collections.Generic;

public sealed class RichTextElement : PdfElement
{
    public List<RichRun> Runs { get; } = new();

    // Paragraph defaults
    public string FontFamily { get; set; } = "Helvetica";
    public float FontSize { get; set; } = 12f;
    public float LineHeight { get; set; } = 1.2f;
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    // Box model
    public float? MarginTop { get; set; }
    public float? MarginBottom { get; set; }
    public float? MarginLeft { get; set; }
    public float? MarginRight { get; set; }
    public float? PaddingTop { get; set; }
    public float? PaddingBottom { get; set; }
    public float? PaddingLeft { get; set; }
    public float? PaddingRight { get; set; }

    public float? MaxWidth { get; set; } = null;
    public float Rotation { get; set; } = 0f;

    public RichTextElement(float x, float y) : base(x, y) { }

    public bool KeepWithNext { get; set; } = false;
    public bool AvoidBreakInside { get; set; } = true;

}
