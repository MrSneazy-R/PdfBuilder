using System;
using System.Collections.Generic;
using System.Drawing;
using PdfBuilder.Elements;

namespace PdfBuilder.Models
{
    public sealed class TextStyleDefaults
    {
        public FlowDirection FlowDirection { get; set; } = FlowDirection.LeftToRight;

        public string? FontFamily { get; set; } = "Helvetica";
        public float? FontSize { get; set; } = 12f;
        public float? LineHeight { get; set; } = 1.2f;
        public string? Color { get; set; } = "black";
        public string? BackgroundColor { get; set; }
        public bool? Bold { get; set; }
        public bool? Italic { get; set; }
        public bool? Underline { get; set; }
        public bool? Strikethrough { get; set; }
        public bool? SmallCaps { get; set; }
        public bool? Monospace { get; set; }
        public float? Opacity { get; set; }
        public TextAlignment? Alignment { get; set; }
        public float? BaselineOffset { get; set; }
        public float? LetterSpacing { get; set; }
        public float? WordSpacing { get; set; }
        public string? DecorationColor { get; set; }
        public float? DecorationThickness { get; set; }
        public TextDecorationStyle? DecorationStyle { get; set; }
        public bool? Overline { get; set; }
        public TextTransform? Transform { get; set; }
        public List<string>? FallbackFonts { get; set; }
        public TextDirection? Direction { get; set; } = TextDirection.Automatic;
        public TextWrapping? Wrapping { get; set; } = TextWrapping.Wrap;
        public bool? Ellipsis { get; set; }
        public int? MaximumLines { get; set; }
        public bool? Superscript { get; set; }
        public bool? Subscript { get; set; }

        internal static TextStyleDefaults CreateOverrides() => new()
        {
            FontFamily = null,
            FontSize = null,
            LineHeight = null,
            Color = null,
            Direction = null,
            Wrapping = null
        };

        public TextStyleDefaults Clone()
        {
            return new TextStyleDefaults
            {
                FontFamily = FontFamily,
                FontSize = FontSize,
                LineHeight = LineHeight,
                Color = Color,
                BackgroundColor = BackgroundColor,
                Bold = Bold,
                Italic = Italic,
                Underline = Underline,
                Strikethrough = Strikethrough,
                SmallCaps = SmallCaps,
                Monospace = Monospace,
                Opacity = Opacity,
                Alignment = Alignment,
                BaselineOffset = BaselineOffset,
                LetterSpacing = LetterSpacing,
                WordSpacing = WordSpacing,
                DecorationColor = DecorationColor,
                DecorationThickness = DecorationThickness,
                DecorationStyle = DecorationStyle,
                Overline = Overline,
                Transform = Transform,
                FallbackFonts = FallbackFonts != null ? new List<string>(FallbackFonts) : null,
                FlowDirection = FlowDirection,
                Direction = Direction,
                Wrapping = Wrapping,
                Ellipsis = Ellipsis,
                MaximumLines = MaximumLines,
                Superscript = Superscript,
                Subscript = Subscript
            };
        }

        public void CopyFrom(TextStyleDefaults other)
        {
            if (other == null) return;

            FontFamily = other.FontFamily;
            FontSize = other.FontSize;
            LineHeight = other.LineHeight;
            Color = other.Color;
            BackgroundColor = other.BackgroundColor;
            Bold = other.Bold;
            Italic = other.Italic;
            Underline = other.Underline;
            Strikethrough = other.Strikethrough;
            SmallCaps = other.SmallCaps;
            Monospace = other.Monospace;
            Opacity = other.Opacity;
            Alignment = other.Alignment;
            BaselineOffset = other.BaselineOffset;
            LetterSpacing = other.LetterSpacing;
            WordSpacing = other.WordSpacing;
            DecorationColor = other.DecorationColor;
            DecorationThickness = other.DecorationThickness;
            DecorationStyle = other.DecorationStyle;
            Overline = other.Overline;
            Transform = other.Transform;
            FallbackFonts = other.FallbackFonts != null ? new List<string>(other.FallbackFonts) : null;
            FlowDirection = other.FlowDirection;
            Direction = other.Direction;
            Wrapping = other.Wrapping;
            Ellipsis = other.Ellipsis;
            MaximumLines = other.MaximumLines;
            Superscript = other.Superscript;
            Subscript = other.Subscript;
        }

