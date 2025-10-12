using System.Collections.Generic;
using System.Drawing;

namespace PdfBuilder.Elements.Table
{
    public enum TextDecorationStyle
    {
        Solid,
        Dotted,
        Dashed,
        Double
    }

    public enum TextWrapMode
    {
        Wrap,
        NoWrap,
        Hyphenate,
        EllipsisWhenClipped
    }

    public sealed class TextStyle
    {
        // Font
        public string FontFamily { get; set; } = "Helvetica";
        public float FontSize { get; set; } = 10f;
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool SmallCaps { get; set; }
        public bool? Kerning { get; set; }

        // Color & background
        public Color TextColor { get; set; } = Color.Black;
        public Color? BackgroundColor { get; set; }
        public float? HighlightPadding { get; set; }

        // Alignment
        public Document.HorizontalAlign HorizontalAlign { get; set; } = Document.HorizontalAlign.Left;
        public Document.VerticalAlign VerticalAlign { get; set; } = Document.VerticalAlign.Top;

        // Spacing
        public float? LineHeight { get; set; }
        public float? LetterSpacing { get; set; }
        public float? WordSpacing { get; set; }
        public float? ParagraphSpacingBefore { get; set; }
        public float? ParagraphSpacingAfter { get; set; }

        // Decorations
        public bool Underline { get; set; }
        public bool Strikethrough { get; set; }
        public Color? DecorationColor { get; set; }
        public float? DecorationThickness { get; set; }
        public TextDecorationStyle DecorationStyle { get; set; } = TextDecorationStyle.Solid;

        // Positioning
        public bool Superscript { get; set; }
        public bool Subscript { get; set; }
        public float RotationDegrees { get; set; }

        // Wrap & overflow
        public TextWrapMode Wrap { get; set; } = TextWrapMode.Wrap;

        // Hyperlinks
        public string? Hyperlink { get; set; }
        public string? ToolTip { get; set; }

        // Fonts
        public List<string>? FallbackFonts { get; set; }

        public TextStyle Clone() => new TextStyle
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            Bold = Bold,
            Italic = Italic,
            SmallCaps = SmallCaps,
            Kerning = Kerning,
            TextColor = TextColor,
            BackgroundColor = BackgroundColor,
            HighlightPadding = HighlightPadding,
            HorizontalAlign = HorizontalAlign,
            VerticalAlign = VerticalAlign,
            LineHeight = LineHeight,
            LetterSpacing = LetterSpacing,
            WordSpacing = WordSpacing,
            ParagraphSpacingBefore = ParagraphSpacingBefore,
            ParagraphSpacingAfter = ParagraphSpacingAfter,
            Underline = Underline,
            Strikethrough = Strikethrough,
            DecorationColor = DecorationColor,
            DecorationThickness = DecorationThickness,
            DecorationStyle = DecorationStyle,
            Superscript = Superscript,
            Subscript = Subscript,
            RotationDegrees = RotationDegrees,
            Wrap = Wrap,
            Hyperlink = Hyperlink,
            ToolTip = ToolTip,
            FallbackFonts = FallbackFonts == null ? null : new List<string>(FallbackFonts)
        };
    }

    public sealed class InlineRun
    {
        public string Text { get; set; } = string.Empty;
        public TextStyle Style { get; set; } = new TextStyle();
        public List<string>? FallbackFonts { get; set; }

        public InlineRun Clone() => new InlineRun
        {
            Text = Text,
            Style = Style.Clone(),
            FallbackFonts = FallbackFonts == null ? null : new List<string>(FallbackFonts)
        };
    }
}
