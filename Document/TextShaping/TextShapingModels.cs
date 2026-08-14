using System;
using System.Collections.Generic;
using PdfBuilder.Models;
using SkiaSharp;

namespace PdfBuilder.TextShaping
{
    internal sealed class TextShapingRequest
    {
        public TextShapingRequest(
            string text,
            string fontFamily,
            float fontSize,
            float lineHeight,
            float maxWidth,
            bool bold,
            bool italic,
            bool smallCaps,
            bool monospace,
            IReadOnlyList<string>? fallbackFonts,
            FlowDirection flowDirection = FlowDirection.LeftToRight,
            float? letterSpacing = null,
            float? wordSpacing = null,
            TextTransform transform = TextTransform.None,
            TextWrapping wrapping = TextWrapping.Wrap)
        {
            Text = text ?? string.Empty;
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Helvetica" : fontFamily;
            FontSize = fontSize > 0 ? fontSize : 12f;
            LineHeight = lineHeight > 0 ? lineHeight : 1.2f;
            MaxWidth = maxWidth;
            Bold = bold;
            Italic = italic;
            SmallCaps = smallCaps;
            Monospace = monospace;
            FallbackFonts = fallbackFonts;
            FallbackFontsCacheKey = fallbackFonts == null
                ? "\0"
                : "\u0001" + string.Join('\u001F', fallbackFonts);
            FlowDirection = flowDirection;
            LetterSpacing = letterSpacing;
            WordSpacing = wordSpacing;
            Transform = transform;
            Wrapping = wrapping;
        }

        public string Text { get; }
        public string FontFamily { get; }
        public float FontSize { get; }
        public float LineHeight { get; }
        public float MaxWidth { get; }
        public bool Bold { get; }
        public bool Italic { get; }
        public bool SmallCaps { get; }
        public bool Monospace { get; }
        public IReadOnlyList<string>? FallbackFonts { get; }
        internal string FallbackFontsCacheKey { get; }
        public FlowDirection FlowDirection { get; }
        public float? LetterSpacing { get; }
        public float? WordSpacing { get; }
        public TextTransform Transform { get; }
        public TextWrapping Wrapping { get; }
    }

    internal sealed class ShapedParagraph
    {
        public ShapedParagraph(string sourceText, IReadOnlyList<ShapedLine> lines, float maxLineWidth, float totalHeight)
        {
            SourceText = sourceText;
            Lines = lines;
            MaxLineWidth = maxLineWidth;
            TotalHeight = totalHeight;
        }

        public string SourceText { get; }
        public IReadOnlyList<ShapedLine> Lines { get; }
        public float MaxLineWidth { get; }
        public float TotalHeight { get; }
    }

    internal sealed class ShapedLine
    {
        public ShapedLine(string text, IReadOnlyList<ShapedRun> runs, float width, float ascent, float descent, float lineHeight)
        {
            Text = text;
            Runs = runs;
            Width = width;
            Ascent = ascent;
            Descent = descent;
            LineHeight = lineHeight;
        }

        public string Text { get; }
        public IReadOnlyList<ShapedRun> Runs { get; }
        public float Width { get; }
        public float Ascent { get; }
        public float Descent { get; }
        public float LineHeight { get; }
    }

    internal sealed class ShapedRun
    {
        public ShapedRun(
            string text,
            string fontFamily,
            float fontSize,
            bool bold,
            bool italic,
            SKTypeface typeface,
            IReadOnlyList<ShapedGlyph> glyphs,
            float width,
            float ascent,
            float descent)
        {
            Text = text;
            FontFamily = fontFamily;
            FontSize = fontSize;
            Bold = bold;
            Italic = italic;
            Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
            Glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            Width = width;
            Ascent = ascent;
            Descent = descent;
        }

        public string Text { get; }
        public string FontFamily { get; }
        public float FontSize { get; }
        public bool Bold { get; }
        public bool Italic { get; }
        public SKTypeface Typeface { get; }
        public IReadOnlyList<ShapedGlyph> Glyphs { get; }
        public float Width { get; }
        public float Ascent { get; }
        public float Descent { get; }
    }

    internal sealed class ShapedGlyph
    {
        public ShapedGlyph(
            uint glyphId,
            float x,
            float y,
            float advanceX,
            float advanceY,
            float offsetX,
            float offsetY,
            float designAdvance,
            int cluster,
            string unicode)
        {
            GlyphId = glyphId;
            X = x;
            Y = y;
            AdvanceX = advanceX;
            AdvanceY = advanceY;
            OffsetX = offsetX;
            OffsetY = offsetY;
            DesignAdvance = designAdvance;
            Cluster = cluster;
            Unicode = unicode;
        }

        public uint GlyphId { get; }
        public float X { get; }
        public float Y { get; }
        public float AdvanceX { get; }
        public float AdvanceY { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float DesignAdvance { get; }
        public int Cluster { get; }
        public string Unicode { get; }

    }
}
