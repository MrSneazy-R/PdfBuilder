using System;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Writer.Rendering
{
    internal static class TextJustification
    {
        internal readonly struct LineJustification
        {
            public LineJustification(bool apply, float wordSpacing, int spaceCount)
            {
                Apply = apply;
                WordSpacing = wordSpacing;
                SpaceCount = spaceCount;
            }

            public bool Apply { get; }
            public float WordSpacing { get; }
            public int SpaceCount { get; }
            public bool HasWordSpacing => Apply && SpaceCount > 0 && Math.Abs(WordSpacing) > 0.0001f;
        }

        public static LineJustification Compute(TextElement element, ShapedLine line, float targetWidth, int lineIndex, int totalLines)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (line == null) throw new ArgumentNullException(nameof(line));

            if (element.Alignment != TextAlignment.Justify)
                return default;

            bool isLastLine = lineIndex == totalLines - 1;
            if (isLastLine && totalLines > 1)
                return default;

            float slack = targetWidth - line.Width;
            if (slack <= 0.1f)
                return default;

            int spaces = CountWordSpacingGlyphs(line);
            if (spaces <= 0)
                return default;

            return new LineJustification(true, slack / spaces, spaces);
        }

        public static int CountWordSpacingGlyphs(ShapedLine line)
        {
            int count = 0;
            foreach (var run in line.Runs)
                count += CountWordSpacingGlyphs(run);
            return count;
        }

        public static int CountWordSpacingGlyphs(ShapedRun run)
        {
            int count = 0;
            foreach (var glyph in run.Glyphs)
            {
                if (IsWordSpacingGlyph(glyph.Unicode))
                    count++;
            }
            return count;
        }

        public static bool IsWordSpacingGlyph(string? unicode)
        {
            if (string.IsNullOrEmpty(unicode))
                return false;

            foreach (char ch in unicode)
            {
                if (ch == ' ' || ch == '\t' || ch == '\u00A0')
                    return true;
                if (!char.IsWhiteSpace(ch))
                    return false;
            }

            return true;
        }
    }
}
