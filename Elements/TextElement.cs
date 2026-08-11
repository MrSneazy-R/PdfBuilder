using System.Collections.Generic;
using PdfBuilder.Document;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

public class TextElement : PdfElement
{
    public string Text { get; set; } = string.Empty;

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
    public List<string>? FallbackFonts { get; set; }

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
    public float? LetterSpacing { get; set; }
    public float? WordSpacing { get; set; }
    public TextWrapping Wrapping { get; set; } = TextWrapping.Wrap;
    public bool EllipsisWhenConstrained { get; set; }
    public int? MaximumLines { get; set; }

    // Position, alignment
    public float Rotation { get; set; } = 0;
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public float? BaselineOffset { get; set; }
    public FlowDirection FlowDirection { get; set; } = FlowDirection.LeftToRight;
    public TextDirection Direction { get; set; } = TextDirection.Automatic;
    public TextTransform Transform { get; set; } = TextTransform.None;

    // Decorations
    public string? DecorationColor { get; set; }
    public float? DecorationThickness { get; set; }
    public TextDecorationStyle DecorationStyle { get; set; } = TextDecorationStyle.Solid;

    public TextElement() : base(0, 0)
    {
    }

    public TextElement(string text, float x, float y) : base(x, y)
    {
        Text = text;
    }

    public bool KeepWithNext { get; set; } = false;   // P2 - simple version (no lookahead splitting)
    public bool AvoidBreakInside { get; set; } = true; // paragraphs are atomic now; future: split w/ widows/orphans
    public int WidowLines { get; set; } = 2;          // reserved for future line-splitting
    public int OrphanLines { get; set; } = 2;
    public List<TextSpan> Spans { get; } = new();

    internal ShapedParagraph? ShapedLayout { get; set; }
    internal string? PageTextTemplate { get; set; }
    internal string? ThemeStyleName { get; set; }
    internal TextStyleDefaults? CanonicalStyleOverrides { get; set; }
    internal int ShapedStartLine { get; set; }
    internal int ShapedLineCount { get; set; }
}

public sealed class TextSpan
{
    public string Text { get; set; } = string.Empty;
    public string? FontFamily { get; set; }
    public float? FontSize { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? SmallCaps { get; set; }
    public bool? Monospace { get; set; }
    public float? LetterSpacing { get; set; }
    public float? WordSpacing { get; set; }
    public TextTransform? Transform { get; set; }
    public List<string>? FallbackFonts { get; set; }
    public string? Color { get; set; }
    public string? BackgroundColor { get; set; }
    public bool? Underline { get; set; }
    public bool? Strikethrough { get; set; }
    public bool? Overline { get; set; }
    public string? DecorationColor { get; set; }
    public float? DecorationThickness { get; set; }
    public TextDecorationStyle? DecorationStyle { get; set; }
    public bool? Superscript { get; set; }
    public bool? Subscript { get; set; }

    public TextSpan Clone()
    {
        return new TextSpan
        {
            Text = Text,
            FontFamily = FontFamily,
            FontSize = FontSize,
            Bold = Bold,
            Italic = Italic,
            SmallCaps = SmallCaps,
            Monospace = Monospace,
            LetterSpacing = LetterSpacing,
            WordSpacing = WordSpacing,
            Transform = Transform,
            FallbackFonts = FallbackFonts == null ? null : new List<string>(FallbackFonts)
            ,
            Color = Color
            ,
            BackgroundColor = BackgroundColor
            ,
            Underline = Underline
            ,
            Strikethrough = Strikethrough
            ,
            Overline = Overline
            ,
            DecorationColor = DecorationColor
            ,
            DecorationThickness = DecorationThickness
            ,
            DecorationStyle = DecorationStyle
            ,
            Superscript = Superscript
            ,
            Subscript = Subscript
        };
    }
}
