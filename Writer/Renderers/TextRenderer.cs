using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfBuilder.Writer
{
    /// <summary>
    /// Renders TextElement blocks into PDF content stream syntax.
    /// Matches features we added: bold/italic/mono, small-caps, underline,
    /// strikethrough, overline, rotation, alignment, and the background box
    /// (padding, border, shadow, rounded corners).
    /// </summary>
    public static class TextRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        /// <param name="pageHeight">Unused (we’re already in PDF coords), kept for symmetry.</param>
        public static void Append(
           StringBuilder sb, TextElement t, float pageHeight,
           Dictionary<string, int> fontObjId)
        {
            // Choose measurement family (Courier when monospace)
            string measureFamily =
                t.Monospace ? "Courier" :
                string.IsNullOrWhiteSpace(t.FontFamily) ? "Helvetica" : t.FontFamily;

            // Pick base-14 font name for actual painting (handles bold/italic + monospace)
            string baseFont = PickBaseFont(t);
            int fontId = fontObjId[baseFont];

            float fs = t.FontSize > 0 ? t.FontSize : 12f;
            float maxWidth = t.MaxWidth ?? 10000f;
            float leading = fs * (t.LineHeight > 0 ? t.LineHeight : 1.2f);

            // Wrap and optionally small-caps (simple uppercase transform)
            var rawLines = PdfLayoutUtils.WrapText(t.Text ?? "", measureFamily, fs, maxWidth);
            var lines = t.SmallCaps
                ? rawLines.Select(s => s?.ToUpperInvariant() ?? "").ToList()
                : rawLines;

            // Pre-measure for background box, underline, etc.
            float ascent = fs * 0.8f;
            float descent = fs * 0.2f;
            float padL = t.PaddingLeft ?? 0, padR = t.PaddingRight ?? 0;
            float padT = t.PaddingTop ?? 0, padB = t.PaddingBottom ?? 0;

            float maxLineW = lines.Count > 0
                ? lines.Max(s => PdfLayoutUtils.EstimateTextWidth(s, measureFamily, fs))
                : 0f;

            // Box width: if MaxWidth is set, use it (more predictable centering); else max line width.
            float textBlockW = t.MaxWidth ?? maxLineW;
            float boxW = textBlockW + padL + padR;

            // From baseline of first line (t.Y):
            float firstBaseline = t.Y;
            float lastBaseline = t.Y - (lines.Count - 1) * leading;

            float yTop = firstBaseline + ascent + padT;
            float yBottom = lastBaseline - descent - padB;
            float boxH = Math.Max(0, yTop - yBottom);

            // Box left X (respect align if MaxWidth known)
            float xLeft = t.X;
            if (t.MaxWidth.HasValue)
            {
                if (t.Alignment == TextAlignment.Center)
                    xLeft = t.X;
                else if (t.Alignment == TextAlignment.Right)
                    xLeft = t.X;
                // For right/center we already position text by shifting inside MaxWidth;
                // the box anchors at t.X with width = MaxWidth (+padding).
            }
            else
            {
                // No MaxWidth: anchor box to text start for left-aligned,
                // or to the min lineX among lines for other aligns.
                if (t.Alignment != TextAlignment.Left)
                {
                    float minX = float.MaxValue;
                    foreach (var s in lines)
                    {
                        float lineX = t.X;
                        float lw = PdfLayoutUtils.EstimateTextWidth(s, measureFamily, fs);
                        if (t.Alignment == TextAlignment.Center) lineX += (textBlockW - lw) / 2f;
                        else if (t.Alignment == TextAlignment.Right) lineX += textBlockW - lw;
                        minX = Math.Min(minX, lineX);
                    }
                    xLeft = minX == float.MaxValue ? t.X : minX;
                }
            }

            // ---------- Background (shadow -> fill -> border) ----------
            if (!string.IsNullOrWhiteSpace(t.BackgroundColor) ||
                !string.IsNullOrWhiteSpace(t.BackgroundBorderColor) && (t.BackgroundBorderWidth ?? 0) > 0 ||
                !string.IsNullOrWhiteSpace(t.BackgroundShadowColor))
            {
                float r = t.BackgroundCornerRadius ?? 0;

                // Shadow (simple offset, no blur)
                if (!string.IsNullOrWhiteSpace(t.BackgroundShadowColor) &&
                    ((t.BackgroundShadowOffsetX ?? 0) != 0 || (t.BackgroundShadowOffsetY ?? 0) != 0))
                {
                    float sx = xLeft + (t.BackgroundShadowOffsetX ?? 0) - padL;
                    float sy = yBottom + (t.BackgroundShadowOffsetY ?? 0);
                    var sRGB = TryRgb(t.BackgroundShadowColor) ?? "0 0 0";
                    sb.Append($"q {sRGB} rg ");
                    AppendRoundedRectPath(sb, sx, sy, boxW, boxH, r);
                    sb.Append("f Q\n");
                }

                // Fill
                if (!string.IsNullOrWhiteSpace(t.BackgroundColor))
                {
                    var fillRGB = TryRgb(t.BackgroundColor) ?? "1 1 1";
                    sb.Append($"q {fillRGB} rg ");
                    AppendRoundedRectPath(sb, xLeft - padL, yBottom, boxW, boxH, r);
                    sb.Append("f Q\n");
                }

                // Border
                if (!string.IsNullOrWhiteSpace(t.BackgroundBorderColor) &&
                    (t.BackgroundBorderWidth ?? 0) > 0)
                {
                    var strokeRGB = TryRgb(t.BackgroundBorderColor) ?? "0 0 0";
                    sb.Append($"q {strokeRGB} RG {N(t.BackgroundBorderWidth ?? 1)} w ");
                    AppendRoundedRectPath(sb, xLeft - padL, yBottom, boxW, boxH, r);
                    sb.Append("S Q\n");
                }
            }

            // ---------- Text lines + decorations ----------
            float baselineY = firstBaseline;

            foreach (var line in lines)
            {
                float lineX = t.X;

                if (t.MaxWidth.HasValue)
                {
                    float lw = PdfLayoutUtils.EstimateTextWidth(line, measureFamily, fs);
                    if (t.Alignment == TextAlignment.Center) lineX += (textBlockW - lw) / 2f;
                    else if (t.Alignment == TextAlignment.Right) lineX += textBlockW - lw;
                }

                sb.Append("BT ");
                sb.Append($"/F{fontId} {N(fs)} Tf ");

                var rgb = TryRgb(t.Color);
                if (rgb != null) sb.Append($"{rgb} rg ");

                if (Math.Abs(t.Rotation) < 0.0001)
                    sb.Append($"{N(lineX)} {N(baselineY)} Td ");
                else
                {
                    double rad = t.Rotation * Math.PI / 180.0;
                    double cos = Math.Cos(rad);
                    double sin = Math.Sin(rad);
                    sb.Append($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} {N(lineX)} {N(baselineY)} Tm ");
                }

                sb.Append($"({Escape(line)}) Tj ET\n");

                // Decorations
                float textWidth = PdfLayoutUtils.EstimateTextWidth(line, measureFamily, fs);
                float underlineY = baselineY - Math.Max(1f, fs * 0.08f);
                float strikeY = baselineY + fs * 0.30f;
                float overlineY = baselineY + fs * 0.90f;

                if (t.Underline)
                    DrawLine(sb, lineX, underlineY, lineX + textWidth, underlineY, t.Color, Math.Max(0.7f, fs * 0.05f));
                if (t.Strikethrough)
                    DrawLine(sb, lineX, strikeY, lineX + textWidth, strikeY, t.Color, Math.Max(0.7f, fs * 0.05f));
                if (t.Overline)
                    DrawLine(sb, lineX, overlineY, lineX + textWidth, overlineY, t.Color, Math.Max(0.7f, fs * 0.05f));

                baselineY -= leading;
            }
        }

        // ----- helpers (kept local to avoid coupling) -----

        public static string PickBaseFont(TextElement t)
        {
            bool b = t.Bold;
            bool i = t.Italic;

            // Monospace uses Courier family
            if (t.Monospace)
            {
                if (b && i) return "Courier-BoldOblique";
                if (b) return "Courier-Bold";
                if (i) return "Courier-Oblique";
                return "Courier";
            }

            // Default Helvetica family
            if (b && i) return "Helvetica-BoldOblique";
            if (b) return "Helvetica-Bold";
            if (i) return "Helvetica-Oblique";
            return "Helvetica";
        }
        public static HashSet<string> CollectBaseFonts(PdfDocument doc)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in doc.Pages.SelectMany(p => p.Elements).OfType<TextElement>())
                set.Add(PickBaseFont(t));

            if (set.Count == 0) set.Add("Helvetica");
            return set;
        }
        private static void DrawLine(StringBuilder sb, float x1, float y1, float x2, float y2, string color, float width)
        {
            var rgb = TryRgb(color) ?? "0 0 0";
            sb.Append($"q {rgb} RG {N(width)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S Q\n");
        }

        private static string? TryRgb(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;
            if (color.Equals("black", StringComparison.OrdinalIgnoreCase)) return null;
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

            // kappa for circle/quarter via cubic Béziers
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

        private static string Escape(string s) =>
            (s ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