        internal void ApplyOverridesTo(TextStyleDefaults target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (FontFamily != null) target.FontFamily = FontFamily;
            if (FontSize.HasValue) target.FontSize = FontSize;
            if (LineHeight.HasValue) target.LineHeight = LineHeight;
            if (Color != null) target.Color = Color;
            if (BackgroundColor != null) target.BackgroundColor = BackgroundColor;
            if (Bold.HasValue) target.Bold = Bold;
            if (Italic.HasValue) target.Italic = Italic;
            if (Underline.HasValue) target.Underline = Underline;
            if (Strikethrough.HasValue) target.Strikethrough = Strikethrough;
            if (SmallCaps.HasValue) target.SmallCaps = SmallCaps;
            if (Monospace.HasValue) target.Monospace = Monospace;
            if (Opacity.HasValue) target.Opacity = Opacity;
            if (Alignment.HasValue) target.Alignment = Alignment;
            if (BaselineOffset.HasValue) target.BaselineOffset = BaselineOffset;
            if (LetterSpacing.HasValue) target.LetterSpacing = LetterSpacing;
            if (WordSpacing.HasValue) target.WordSpacing = WordSpacing;
            if (DecorationColor != null) target.DecorationColor = DecorationColor;
            if (DecorationThickness.HasValue) target.DecorationThickness = DecorationThickness;
            if (DecorationStyle.HasValue) target.DecorationStyle = DecorationStyle;
            if (Overline.HasValue) target.Overline = Overline;
            if (Transform.HasValue) target.Transform = Transform;
            if (FallbackFonts != null) target.FallbackFonts = new List<string>(FallbackFonts);
            if (Direction.HasValue) { target.Direction = Direction; target.FlowDirection = FlowDirection; }
            if (Wrapping.HasValue) target.Wrapping = Wrapping;
            if (Ellipsis.HasValue) target.Ellipsis = Ellipsis;
            if (MaximumLines.HasValue) target.MaximumLines = MaximumLines;
            if (Superscript.HasValue) target.Superscript = Superscript;
            if (Subscript.HasValue) target.Subscript = Subscript;
        }

        public void ApplyTo(TextElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            if (!string.IsNullOrWhiteSpace(FontFamily))
                element.FontFamily = FontFamily!;
            if (FontSize.HasValue)
                element.FontSize = FontSize.Value;
            if (LineHeight.HasValue)
                element.LineHeight = LineHeight.Value;
            if (!string.IsNullOrWhiteSpace(Color))
                element.Color = Color!;
            if (!string.IsNullOrWhiteSpace(BackgroundColor))
                element.BackgroundColor = BackgroundColor;
            if (Direction.HasValue)
            {
                element.Direction = Direction.Value;
                element.FlowDirection = ResolveFlowDirection();
            }
            if (Bold.HasValue)
                element.Bold = Bold.Value;
            if (Italic.HasValue)
                element.Italic = Italic.Value;
            if (Underline.HasValue)
                element.Underline = Underline.Value;
            if (Strikethrough.HasValue)
                element.Strikethrough = Strikethrough.Value;
            if (Overline.HasValue)
                element.Overline = Overline.Value;
            if (SmallCaps.HasValue)
                element.SmallCaps = SmallCaps.Value;
            if (Monospace.HasValue)
                element.Monospace = Monospace.Value;
            if (Opacity.HasValue)
                element.Opacity = Opacity.Value;
            if (Alignment.HasValue)
                element.Alignment = Alignment.Value;
            if (BaselineOffset.HasValue)
                element.BaselineOffset = BaselineOffset.Value;
            if (LetterSpacing.HasValue)
                element.LetterSpacing = LetterSpacing.Value;
            if (WordSpacing.HasValue)
                element.WordSpacing = WordSpacing.Value;
            if (!string.IsNullOrWhiteSpace(DecorationColor))
                element.DecorationColor = DecorationColor!;
            if (DecorationThickness.HasValue)
                element.DecorationThickness = DecorationThickness.Value;
            if (DecorationStyle.HasValue)
                element.DecorationStyle = DecorationStyle.Value;
            if (Transform.HasValue)
                element.Transform = Transform.Value;
            if (FallbackFonts != null)
                element.FallbackFonts = new List<string>(FallbackFonts);
            if (Wrapping.HasValue)
                element.Wrapping = Wrapping.Value;
            if (Ellipsis.HasValue)
                element.EllipsisWhenConstrained = Ellipsis.Value;
            if (MaximumLines.HasValue)
                element.MaximumLines = MaximumLines;
            if (Superscript == true)
                element.BaselineOffset = element.FontSize * 0.35f;
            else if (Subscript == true)
                element.BaselineOffset = element.FontSize * -0.20f;
        }

