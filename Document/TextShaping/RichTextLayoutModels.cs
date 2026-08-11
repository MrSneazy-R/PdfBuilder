using System;
using System.Collections.Generic;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Document.TextShaping
{
    internal sealed class RichTextLayoutResult
    {
        public RichTextLayoutResult(IReadOnlyList<RichTextLine> lines, float maxLineWidth, float totalHeight)
        {
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            MaxLineWidth = maxLineWidth;
            TotalHeight = totalHeight;
        }

        public IReadOnlyList<RichTextLine> Lines { get; }

        public float MaxLineWidth { get; }

        public float TotalHeight { get; }
    }

    internal sealed class RichTextLine
    {
        public RichTextLine(IReadOnlyList<RichTextSegment> segments, float width, float ascent, float descent, float lineHeight)
        {
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
            Width = width;
            Ascent = ascent;
            Descent = descent;
            LineHeight = lineHeight;
        }

        public IReadOnlyList<RichTextSegment> Segments { get; }

        public float Width { get; }

        public float Ascent { get; }

        public float Descent { get; }

        public float LineHeight { get; }
    }

    internal sealed class RichTextSegment
    {
        public RichTextSegment(
            ShapedRun shapedRun,
            string color,
            bool underline,
            bool strikethrough,
            bool overline,
            string? backgroundColor,
            string? decorationColor,
            float? decorationThickness,
            PdfBuilder.Models.TextDecorationStyle decorationStyle,
            float baselineOffset,
            string? url,
            string? anchor)
        {
            ShapedRun = shapedRun ?? throw new ArgumentNullException(nameof(shapedRun));
            Color = color ?? "black";
            Underline = underline;
            Strikethrough = strikethrough;
            Overline = overline;
            BackgroundColor = backgroundColor;
            DecorationColor = decorationColor;
            DecorationThickness = decorationThickness;
            DecorationStyle = decorationStyle;
            BaselineOffset = baselineOffset;
            Url = url;
            Anchor = anchor;
        }

        public ShapedRun ShapedRun { get; }

        public string Color { get; }

        public bool Underline { get; }

        public bool Strikethrough { get; }
        public bool Overline { get; }
        public string? BackgroundColor { get; }
        public string? DecorationColor { get; }
        public float? DecorationThickness { get; }
        public PdfBuilder.Models.TextDecorationStyle DecorationStyle { get; }
        public float BaselineOffset { get; }

        public string? Url { get; }

        public string? Anchor { get; }

        public bool HasLink => !string.IsNullOrEmpty(Url) || !string.IsNullOrEmpty(Anchor);
    }
}
