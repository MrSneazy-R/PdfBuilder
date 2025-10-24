using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Document
{
    public static class PdfLayoutUtils
    {
        // ---------- Encoding helpers ----------
        public static string EncodeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // strip BOM / ZW* / control (except \t \n \r)
            ReadOnlySpan<char> banned = stackalloc char[] { '\uFEFF', '\u200B', '\u200C', '\u200D', '\u2060' };
            var sb = new System.Text.StringBuilder(text.Length + 8);
            foreach (var ch in text)
            {
                if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
                bool skip = false; for (int i = 0; i < banned.Length; i++) if (ch == banned[i]) { skip = true; break; }
                if (skip) continue;

                // escape PDF specials
                switch (ch)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '(': sb.Append(@"\("); break;
                    case ')': sb.Append(@"\)"); break;

                    // ASCII-only output: emit a few common non-ASCII via octal so you don't need CP1252
                    case '°': sb.Append(@"\260"); break; // 176 dec = 260 oct

                    default:
                        // keep ASCII; replace other non-ASCII with '?'
                        sb.Append(ch <= 0x7F ? ch : '?');
                        break;
                }
            }
            return sb.ToString();
        }



        private static string StripInvisibleMarkers(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            // Characters that should never go into a PDF literal with Type1/WinAnsi
            // U+FEFF BOM, zero-width space/joiners, word-joiner, etc.
            ReadOnlySpan<char> banned = stackalloc char[] {
            '\uFEFF', // BOM / zero-width no-break space
            '\u200B', // zero-width space
            '\u200C', // zero-width non-joiner
            '\u200D', // zero-width joiner
            '\u2060'  // word joiner
        };

            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s)
            {
                // Skip control chars except tab/newline if you allow them
                if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;

                bool isBanned = false;
                for (int i = 0; i < banned.Length; i++)
                    if (ch == banned[i]) { isBanned = true; break; }

                if (!isBanned) sb.Append(ch);
            }
            return sb.ToString();
        }


        // ---------- Measurement helpers ----------
        public static float EstimateTextWidth(string text, string fontFamily, float fontSize, bool monospace = false, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            var request = new TextShapingRequest(
                text ?? string.Empty,
                string.IsNullOrWhiteSpace(fontFamily) ? "Helvetica" : fontFamily,
                fontSize > 0 ? fontSize : 12f,
                lineHeight: 1f,
                maxWidth: float.PositiveInfinity,
                bold: bold,
                italic: false,
                smallCaps: false,
                monospace: monospace,
                fallbackFonts: null,
                FlowDirection.LeftToRight);

            var shaped = TextShaper.Shared.ShapeParagraph(request);
            return shaped.MaxLineWidth;
        }

        public static float MeasureText(string text, string fontFamily, float fontSize) =>
            EstimateTextWidth(text ?? string.Empty, fontFamily, fontSize);

        public static float MeasureLineHeight(string fontFamily, float fontSize) => fontSize * 1.2f;

        public static float MeasureWrappedHeight(List<string> lines, string fontFamily, float fontSize, float maxWidth) =>
            (lines?.Count ?? 0) * MeasureLineHeight(fontFamily, fontSize);

        // ---------- Wrap helpers ----------
        // Newer overload (font-aware)
        public static List<string> WrapText(string text, string fontFamily, float fontSize, float maxWidth)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Add(string.Empty);
                return result;
            }

            var words = text.Split(' ');
            string current = "";

            foreach (var w in words)
            {
                string test = string.IsNullOrEmpty(current) ? w : current + " " + w;
                if (EstimateTextWidth(test, fontFamily, fontSize) <= maxWidth || string.IsNullOrEmpty(current))
                {
                    current = test;
                }
                else
                {
                    result.Add(current);
                    current = w;
                }
            }

            if (!string.IsNullOrEmpty(current))
                result.Add(current);

            return result;
        }

        // Back-compat overload used by older callers: (text, maxWidth, fontSize)
        public static List<string> WrapText(string text, float maxWidth, float fontSize)
            => WrapText(text, "Helvetica", fontSize, maxWidth);

        // ---------- Alignment helpers ----------
        public static float GetVerticalAlignedY(VerticalAlign align, float topY, float cellHeight, float textHeight, float padding) =>
            align switch
            {
                VerticalAlign.Middle => topY - (cellHeight / 2f) + (textHeight / 2f),
                VerticalAlign.Bottom => topY - cellHeight + padding + textHeight,
                _ => topY - padding
            };

        public static float GetHorizontalAlignedX(HorizontalAlign align, float startX, float cellWidth, List<string> lines, string fontFamily, float fontSize, float padding)
        {
            string first = (lines != null && lines.Count > 0) ? lines[0] : "";
            float textWidth = EstimateTextWidth(first, fontFamily, fontSize);
            return align switch
            {
                HorizontalAlign.Center => startX + (cellWidth - textWidth) / 2f,
                HorizontalAlign.Right => startX + cellWidth - textWidth - padding,
                _ => startX + padding
            };
        }
    }

    public enum VerticalAlign { Top, Middle, Bottom }
    public enum HorizontalAlign { Left, Center, Right }
}













