using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfBuilder.Writer
{
    /// <summary>
    /// Renders TextElement blocks using shaped glyph data and embedded fonts.
    /// </summary>
    public static class TextRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public static void Append(
           StringBuilder sb, TextElement element, float pageHeight,
           PdfRenderContext context)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var paragraph = EnsureShapedParagraph(element);
            int startLine = Math.Clamp(element.ShapedStartLine, 0, Math.Max(0, paragraph.Lines.Count - 1));
            int remaining = paragraph.Lines.Count - startLine;
            int lineCount = element.ShapedLineCount > 0 ? Math.Min(element.ShapedLineCount, remaining) : remaining;
            lineCount = Math.Max(1, lineCount);
            var lines = paragraph.Lines.Skip(startLine).Take(lineCount).ToList();

            float padL = element.PaddingLeft ?? 0f;
            float padR = element.PaddingRight ?? 0f;
            float padT = element.PaddingTop ?? 0f;
            float padB = element.PaddingBottom ?? 0f;

            float maxLineWidth = lines.Max(l => l.Width);
            float textBlockWidth = element.MaxWidth ?? maxLineWidth;
            float boxWidth = textBlockWidth + padL + padR;

            float baseline = element.Y;
            var baselines = new List<float>(lines.Count);
            foreach (var line in lines)
            {
                baselines.Add(baseline);
                baseline -= line.LineHeight;
            }

            float topY = baselines[0] + lines[0].Ascent + padT;
            float bottomY = baselines[^1] - lines[^1].Descent - padB;
            float boxHeight = Math.Max(0f, topY - bottomY);

            DrawBackground(sb, element, textBlockWidth, boxWidth, baselines[0], baselines[^1], lines, padL, padR, padT, padB, topY, bottomY);

            var textRgb = TryRgb(element.Color) ?? "0 0 0";
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                float baselineY = baselines[i];

                float lineX = element.X;
                if (element.Alignment == TextAlignment.Center)
                    lineX += (textBlockWidth - line.Width) / 2f;
                else if (element.Alignment == TextAlignment.Right)
                    lineX += textBlockWidth - line.Width;

                float cursorX = lineX;
                foreach (var run in line.Runs)
                {
                    if (run.Glyphs.Count == 0)
                        continue;

                    var encoded = GlyphRunEncoder.Encode(run, context);
                    sb.Append("BT ");
                    sb.Append($"{encoded.FontResourceName} {N(run.FontSize)} Tf {textRgb} rg ");
                    if (Math.Abs(element.Rotation) < 0.0001)
                    {
                        sb.Append($"{N(cursorX)} {N(baselineY)} Td ");
                    }
                    else
                    {
                        double rad = element.Rotation * Math.PI / 180.0;
                        double cos = Math.Cos(rad);
                        double sin = Math.Sin(rad);
                        sb.Append($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} {N(cursorX)} {N(baselineY)} Tm ");
                    }
                    sb.Append($"{encoded.TjCommand} ET\n");
                    cursorX += run.Width;
                }

                DrawDecorations(sb, element, lineX, line.Width, baselineY);
            }
        }

        private static ShapedParagraph EnsureShapedParagraph(TextElement element)
        {
            if (element.ShapedLayout != null)
                return element.ShapedLayout;

            var request = new TextShapingRequest(
                element.Text ?? string.Empty,
                element.FontFamily,
                element.FontSize > 0 ? element.FontSize : 12f,
                element.LineHeight > 0 ? element.LineHeight : 1.2f,
                element.MaxWidth ?? 0f,
                element.Bold,
                element.Italic,
                element.SmallCaps,
                element.Monospace,
                element.FallbackFonts);

            var shaped = TextShaper.Shared.ShapeParagraph(request);
            element.ShapedLayout = shaped;
            element.ShapedStartLine = 0;
            element.ShapedLineCount = shaped.Lines.Count;
            return shaped;
        }

        private static void DrawBackground(
            StringBuilder sb,
            TextElement element,
            float textBlockWidth,
            float boxWidth,
            float firstBaseline,
            float lastBaseline,
            IReadOnlyList<ShapedLine> lines,
            float padL,
            float padR,
            float padT,
            float padB,
            float topY,
            float bottomY)
        {
            if (string.IsNullOrWhiteSpace(element.BackgroundColor) &&
                (string.IsNullOrWhiteSpace(element.BackgroundBorderColor) || (element.BackgroundBorderWidth ?? 0f) <= 0f) &&
                string.IsNullOrWhiteSpace(element.BackgroundShadowColor))
            {
                return;
            }

            float xLeft = element.X;
            float boxHeight = Math.Max(0f, topY - bottomY);
            float radius = element.BackgroundCornerRadius ?? 0f;

            if (!string.IsNullOrWhiteSpace(element.BackgroundShadowColor) &&
                ((element.BackgroundShadowOffsetX ?? 0) != 0 || (element.BackgroundShadowOffsetY ?? 0) != 0))
            {
                float shadowX = xLeft + (element.BackgroundShadowOffsetX ?? 0) - padL;
                float shadowY = bottomY + (element.BackgroundShadowOffsetY ?? 0);
                var shadowRgb = TryRgb(element.BackgroundShadowColor) ?? "0 0 0";
                sb.Append($"q {shadowRgb} rg ");
                AppendRoundedRectPath(sb, shadowX, shadowY, boxWidth, boxHeight, radius);
                sb.Append("f Q\n");
            }

            if (!string.IsNullOrWhiteSpace(element.BackgroundColor))
            {
                var fillRgb = TryRgb(element.BackgroundColor) ?? "1 1 1";
                sb.Append($"q {fillRgb} rg ");
                AppendRoundedRectPath(sb, xLeft - padL, bottomY, boxWidth, boxHeight, radius);
                sb.Append("f Q\n");
            }

            if (!string.IsNullOrWhiteSpace(element.BackgroundBorderColor) && (element.BackgroundBorderWidth ?? 0f) > 0f)
            {
                var strokeRgb = TryRgb(element.BackgroundBorderColor) ?? "0 0 0";
                sb.Append($"q {strokeRgb} RG {N(element.BackgroundBorderWidth ?? 1f)} w ");
                AppendRoundedRectPath(sb, xLeft - padL, bottomY, boxWidth, boxHeight, radius);
                sb.Append("S Q\n");
            }
        }

        private static void DrawDecorations(StringBuilder sb, TextElement element, float lineX, float lineWidth, float baseline)
        {
            if (!element.Underline && !element.Strikethrough && !element.Overline)
                return;

            float underlineY = baseline - Math.Max(1f, element.FontSize * 0.08f);
            float strikeY = baseline + element.FontSize * 0.30f;
            float overlineY = baseline + element.FontSize * 0.90f;
            float strokeWidth = Math.Max(0.7f, element.FontSize * 0.05f);

            if (element.Underline)
                DrawLine(sb, lineX, underlineY, lineX + lineWidth, underlineY, element.Color, strokeWidth);
            if (element.Strikethrough)
                DrawLine(sb, lineX, strikeY, lineX + lineWidth, strikeY, element.Color, strokeWidth);
            if (element.Overline)
                DrawLine(sb, lineX, overlineY, lineX + lineWidth, overlineY, element.Color, strokeWidth);
        }

        private static void DrawLine(StringBuilder sb, float x1, float y1, float x2, float y2, string color, float width)
        {
            var rgb = TryRgb(color) ?? "0 0 0";
            sb.Append($"q {rgb} RG {N(width)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S Q\n");
        }

        private static string? TryRgb(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;
            if (color.Equals("black", StringComparison.OrdinalIgnoreCase)) return "0 0 0";
            if (color.StartsWith("#") && color.Length == 7 &&
                int.TryParse(color.Substring(1, 2), NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(color.Substring(3, 2), NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(color.Substring(5, 2), NumberStyles.HexNumber, null, out var b))
            {
                return $"{(r / 255.0).ToString("0.###", Inv)} {(g / 255.0).ToString("0.###", Inv)} {(b / 255.0).ToString("0.###", Inv)}";
            }
            return null;
        }

        private static void AppendRoundedRectPath(StringBuilder sb, float x, float y, float w, float h, float r)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2f));
            if (r == 0)
            {
                sb.Append($"{N(x)} {N(y)} {N(w)} {N(h)} re ");
                return;
            }

            float c = r * 0.5522847498f;
            float x0 = x, x1 = x + r, x2 = x + w - r, x3 = x + w;
            float y0 = y, y1 = y + r, y2 = y + h - r, y3 = y + h;

            sb.Append($"{N(x1)} {N(y0)} m ");
            sb.Append($"{N(x2)} {N(y0)} l ");
            sb.Append($"{N(x2 + c)} {N(y0)} {N(x3)} {N(y1 - c)} {N(x3)} {N(y1)} c ");
            sb.Append($"{N(x3)} {N(y2)} l ");
            sb.Append($"{N(x3)} {N(y2 + c)} {N(x2 + c)} {N(y3)} {N(x2)} {N(y3)} c ");
            sb.Append($"{N(x1)} {N(y3)} l ");
            sb.Append($"{N(x1 - c)} {N(y3)} {N(x0)} {N(y2 + c)} {N(x0)} {N(y2)} c ");
            sb.Append($"{N(x0)} {N(y1)} l ");
            sb.Append($"{N(x0)} {N(y1 - c)} {N(x1 - c)} {N(y0)} {N(x1)} {N(y0)} c ");
            sb.Append("h ");
        }

        public static string PickBaseFont(TextElement element)
        {
            bool bold = element.Bold;
            bool italic = element.Italic;
            if (element.Monospace)
            {
                if (bold && italic) return "Courier-BoldOblique";
                if (bold) return "Courier-Bold";
                if (italic) return "Courier-Oblique";
                return "Courier";
            }

            if (bold && italic) return "Helvetica-BoldOblique";
            if (bold) return "Helvetica-Bold";
            if (italic) return "Helvetica-Oblique";
            return "Helvetica";
        }

        public static HashSet<string> CollectBaseFonts(PdfDocument doc)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var text in doc.Pages.SelectMany(p => p.Elements).OfType<TextElement>())
                set.Add(PickBaseFont(text));
            if (set.Count == 0)
                set.Add("Helvetica");
            return set;
        }
    }
}