        public void ApplyTo(RichTextElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            if (!string.IsNullOrWhiteSpace(FontFamily))
                element.FontFamily = FontFamily!;
            if (FontSize.HasValue)
                element.FontSize = FontSize.Value;
            if (LineHeight.HasValue)
                element.LineHeight = LineHeight.Value;
            if (Alignment.HasValue)
                element.Alignment = Alignment.Value;
            if (Direction.HasValue)
            {
                element.Direction = Direction.Value;
                element.FlowDirection = ResolveFlowDirection();
            }
            if (!string.IsNullOrWhiteSpace(Color))
                element.Color = Color!;
            if (!string.IsNullOrWhiteSpace(BackgroundColor))
                element.BackgroundColor = BackgroundColor;
            if (Wrapping.HasValue)
                element.Wrapping = Wrapping.Value;
            if (Ellipsis.HasValue)
                element.EllipsisWhenConstrained = Ellipsis.Value;
            if (MaximumLines.HasValue)
                element.MaximumLines = MaximumLines;
            if (FallbackFonts != null)
                element.FallbackFonts = new List<string>(FallbackFonts);
        }

        public void ApplyTo(ListElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            if (!string.IsNullOrWhiteSpace(FontFamily))
                element.FontFamily = FontFamily!;
            if (FontSize.HasValue)
                element.FontSize = FontSize.Value;
            if (!string.IsNullOrWhiteSpace(Color))
                element.Color = Color!;
            if (LineHeight.HasValue)
                element.LineHeight = LineHeight.Value;
            element.FlowDirection = FlowDirection;
        }

        public void ApplyTo(RichRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));

