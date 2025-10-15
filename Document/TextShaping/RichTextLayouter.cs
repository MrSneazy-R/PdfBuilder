using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PdfBuilder.Document.TextShaping;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.TextShaping
{
    internal static class RichTextLayouter
    {
        public static RichTextLayoutResult Layout(RichTextElement element, float innerWidth)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            float availableWidth = innerWidth > 0f ? innerWidth : float.PositiveInfinity;
            var segments = EnumerateSegments(element).ToList();

            var lines = new List<RichTextLine>();
            var currentSegments = new List<RichTextSegment>();
            float currentWidth = 0f;
            float currentAscent = 0f;
            float currentDescent = 0f;
            float currentMaxFontSize = 0f;

            float maxLineWidth = 0f;
            float totalHeight = 0f;
            float defaultLineHeight = element.FontSize * (element.LineHeight > 0f ? element.LineHeight : 1.2f);
            float defaultAscent = element.FontSize * 0.8f;
            float defaultDescent = element.FontSize * 0.2f;

            void FinalizeLine(bool forceEmptyLine)
            {
                if (currentSegments.Count == 0)
                {
                    if (!forceEmptyLine && lines.Count > 0)
                    {
                        ResetLineState();
                        return;
                    }

                    float lineHeightEmpty = Math.Max(defaultLineHeight, defaultAscent + defaultDescent);
                    lines.Add(new RichTextLine(Array.Empty<RichTextSegment>(), 0f, defaultAscent, defaultDescent, lineHeightEmpty));
                    totalHeight += lineHeightEmpty;
                    ResetLineState();
                    return;
                }

                float naturalHeight = currentAscent + currentDescent;
                float candidate = Math.Max(currentMaxFontSize * (element.LineHeight > 0f ? element.LineHeight : 1.2f), naturalHeight);
                float lineHeight = Math.Max(candidate, naturalHeight);
                lines.Add(new RichTextLine(currentSegments.ToArray(), currentWidth, currentAscent, currentDescent, lineHeight));
                maxLineWidth = Math.Max(maxLineWidth, currentWidth);
                totalHeight += lineHeight;
                ResetLineState();
            }

            void ResetLineState()
            {
                currentSegments = new List<RichTextSegment>();
                currentWidth = 0f;
                currentAscent = 0f;
                currentDescent = 0f;
                currentMaxFontSize = 0f;
            }

            foreach (var segment in segments)
            {
                if (segment.ForceLineBreak)
                {
                    FinalizeLine(forceEmptyLine: true);
                    continue;
                }

                if (segment.IsWhitespace && currentSegments.Count == 0)
                {
                    continue;
                }

                bool exceedsWidth = !float.IsPositiveInfinity(availableWidth) &&
                                    currentSegments.Count > 0 &&
                                    currentWidth + segment.ShapedRun.Width > availableWidth + 0.1f;

                if (exceedsWidth)
                {
                    FinalizeLine(forceEmptyLine: false);
                    if (segment.IsWhitespace)
                        continue;
                }

                currentSegments.Add(segment.ToRichTextSegment());
                currentWidth += segment.ShapedRun.Width;
                currentAscent = Math.Max(currentAscent, segment.ShapedRun.Ascent);
                currentDescent = Math.Max(currentDescent, segment.ShapedRun.Descent);
                currentMaxFontSize = Math.Max(currentMaxFontSize, segment.ShapedRun.FontSize);
            }

            if (currentSegments.Count > 0)
                FinalizeLine(forceEmptyLine: false);
            else if (lines.Count == 0)
                FinalizeLine(forceEmptyLine: true);

            if (lines.Count == 0)
            {
                float lineHeightEmpty = Math.Max(defaultLineHeight, defaultAscent + defaultDescent);
                lines.Add(new RichTextLine(Array.Empty<RichTextSegment>(), 0f, defaultAscent, defaultDescent, lineHeightEmpty));
                totalHeight += lineHeightEmpty;
            }

            return new RichTextLayoutResult(lines, maxLineWidth, totalHeight);
        }

        private static IEnumerable<LayoutSegment> EnumerateSegments(RichTextElement element)
        {
            if (element.Runs == null || element.Runs.Count == 0)
                yield break;

            foreach (var run in element.Runs)
            {
                string text = run.Text ?? string.Empty;
                text = text.Replace("\r\n", "\n");
                if (run.SmallCaps)
                    text = text.ToUpperInvariant();

                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string part = lines[i];
                    foreach (var token in SplitPreservingWhitespace(part))
                    {
                        if (token.Length == 0)
                            continue;

                        var shapedRun = ShapeToken(token, run, element);
                        bool isWhitespace = token.All(char.IsWhiteSpace);
                        yield return new LayoutSegment(shapedRun, isWhitespace, false, run.Color, run.Underline, run.Strikethrough, run.LinkUrl, run.LinkAnchor);
                    }

                    if (i < lines.Length - 1)
                        yield return LayoutSegment.LineBreak();
                }
            }
        }

        private static IEnumerable<string> SplitPreservingWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            int index = 0;
            while (index < text.Length)
            {
                int end = index;
                bool whitespace = char.IsWhiteSpace(text[index]);
                if (whitespace)
                {
                    while (end < text.Length && char.IsWhiteSpace(text[end]))
                        end++;
                }
                else
                {
                    while (end < text.Length && !char.IsWhiteSpace(text[end]))
                        end++;
                }

                yield return text.Substring(index, end - index);
                index = end;
            }
        }

        private static ShapedRun ShapeToken(string text, RichRun run, RichTextElement element)
        {
            string fontFamily = string.IsNullOrWhiteSpace(run.FontFamily) ? element.FontFamily : run.FontFamily!;
            float fontSize = run.FontSize ?? element.FontSize;
            bool monospace = string.Equals(fontFamily, "Courier", StringComparison.OrdinalIgnoreCase);

            var request = new TextShapingRequest(
                text,
                fontFamily,
                fontSize,
                lineHeight: 1f,
                maxWidth: float.PositiveInfinity,
                bold: run.Bold,
                italic: run.Italic,
                smallCaps: false,
                monospace: monospace,
                fallbackFonts: null);

            var paragraph = TextShaper.Shared.ShapeParagraph(request);
            var shapedLine = paragraph.Lines.FirstOrDefault();
            if (shapedLine == null || shapedLine.Runs.Count == 0)
            {
                var empty = new ShapedRun(
                    text,
                    fontFamily,
                    fontSize,
                    run.Bold,
                    run.Italic,
                    ResolveTypeface(fontFamily, run.Bold, run.Italic, monospace),
                    Array.Empty<ShapedGlyph>(),
                    0f,
                    fontSize * 0.8f,
                    fontSize * 0.2f);
                return empty;
            }

            return shapedLine.Runs[0];
        }

        private static SkiaSharp.SKTypeface ResolveTypeface(string fontFamily, bool bold, bool italic, bool monospace)
        {
            string resolvedFamily = monospace ? "Courier New" : fontFamily;
            var weight = bold ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal;
            var slant = italic ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright;
            var style = new SkiaSharp.SKFontStyle(weight, SkiaSharp.SKFontStyleWidth.Normal, slant);
            return SkiaSharp.SKFontManager.Default.MatchFamily(resolvedFamily, style) ?? SkiaSharp.SKTypeface.Default;
        }

        private sealed class LayoutSegment
        {
            public LayoutSegment(
                ShapedRun shapedRun,
                bool isWhitespace,
                bool forceLineBreak,
                string color,
                bool underline,
                bool strikethrough,
                string? url,
                string? anchor)
            {
                ShapedRun = shapedRun;
                IsWhitespace = isWhitespace;
                ForceLineBreak = forceLineBreak;
                Color = color ?? "#000";
                Underline = underline;
                Strikethrough = strikethrough;
                Url = url;
                Anchor = anchor;
            }

            private LayoutSegment(bool forceLineBreak)
            {
                ShapedRun = new ShapedRun(string.Empty, "Helvetica", 12f, false, false, SkiaSharp.SKTypeface.Default, Array.Empty<ShapedGlyph>(), 0f, 9f, 3f);
                IsWhitespace = false;
                ForceLineBreak = forceLineBreak;
                Color = "#000";
                Underline = false;
                Strikethrough = false;
                Url = null;
                Anchor = null;
            }

            public ShapedRun ShapedRun { get; }
            public bool IsWhitespace { get; }
            public bool ForceLineBreak { get; }
            public string Color { get; }
            public bool Underline { get; }
            public bool Strikethrough { get; }
            public string? Url { get; }
            public string? Anchor { get; }

            public static LayoutSegment LineBreak() => new LayoutSegment(true);

            public RichTextSegment ToRichTextSegment() =>
                new RichTextSegment(ShapedRun, Color, Underline, Strikethrough, Url, Anchor);
        }
    }
}
