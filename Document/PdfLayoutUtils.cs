using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PdfBuilder.Document
{
    public static class PdfLayoutUtils
    {
        // Helvetica ASCII width table (1/1000 em). Fallback for non-ASCII ~500.
        private static readonly int[] HelveticaWidths = new int[128]
        {
            // 0-31
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 32-47:  space !"#$%&'()*+,-./
            278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
            // 48-63:  0-9:;<=>?
            556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
            // 64-95:  @A-Z[\]^_
            1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
            667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
            // 96-127: `a-z{|}~ DEL
            278,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,
            611,611,389,556,333,611,556,778,556,556,500,389,280,389,584,0
        };

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

            // Simple monospace shortcut
            if (monospace) return text.Length * fontSize * 0.6f;

            // Approx helvetica metrics for ASCII; non-ASCII ~500
            float units = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int w = (c < 128) ? HelveticaWidths[c] : 500;
                units += w;
            }

            // Make bold a tad wider
            if (bold) units *= 1.05f;

            return (units * fontSize) / 1000f;
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
