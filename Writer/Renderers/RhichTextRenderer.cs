using PdfBuilder.Document;
using PdfBuilder.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfBuilder.Writer
{
    public static class RichTextRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public sealed class LinkRect
        {
            public float X1, Y1, X2, Y2;        // PDF coords
            public string? Url;
            public string? Anchor;
        }

        /// <summary>
        /// Renders rich paragraph and collects clickable rectangles for link spans.
        /// </summary>
        public static void Append(StringBuilder sb, RichTextElement rt, float pageHeight,
                                  Dictionary<string, int> fontObjId,
                                  List<LinkRect> outLinks)
        {
            // Layout setup
            float fsPara = rt.FontSize > 0 ? rt.FontSize : 12f;
            float lineH = fsPara * (rt.LineHeight > 0 ? rt.LineHeight : 1.2f);
            float maxW = rt.MaxWidth ?? 1_000_000f;

            float padL = rt.PaddingLeft ?? 0, padR = rt.PaddingRight ?? 0;
            float padT = rt.PaddingTop ?? 0, padB = rt.PaddingBottom ?? 0;

            float cursorX = rt.X;
            float baselineY = rt.Y;

            // Simple wrapping per word, carrying run styles
            var tokens = Tokenize(rt.Runs);
            var line = new List<TokenBox>();
            float lineWidth = 0f;

            foreach (var tok in tokens)
            {
                float wTok = Measure(tok, fontObjId);
                if (line.Count > 0 && lineWidth + wTok > maxW)
                {
                    // flush line
                    DrawLine(sb, line, rt.Alignment, maxW, cursorX, baselineY, fontObjId, outLinks);
                    baselineY -= lineH;
                    line.Clear();
                    lineWidth = 0f;
                }
                line.Add(tok);
                lineWidth += wTok;
            }
            if (line.Count > 0) DrawLine(sb, line, rt.Alignment, maxW, cursorX, baselineY, fontObjId, outLinks);
        }

        // ----- internal layout helpers -----

        private sealed class TokenBox
        {
            public string Text = "";
            public string Font = "Helvetica";
            public float Size = 12f;
            public bool Bold, Italic, Underline, Strike, SmallCaps;
            public string Color = "#000";
            public string? Url, Anchor;
            public float Width; // cached width
        }

        private static IEnumerable<TokenBox> Tokenize(List<RichRun> runs)
        {
            foreach (var r in runs)
            {
                var text = r.SmallCaps ? (r.Text ?? "").ToUpperInvariant() : (r.Text ?? "");
                // split keeping spaces as separate tokens to preserve spacing width
                int i = 0;
                while (i < text.Length)
                {
                    int j = i;
                    if (char.IsWhiteSpace(text[i]))
                    {
                        while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                    }
                    else
                    {
                        while (j < text.Length && !char.IsWhiteSpace(text[j])) j++;
                    }
                    string slice = text.Substring(i, j - i);
                    yield return new TokenBox
                    {
                        Text = slice,
                        Font = string.IsNullOrWhiteSpace(r.FontFamily) ? "Helvetica" : r.FontFamily,
                        Size = r.FontSize ?? 12f,
                        Bold = r.Bold,
                        Italic = r.Italic,
                        Underline = r.Underline,
                        Strike = r.Strikethrough,
                        SmallCaps = r.SmallCaps,
                        Color = string.IsNullOrWhiteSpace(r.Color) ? "#000" : r.Color,
                        Url = r.LinkUrl,
                        Anchor = r.LinkAnchor
                    };
                    i = j;
                }
            }
        }

        private static float Measure(TokenBox t, Dictionary<string, int> fontObjId)
        {
            // Use your existing estimator (family + size + bold/italic)
            bool mono = string.Equals(t.Font, "Courier", StringComparison.OrdinalIgnoreCase);
            string fam = mono ? "Courier" : "Helvetica"; // keep to core 14
            float w = PdfLayoutUtils.EstimateTextWidth(t.Text, fam, t.Size, mono, t.Bold);
            t.Width = w;
            return w;
        }

        private static string PickBaseFont(TokenBox t)
        {
            bool mono = string.Equals(t.Font, "Courier", StringComparison.OrdinalIgnoreCase);
            if (mono)
            {
                if (t.Bold && t.Italic) return "Courier-BoldOblique";
                if (t.Bold) return "Courier-Bold";
                if (t.Italic) return "Courier-Oblique";
                return "Courier";
            }
            if (t.Bold && t.Italic) return "Helvetica-BoldOblique";
            if (t.Bold) return "Helvetica-Bold";
            if (t.Italic) return "Helvetica-Oblique";
            return "Helvetica";
        }

        private static void DrawLine(
            StringBuilder sb, List<TokenBox> line, TextAlignment align, float maxWidth,
            float xLeft, float baselineY,
            Dictionary<string, int> fontObjId,
            List<LinkRect> outLinks)
        {
            float lineW = line.Sum(t => t.Width);
            float shiftX = align switch
            {
                TextAlignment.Center => (maxWidth - lineW) / 2f,
                TextAlignment.Right => (maxWidth - lineW),
                _ => 0f
            };

            float cursorX = xLeft + Math.Max(0, shiftX);
            const float ASC = 0.72f, DESC = 0.28f;

            foreach (var t in line)
            {
                string baseFont = PickBaseFont(t);
                if (!fontObjId.TryGetValue(baseFont, out int fontId))
                    fontId = fontObjId.Values.First();

                var col = TryRgb(t.Color) ?? "0 0 0";

                sb.Append("BT ");
                sb.Append($"/F{fontId} {N(t.Size)} Tf {col} {N(cursorX)} {N(baselineY)} Td ");
                sb.Append($"{PdfText(t.Text)} Tj ET\n");

                // Decorations
                if (t.Underline || t.Strike)
                {
                    float u = Math.Max(0.7f, t.Size * 0.05f);
                    if (t.Underline)
                        DrawRule(sb, cursorX, baselineY - t.Size * 0.15f, cursorX + t.Width, baselineY - t.Size * 0.15f, col, u);
                    if (t.Strike)
                        DrawRule(sb, cursorX, baselineY + t.Size * 0.30f, cursorX + t.Width, baselineY + t.Size * 0.30f, col, u);
                }

                // Link rect
                if (!string.IsNullOrEmpty(t.Url) || !string.IsNullOrEmpty(t.Anchor))
                {
                    outLinks.Add(new LinkRect
                    {
                        X1 = cursorX,
                        Y1 = baselineY - t.Size * DESC,
                        X2 = cursorX + t.Width,
                        Y2 = baselineY + t.Size * ASC,
                        Url = t.Url,
                        Anchor = t.Anchor
                    });
                }

                cursorX += t.Width;
            }
        }

        private static void DrawRule(StringBuilder sb, float x1, float y1, float x2, float y2, string rgb, float w)
        {
            sb.Append($"q {rgb} RG {N(w)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S Q\n");
        }

        private static string PdfText(string s) =>
            s.Any(ch => ch > 0x7F) ? Utf16Hex(s) : $"({Escape(s)})";
        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        private static string Utf16Hex(string s)
        {
            var raw = Encoding.BigEndianUnicode.GetBytes(s);
            var withBom = new byte[raw.Length + 2]; withBom[0] = 0xFE; withBom[1] = 0xFF;
            Buffer.BlockCopy(raw, 0, withBom, 2, raw.Length);
            return $"<{BitConverter.ToString(withBom).Replace("-", "")}>";
        }
        private static string? TryRgb(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            hex = hex.Trim(); if (hex.StartsWith("#")) hex = hex[1..];
            if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return $"{(r / 255.0).ToString("0.###", Inv)} {(g / 255.0).ToString("0.###", Inv)} {(b / 255.0).ToString("0.###", Inv)}";
            return null;
        }
    }
}
