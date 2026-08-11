using System.Collections.Generic;
using PdfBuilder.Document;
using PdfBuilder.Document.TextShaping;
using PdfBuilder.Models;

public sealed class RichTextElement : PdfElement
{
    public List<RichRun> Runs { get; } = new();

    // Paragraph defaults
    public string FontFamily { get; set; } = "Helvetica";
    public float FontSize { get; set; } = 12f;
    public float LineHeight { get; set; } = 1.2f;
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public string Color { get; set; } = "black";
    public string? BackgroundColor { get; set; }
    public List<string>? FallbackFonts { get; set; }
    public TextWrapping Wrapping { get; set; } = TextWrapping.Wrap;
    public bool EllipsisWhenConstrained { get; set; }
    public int? MaximumLines { get; set; }

    // Box model
    public float? MarginTop { get; set; }
    public float? MarginBottom { get; set; }
    public float? MarginLeft { get; set; }
    public float? MarginRight { get; set; }
    public float? PaddingTop { get; set; }
    public float? PaddingBottom { get; set; }
    public float? PaddingLeft { get; set; }
    public float? PaddingRight { get; set; }

    public float? MaxWidth { get; set; }
    public float Rotation { get; set; } = 0f;
    public FlowDirection FlowDirection { get; set; } = FlowDirection.LeftToRight;
    public TextDirection Direction { get; set; } = TextDirection.Automatic;

    public RichTextElement(float x, float y) : base(x, y) { }

    public bool KeepWithNext { get; set; } = false;
    public bool AvoidBreakInside { get; set; } = true;

    internal RichTextLayoutResult? ShapedLayout { get; set; }
    internal float ShapedLayoutWidth { get; set; }
    internal int ShapedStartLine { get; set; }
    internal int ShapedLineCount { get; set; }
}