            if (!string.IsNullOrWhiteSpace(FontFamily))
                run.FontFamily = FontFamily!;
            if (FontSize.HasValue)
                run.FontSize = FontSize.Value;
            if (!string.IsNullOrWhiteSpace(Color))
                run.Color = Color!;
            if (Bold.HasValue)
                run.Bold = Bold.Value;
            if (Italic.HasValue)
                run.Italic = Italic.Value;
            if (Underline.HasValue)
                run.Underline = Underline.Value;
            if (Strikethrough.HasValue)
                run.Strikethrough = Strikethrough.Value;
            if (SmallCaps.HasValue)
                run.SmallCaps = SmallCaps.Value;
            if (Monospace.HasValue)
                run.Monospace = Monospace.Value;
            if (FallbackFonts != null)
                run.FallbackFonts = new List<string>(FallbackFonts);
            if (LetterSpacing.HasValue)
                run.LetterSpacing = LetterSpacing;
            if (WordSpacing.HasValue)
                run.WordSpacing = WordSpacing;
            if (Transform.HasValue && Transform.Value != TextTransform.None)
                run.Transform = Transform;
            if (!string.IsNullOrWhiteSpace(BackgroundColor))
                run.BackgroundColor = BackgroundColor;
            if (Overline.HasValue)
                run.Overline = Overline.Value;
            if (!string.IsNullOrWhiteSpace(DecorationColor))
                run.DecorationColor = DecorationColor;
            if (DecorationThickness.HasValue)
                run.DecorationThickness = DecorationThickness;
            if (DecorationStyle.HasValue)
                run.DecorationStyle = DecorationStyle.Value;
            if (Superscript.HasValue)
                run.Superscript = Superscript.Value;
            if (Subscript.HasValue)
                run.Subscript = Subscript.Value;
        }

        public void ApplyTo(PdfBuilder.Elements.Table.TextStyle style)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));

            if (!string.IsNullOrWhiteSpace(FontFamily))
                style.FontFamily = FontFamily!;
            if (FontSize.HasValue)
                style.FontSize = FontSize.Value;
            if (!string.IsNullOrWhiteSpace(Color))
            {
                try { style.TextColor = ColorTranslator.FromHtml(Color!); }
                catch { }
            }
            if (LetterSpacing.HasValue)
                style.LetterSpacing = LetterSpacing;
            if (WordSpacing.HasValue)
                style.WordSpacing = WordSpacing;
            if (Bold.HasValue)
                style.Bold = Bold.Value;
            if (Italic.HasValue)
                style.Italic = Italic.Value;
            if (SmallCaps.HasValue)
                style.SmallCaps = SmallCaps.Value;
            if (DecorationColor != null)
            {
                try { style.DecorationColor = ColorTranslator.FromHtml(DecorationColor); }
                catch { }
            }
            if (DecorationThickness.HasValue)
                style.DecorationThickness = DecorationThickness;
            if (DecorationStyle.HasValue)
                style.DecorationStyle = DecorationStyle.Value;
            if (LineHeight.HasValue)
                style.LineHeight = LineHeight;
            if (Alignment.HasValue)
                style.HorizontalAlign = Alignment.Value switch
                {
                    TextAlignment.Center => PdfBuilder.Document.HorizontalAlign.Center,
                    TextAlignment.Right => PdfBuilder.Document.HorizontalAlign.Right,
                    _ => PdfBuilder.Document.HorizontalAlign.Left
                };
            if (FallbackFonts != null)
                style.FallbackFonts = new List<string>(FallbackFonts);
            if (Direction.HasValue)
            {
                style.Direction = Direction;
                style.FlowDirection = ResolveFlowDirection();
            }
            if (!string.IsNullOrWhiteSpace(BackgroundColor))
            {
                try { style.BackgroundColor = ColorTranslator.FromHtml(BackgroundColor); }
                catch { }
            }
            if (Overline.HasValue)
                style.Overline = Overline.Value;
            if (Underline.HasValue)
                style.Underline = Underline.Value;
            if (Strikethrough.HasValue)
                style.Strikethrough = Strikethrough.Value;
            if (Superscript.HasValue)
                style.Superscript = Superscript.Value;
            if (Subscript.HasValue)
                style.Subscript = Subscript.Value;
            if (Wrapping.HasValue || Ellipsis == true)
                style.Wrap = Wrapping switch
                {
                    TextWrapping.NoWrap => PdfBuilder.Elements.Table.TextWrapMode.NoWrap,
                    TextWrapping.Hyphenate => PdfBuilder.Elements.Table.TextWrapMode.Hyphenate,
                    _ when Ellipsis == true => PdfBuilder.Elements.Table.TextWrapMode.EllipsisWhenClipped,
                    _ => PdfBuilder.Elements.Table.TextWrapMode.Wrap
                };
        }

        internal FlowDirection ResolveFlowDirection()
        {
            return Direction switch
            {
                TextDirection.LeftToRight => FlowDirection.LeftToRight,
                TextDirection.RightToLeft => FlowDirection.RightToLeft,
                _ => FlowDirection
            };
        }
    }
}
