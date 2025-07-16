using PdfBuilder.Document;
using PdfBuilder.Models;

public class TextElement : PdfElement
{
    public string Text { get; set; } = "";

    // Font
    public float FontSize { get; set; } = 12;
    public string FontFamily { get; set; } = "Helvetica";
    public bool Bold { get; set; } = false;
    public bool Italic { get; set; } = false;
    public bool Underline { get; set; } = false;
    public bool Strikethrough { get; set; } = false;
    public bool Overline { get; set; } = false;
    public bool SmallCaps { get; set; } = false;
    public bool Monospace { get; set; } = false;

    // Color and styling
    public string Color { get; set; } = "black";
    public float Opacity { get; set; } = 1.0f;
    public string? BackgroundColor { get; set; } = null;

    // Background box styling
    public string? BackgroundBorderColor { get; set; }
    public float? BackgroundBorderWidth { get; set; }
    public float? BackgroundCornerRadius { get; set; }
    public float? BackgroundCornerRadiusTopLeft { get; set; }
    public float? BackgroundCornerRadiusTopRight { get; set; }
    public float? BackgroundCornerRadiusBottomLeft { get; set; }
    public float? BackgroundCornerRadiusBottomRight { get; set; }
    public float? BackgroundShadowOffsetX { get; set; }
    public float? BackgroundShadowOffsetY { get; set; }
    public float? BackgroundShadowBlur { get; set; }
    public string? BackgroundShadowColor { get; set; }

    // Margins
    public float? MarginTop { get; set; }
    public float? MarginBottom { get; set; }
    public float? MarginLeft { get; set; }
    public float? MarginRight { get; set; }

    // Padding
    public float? PaddingTop { get; set; }
    public float? PaddingBottom { get; set; }
    public float? PaddingLeft { get; set; }
    public float? PaddingRight { get; set; }

    // Wrapping and spacing
    public float? MaxWidth { get; set; } = null;
    public float LineHeight { get; set; } = 1.2f;

    // Position, alignment
    public float Rotation { get; set; } = 0;
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public float? BaselineOffset { get; set; }

    public TextElement() : base(0, 0) { }
    public TextElement(string text, float x, float y) : base(x, y) { Text = text; }
}
