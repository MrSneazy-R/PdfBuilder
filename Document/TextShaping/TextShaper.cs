using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PdfBuilder.Fonts;
using PdfBuilder.Models;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace PdfBuilder.TextShaping
{
    internal sealed class TextShaper
    {
        private const int MaxGlyphsPerRun = 512;
        private const int MaxTokenTextElements = 256;

        private static readonly TextShaper _shared = new();
        public static TextShaper Shared => _shared;

        private readonly SKFontManager _fontManager;
        private readonly ConcurrentDictionary<string, SKTypeface> _typefaceCache = new(StringComparer.OrdinalIgnoreCase);

        private TextShaper()
        {
            _fontManager = SKFontManager.Default;
        }

        public ShapedParagraph ShapeParagraph(TextShapingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string text = NormalizeNewlines(request.Text);
            text = ApplyTransform(text, request.Transform, request.SmallCaps);

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
            var current = new StringBuilder();
            ShapedLine? currentShape = null;

            foreach (var token in TokenizeParagraph(paragraph))
            {
                if (token.Length == 0)
                    continue;

                string candidate = current.Length == 0 ? token : string.Concat(current.ToString(), token);
                var candidateShape = ShapeLine(candidate, request);

                if (candidateShape.Width <= maxWidth || float.IsInfinity(maxWidth))
                {
                    current.Clear();
                    current.Append(candidate);
                    currentShape = candidateShape;
                    continue;
                }

                if (current.Length > 0)
                {
                    currentShape ??= ShapeLine(current.ToString(), request);
                    if (!string.IsNullOrWhiteSpace(currentShape.Text))
                        lines.Add(currentShape);
                    current.Clear();
                    currentShape = null;

                    string trimmedRemainder = RemoveLeadingWhitespace(token);
                    if (trimmedRemainder.Length > 0)
                        AppendBrokenPieces(trimmedRemainder);
                }
                else
                {
                    string trimmed = RemoveLeadingWhitespace(token);
                    AppendBrokenPieces(trimmed.Length == 0 ? token : trimmed);
                }
            }

            if (current.Length > 0)
            {
                currentShape ??= ShapeLine(current.ToString(), request);
                lines.Add(currentShape);
            }

            return lines;

            void AppendBrokenPieces(string tokenText)
            {
                foreach (var piece in BreakToken(tokenText, request, maxWidth))
                {
                    if (string.IsNullOrEmpty(piece))
                        continue;

                    var pieceShape = ShapeLine(piece, request);
                    if (!float.IsInfinity(maxWidth) && pieceShape.Width > maxWidth)
                    {
                        lines.Add(pieceShape);
                        current.Clear();
                        currentShape = null;
                        continue;
                    }

                    if (current.Length == 0)
                    {
                        current.Append(piece);
                        currentShape = pieceShape;
                    }
                    else
                    {
                        lines.Add(currentShape ?? ShapeLine(current.ToString(), request));
                        current.Clear();
                        current.Append(piece);
                        currentShape = pieceShape;
                    }
                }
            }
        }

        private static IEnumerable<string> TokenizeParagraph(string paragraph)
        {
            if (string.IsNullOrEmpty(paragraph))
                yield break;

            int start = 0;
            bool isWhitespace = char.IsWhiteSpace(paragraph[0]);
            for (int i = 1; i < paragraph.Length; i++)
            {
                bool currentWhitespace = char.IsWhiteSpace(paragraph[i]);
                if (currentWhitespace != isWhitespace)
                {
                    foreach (var part in SplitToken(paragraph.Substring(start, i - start), isWhitespace))
                        yield return part;
                    start = i;
                    isWhitespace = currentWhitespace;
                }
            }

            if (start < paragraph.Length)
            {
                foreach (var part in SplitToken(paragraph.Substring(start), isWhitespace))
                    yield return part;
            }
        }

        private static IEnumerable<string> SplitToken(string token, bool isWhitespace)
        {
            if (string.IsNullOrEmpty(token))
                yield break;

            if (isWhitespace || token.Length <= MaxTokenTextElements)
            {
                yield return token;
                yield break;
            }

            var enumerator = StringInfo.GetTextElementEnumerator(token);
            var builder = new StringBuilder();
            int count = 0;

            while (enumerator.MoveNext())
            {
                builder.Append(enumerator.GetTextElement());
                count++;

                if (count >= MaxTokenTextElements)
                {
                    yield return builder.ToString();
                    builder.Clear();
                    count = 0;
                }
            }

            if (builder.Length > 0)
                yield return builder.ToString();
        }

        private static string RemoveLeadingWhitespace(string token)
        {
            if (string.IsNullOrEmpty(token))
                return string.Empty;

            int index = 0;
            while (index < token.Length && char.IsWhiteSpace(token[index]))
                index++;
            return index == 0 ? token : token[index..];
        }

        private IEnumerable<string> BreakToken(string token, TextShapingRequest request, float maxWidth)
        {
            if (string.IsNullOrEmpty(token))
                yield break;

            if (float.IsInfinity(maxWidth) || maxWidth <= 0f)
            {
                yield return token;
                yield break;
            }

            var buffer = new StringBuilder();
            ShapedLine? cached = null;

            var enumerator = StringInfo.GetTextElementEnumerator(token);
            while (enumerator.MoveNext())
            {
                string element = enumerator.GetTextElement();
                buffer.Append(element);
                var shape = ShapeLine(buffer.ToString(), request);

                if (shape.Width <= maxWidth)
                {
                    cached = shape;
                    continue;
                }

                if (buffer.Length == element.Length)
                {
                    yield return element;
                    buffer.Clear();
                    cached = null;
                    continue;
                }

                if (cached != null)
                {
                    yield return cached.Text;
                    buffer.Clear();
                    buffer.Append(element);
                    cached = ShapeLine(buffer.ToString(), request);
                    if (cached.Width > maxWidth)
                    {
                        yield return element;
                        buffer.Clear();
                        cached = null;
                    }
                }
                else
                {
                    yield return element;
                    buffer.Clear();
                }
            }

            if (buffer.Length > 0)
            {
                if (cached == null || !string.Equals(cached.Text, buffer.ToString(), StringComparison.Ordinal))
                    cached = ShapeLine(buffer.ToString(), request);
                yield return cached.Text;
            }
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

            if (runs.Count > 0)
            {
                var processed = new List<ShapedRun>(runs.Count);
                foreach (var run in runs)
                {
                    if (run.Glyphs.Count > MaxGlyphsPerRun)
                        processed.AddRange(SplitRun(run));
                    else
                        processed.Add(run);
                }

                if (processed.Count != runs.Count)
                {
                    runs = processed;
                    width = runs.Sum(r => r.Width);
                }
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

        private IEnumerable<TextSegment> SegmentText(string text, TextShapingRequest req)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            SKTypeface? current = null;
            var sb = new StringBuilder();

            foreach (var rune in text.EnumerateRunes())
            {
                var face = ResolveTypefaceForRune(req, rune);
                if (current is null || !ReferenceEquals(current, face))
                {
                    if (sb.Length > 0 && current != null)
                        yield return new TextSegment(sb.ToString(), current, req.FontFamily, req.FontSize, req.Bold, req.Italic);
                    sb.Clear();
                    current = face;
                }
                sb.Append(rune.ToString());
            }
            if (sb.Length > 0 && current != null)
                yield return new TextSegment(sb.ToString(), current, req.FontFamily, req.FontSize, req.Bold, req.Italic);
        }

        private readonly ConcurrentDictionary<SKTypeface, SKShaper> _shaperCache = new();
        private SKShaper GetShaper(SKTypeface tf) =>
            _shaperCache.GetOrAdd(tf, t => new SKShaper(t));
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

            var shaper = GetShaper(segment.Typeface);
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

            var adjustedGlyphs = ApplySpacing(glyphs, request);
            glyphs = adjustedGlyphs;
            float runWidth = glyphs.Sum(g => g.AdvanceX);
            using var metricsFont = new SKFont(segment.Typeface, segment.FontSize);
            var metrics = metricsFont.Metrics;
            float ascent = Math.Abs(metrics.Ascent);
            float descent = Math.Abs(metrics.Descent);

            return new ShapedRun(segment.Text, segment.FontFamily, segment.FontSize, segment.Bold, segment.Italic, segment.Typeface, glyphs, runWidth, ascent, descent);
        }

        private static Dictionary<int, (int start, int end)> BuildClusterRanges(IReadOnlyList<uint> clusters, string text)
        {
            if (clusters == null || clusters.Count == 0)
                return new() { [0] = (0, text.Length) };

            // Distinct, sorted starts in UTF-16 code units
            var starts = new SortedSet<int>(clusters.Select(c => (int)c));
            var list = starts.ToList();

            var dict = new Dictionary<int, (int, int)>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                int start = Math.Clamp(list[i], 0, text.Length);
                int end = (i + 1 < list.Count) ? Math.Clamp(list[i + 1], 0, text.Length) : text.Length;
                if (end < start) (start, end) = (end, start); // defend against odd ordering
                dict[start] = (start, end);
            }
            if (!dict.ContainsKey(0))
                dict[0] = (0, text.Length);
            return dict;
        }

        private static string NormalizeNewlines(string? text) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", "\n");

        private static string ApplyTransform(string text, TextTransform transform, bool smallCaps)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            TextTransform effective = transform;
            if (effective == TextTransform.None && smallCaps)
                effective = TextTransform.SmallCaps;

            return effective switch
            {
                TextTransform.Uppercase => text.ToUpperInvariant(),
                TextTransform.Lowercase => text.ToLowerInvariant(),
                TextTransform.Capitalize => CapitalizeFirst(text),
                TextTransform.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text),
                TextTransform.SmallCaps => text.ToUpperInvariant(),
                _ => text
            };
        }

        private static string CapitalizeFirst(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            if (text.Length == 1)
                return text.ToUpperInvariant();
            return char.ToUpperInvariant(text[0]) + text.Substring(1).ToLowerInvariant();
        }

        private static List<ShapedGlyph> ApplySpacing(List<ShapedGlyph> glyphs, TextShapingRequest request)
        {
            if (glyphs == null || glyphs.Count == 0)
                return glyphs ?? new List<ShapedGlyph>();

            float letterSpacing = request.LetterSpacing ?? 0f;
            float wordSpacing = request.WordSpacing ?? 0f;
            if (Math.Abs(letterSpacing) < 0.0001f && Math.Abs(wordSpacing) < 0.0001f)
                return glyphs;

            var adjusted = new List<ShapedGlyph>(glyphs.Count);
            for (int i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                float extra = 0f;
                bool isLast = i == glyphs.Count - 1;

                if (!isLast && Math.Abs(letterSpacing) > 0.0001f)
                    extra += letterSpacing;

                if (Math.Abs(wordSpacing) > 0.0001f && IsWordSpacingGlyph(glyph.Unicode))
                    extra += wordSpacing;

                if (Math.Abs(extra) > 0.0001f)
                {
                    var modified = new ShapedGlyph(
                        glyph.GlyphId,
                        glyph.X,
                        glyph.Y,
                        glyph.AdvanceX + extra,
                        glyph.AdvanceY,
                        glyph.OffsetX,
                        glyph.OffsetY,
                        glyph.DesignAdvance,
                        glyph.Cluster,
                        glyph.Unicode);
                    modified.AssignedCid = glyph.AssignedCid;
                    adjusted.Add(modified);
                }
                else
                {
                    adjusted.Add(glyph);
                }
            }
            return adjusted;
        }

        private static bool IsWordSpacingGlyph(string unicode)
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
            return _typefaceCache.GetOrAdd(key, _ =>
            {
                var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
                var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

                var custom = FontCatalog.Resolve(resolvedFamily, style);
                if (custom != null)
                    return custom;

                return _fontManager.MatchFamily(resolvedFamily, style) ?? SKTypeface.Default;
            });
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

            foreach (var custom in FontCatalog.EnumerateRegisteredTypefaces())
            {
                if (ReferenceEquals(custom, primary))
                    continue;
                if (custom.ContainsGlyphs(runeString))
                    return custom;
            }

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

        private static IEnumerable<ShapedRun> SplitRun(ShapedRun source)
        {
            var glyphs = source.Glyphs;
            int count = glyphs.Count;
            int index = 0;

            while (index < count)
            {
                int end = Math.Min(index + MaxGlyphsPerRun, count);
                int cluster = glyphs[end - 1].Cluster;
                while (end < count && glyphs[end].Cluster == cluster)
                    end++;

                var slice = new List<ShapedGlyph>(end - index);
                var textBuilder = new StringBuilder();
                float sliceWidth = 0f;

                for (int i = index; i < end; i++)
                {
                    var glyph = glyphs[i];
                    slice.Add(glyph);
                    sliceWidth += glyph.AdvanceX;
                    if (!string.IsNullOrEmpty(glyph.Unicode))
                        textBuilder.Append(glyph.Unicode);
                }

                string sliceText = textBuilder.Length > 0 ? textBuilder.ToString() : source.Text;

                yield return new ShapedRun(
                    sliceText,
                    source.FontFamily,
                    source.FontSize,
                    source.Bold,
                    source.Italic,
                    source.Typeface,
                    slice,
                    sliceWidth,
                    source.Ascent,
                    source.Descent);

                index = end;
            }
        }
    }
}




