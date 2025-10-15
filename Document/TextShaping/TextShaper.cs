using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace PdfBuilder.TextShaping
{
    internal sealed class TextShaper
    {
        private static readonly TextShaper _shared = new();
        public static TextShaper Shared => _shared;

        private readonly SKFontManager _fontManager;
        private readonly Dictionary<string, SKTypeface> _typefaceCache = new(StringComparer.OrdinalIgnoreCase);

        private TextShaper()
        {
            _fontManager = SKFontManager.Default;
        }

        public ShapedParagraph ShapeParagraph(TextShapingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string text = request.Text?.Replace("\r\n", "\n") ?? string.Empty;
            if (request.SmallCaps && text.Length > 0)
                text = text.ToUpperInvariant();

            var lines = new List<ShapedLine>();
            float maxWidth = 0f;

            string[] paragraphs = text.Split('\n');
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length == 0)
                {
                    lines.Add(CreateEmptyLine(request));
                    continue;
                }

                var shapedLines = ShapeParagraphLines(paragraph, request);
                foreach (var line in shapedLines)
                {
                    lines.Add(line);
                    if (line.Width > maxWidth)
                        maxWidth = line.Width;
                }
            }

            if (lines.Count == 0)
                lines.Add(CreateEmptyLine(request));

            float totalHeight = lines.Sum(l => l.LineHeight);
            return new ShapedParagraph(text, lines, maxWidth, totalHeight);
        }

        private List<ShapedLine> ShapeParagraphLines(string paragraph, TextShapingRequest request)
        {
            float maxWidth = request.MaxWidth <= 0 ? float.PositiveInfinity : request.MaxWidth;
            var lines = new List<ShapedLine>();

            var words = paragraph.Split(' ');
            string currentText = string.Empty;
            ShapedLine? currentShape = null;

            foreach (var word in words)
            {
                string candidate = string.IsNullOrEmpty(currentText) ? word : $"{currentText} {word}";
                var candidateShape = ShapeLine(candidate, request);

                if (candidateShape.Width <= maxWidth || float.IsInfinity(maxWidth) || string.IsNullOrEmpty(currentText))
                {
                    currentText = candidate;
                    currentShape = candidateShape;
                }
                else
                {
                    if (currentShape != null)
                        lines.Add(currentShape);
                    else if (!string.IsNullOrEmpty(currentText))
                        lines.Add(ShapeLine(currentText, request));

                    currentText = word;
                    currentShape = ShapeLine(currentText, request);
                }
            }

            if (currentShape != null)
                lines.Add(currentShape);
            else if (!string.IsNullOrEmpty(currentText))
                lines.Add(ShapeLine(currentText, request));

            return lines;
        }

        private ShapedLine ShapeLine(string text, TextShapingRequest request)
        {
            if (string.IsNullOrEmpty(text))
            {
                var emptyRun = CreateEmptyRun(request);
                return new ShapedLine(string.Empty, new List<ShapedRun> { emptyRun }, 0f, emptyRun.Ascent, emptyRun.Descent, GetLineHeight(request.FontSize, request.LineHeight, emptyRun.Ascent, emptyRun.Descent));
            }

            var runs = new List<ShapedRun>();
            float width = 0f;
            float lineAscent = 0f;
            float lineDescent = 0f;

            foreach (var segment in SegmentText(text, request))
            {
                var run = ShapeRun(segment, request);
                runs.Add(run);
                width += run.Width;
                if (run.Ascent > lineAscent) lineAscent = run.Ascent;
                if (run.Descent > lineDescent) lineDescent = run.Descent;
            }

            float lineHeight = GetLineHeight(request.FontSize, request.LineHeight, lineAscent, lineDescent);
            return new ShapedLine(text, runs, width, lineAscent, lineDescent, lineHeight);
        }

        private static float GetLineHeight(float fontSize, float lineHeightMultiplier, float ascent, float descent)
        {
            float preferred = fontSize * lineHeightMultiplier;
            float natural = ascent + descent;
            return Math.Max(preferred, natural);
        }

        private IEnumerable<TextSegment> SegmentText(string text, TextShapingRequest request)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            var builder = new StringBuilder();
            SKTypeface? currentTypeface = null;
            foreach (var rune in text.EnumerateRunes())
            {
                string runeText = rune.ToString();
                SKTypeface typefaceForRune = ResolveTypefaceForRune(request, rune);

                if (currentTypeface == null)
                {
                    currentTypeface = typefaceForRune;
                    builder.Append(runeText);
                }
                else
                {
                    builder.Append(runeText);
                    if (!currentTypeface.ContainsGlyphs(builder.ToString()))
                    {
                        builder.Length -= runeText.Length;
                        if (builder.Length > 0)
                        {
                            yield return new TextSegment(builder.ToString(), currentTypeface, request.FontFamily, request.FontSize, request.Bold, request.Italic);
                        }

                        builder.Clear();
                        builder.Append(runeText);
                        currentTypeface = typefaceForRune;
                    }
                }
            }

            if (builder.Length > 0 && currentTypeface != null)
            {
                yield return new TextSegment(builder.ToString(), currentTypeface, request.FontFamily, request.FontSize, request.Bold, request.Italic);
            }
        }

        private ShapedRun ShapeRun(TextSegment segment, TextShapingRequest request)
        {
            using var paint = new SKPaint
            {
                Typeface = segment.Typeface,
                TextSize = segment.FontSize,
                IsAntialias = true,
                SubpixelText = true,
                HintingLevel = SKPaintHinting.NoHinting,
                LcdRenderText = true,
                TextEncoding = SKTextEncoding.Utf16
            };

            using var shaper = new SKShaper(segment.Typeface);
            var result = shaper.Shape(segment.Text, 0, 0, paint);

            var glyphs = new List<ShapedGlyph>(result?.Codepoints?.Length ?? 0);
            if (result?.Codepoints != null && result.Points != null && result.Clusters != null)
            {
                var clusterRanges = BuildClusterRanges(result.Clusters, segment.Text);
                using var font = new SKFont(segment.Typeface, segment.FontSize);
                var glyphCodes = result.Codepoints;
                var glyphWidths = glyphCodes.Length > 0 ? new float[glyphCodes.Length] : Array.Empty<float>();
                if (glyphCodes.Length > 0)
                {
                    var glyphIndices = new ushort[glyphCodes.Length];
                    for (int i = 0; i < glyphCodes.Length; i++)
                        glyphIndices[i] = (ushort)glyphCodes[i];
                    font.GetGlyphWidths(glyphIndices, glyphWidths, Span<SKRect>.Empty);
                }

                for (int i = 0; i < result.Codepoints.Length; i++)
                {
                    uint glyphId = result.Codepoints[i];
                    var pt = result.Points[i];
                    int cluster = (int)result.Clusters[i];
                    string unicode = clusterRanges.TryGetValue(cluster, out var range)
                        ? segment.Text.Substring(range.start, Math.Max(0, range.end - range.start))
                        : segment.Text;

                    float designAdvance = glyphWidths.Length > i ? glyphWidths[i] : 0f;
                    float nextX = i + 1 < result.Points.Length ? result.Points[i + 1].X : result.Width;
                    float actualAdvance = nextX - pt.X;

                    glyphs.Add(new ShapedGlyph(
                        glyphId,
                        pt.X,
                        pt.Y,
                        actualAdvance,
                        0f,
                        0f,
                        0f,
                        designAdvance,
                        cluster,
                        unicode));
                }

                // HarfBuzz width is in result.Width
            }

            float runWidth = result?.Width ?? glyphs.Sum(g => g.AdvanceX);
            using var metricsFont = new SKFont(segment.Typeface, segment.FontSize);
            var metrics = metricsFont.Metrics;
            float ascent = Math.Abs(metrics.Ascent);
            float descent = Math.Abs(metrics.Descent);

            return new ShapedRun(segment.Text, segment.FontFamily, segment.FontSize, segment.Bold, segment.Italic, segment.Typeface, glyphs, runWidth, ascent, descent);
        }

        private static Dictionary<int, (int start, int end)> BuildClusterRanges(IReadOnlyList<uint> clusters, string text)
        {
            var unique = new SortedSet<int>();
            foreach (var cluster in clusters)
                unique.Add((int)cluster);

            var list = unique.ToList();
            var dict = new Dictionary<int, (int start, int end)>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                int start = list[i];
                int end = (i + 1 < list.Count) ? list[i + 1] : text.Length;
                if (end < start)
                    (start, end) = (end, start);
                if (end < start)
                    end = start;
                dict[start] = (start, Math.Min(text.Length, end));
            }
            if (!dict.ContainsKey(0))
                dict[0] = (0, text.Length);
            return dict;
        }

        private ShapedRun CreateEmptyRun(TextShapingRequest request)
        {
            var typeface = ResolveTypeface(request.FontFamily, request.Bold, request.Italic, request.Monospace);
            using var font = new SKFont(typeface, request.FontSize);
            var metrics = font.Metrics;
            float ascent = Math.Abs(metrics.Ascent);
            float descent = Math.Abs(metrics.Descent);
            return new ShapedRun(string.Empty, request.FontFamily, request.FontSize, request.Bold, request.Italic, typeface, Array.Empty<ShapedGlyph>(), 0f, ascent, descent);
        }

        private ShapedLine CreateEmptyLine(TextShapingRequest request)
        {
            var emptyRun = CreateEmptyRun(request);
            float lineHeight = GetLineHeight(request.FontSize, request.LineHeight, emptyRun.Ascent, emptyRun.Descent);
            return new ShapedLine(string.Empty, new List<ShapedRun> { emptyRun }, 0f, emptyRun.Ascent, emptyRun.Descent, lineHeight);
        }

        private SKTypeface ResolveTypeface(string fontFamily, bool bold, bool italic, bool monospace)
        {
            string resolvedFamily = monospace ? ResolveMonospaceFamily(fontFamily) : fontFamily;
            string key = $"{resolvedFamily}|{(bold ? "b" : "n")}|{(italic ? "i" : "r")}";
            if (_typefaceCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

            var typeface = _fontManager.MatchFamily(resolvedFamily, style) ?? SKTypeface.Default;
            _typefaceCache[key] = typeface;
            return typeface;
        }

        private static string ResolveMonospaceFamily(string requested)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                string lower = requested.Trim().ToLowerInvariant();
                if (lower.Contains("courier") || lower.Contains("mono") || lower.Contains("code"))
                    return requested;
            }
            return "Courier New";
        }

        private SKTypeface ResolveTypefaceForRune(TextShapingRequest request, Rune rune)
        {
            string runeString = rune.ToString();

            var primary = ResolveTypeface(request.FontFamily, request.Bold, request.Italic, request.Monospace);
            if (primary.ContainsGlyphs(runeString))
                return primary;

            if (request.FallbackFonts != null)
            {
                foreach (var fallback in request.FallbackFonts)
                {
                    var fallbackFace = ResolveTypeface(fallback, request.Bold, request.Italic, request.Monospace);
                    if (fallbackFace.ContainsGlyphs(runeString))
                        return fallbackFace;
                }
            }

            var weight = request.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = request.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

            var matched = _fontManager.MatchCharacter(request.FontFamily, style, Array.Empty<string>(), rune.Value);
            if (matched != null)
                return matched;

            matched = _fontManager.MatchCharacter(null, style, Array.Empty<string>(), rune.Value);
            return matched ?? SKTypeface.Default;
        }

        private readonly struct TextSegment
        {
            public TextSegment(string text, SKTypeface typeface, string fontFamily, float fontSize, bool bold, bool italic)
            {
                Text = text ?? string.Empty;
                Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
                FontFamily = fontFamily ?? "Helvetica";
                FontSize = fontSize;
                Bold = bold;
                Italic = italic;
            }

            public string Text { get; }
            public SKTypeface Typeface { get; }
            public string FontFamily { get; }
            public float FontSize { get; }
            public bool Bold { get; }
            public bool Italic { get; }
        }
    }
}
