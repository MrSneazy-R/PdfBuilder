using PdfBuilder.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfBuilder.Writer
{
    public static class ListRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public static void Append(StringBuilder sb, ListElement list, float pageHeight,
                                  Dictionary<string, int> fontObjId,
                                  List<RichTextRenderer.LinkRect> outLinks)
        {
            string baseFont = "Helvetica";
            if (!fontObjId.ContainsKey(baseFont)) baseFont = fontObjId.Keys.First();
            int markerFontId = fontObjId[baseFont];

            float fs = list.FontSize;
            float y = list.Y;
            float xStart = list.X;
            float maxW = list.MaxWidth ?? 1_000_000f;
            string col = TryRgb(list.Color) ?? "0 0 0";

            RenderItems(sb, list.Items, 0);

            void RenderItems(StringBuilder sb2, List<ListItem> items, int level)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    // Marker text
                    string marker = MarkerText(list.Marker, i, level);
                    float xLeft = xStart + level * list.IndentPerLevel;

                    // Draw marker
                    sb2.Append("BT ");
                    sb2.Append($"/F{markerFontId} {N(fs)} Tf {col} rg {N(xLeft)} {N(y)} Td {PdfText(marker)} Tj ET\n");

                    // Rich content to the right with hanging indent
                    float textX = xLeft + EstimateText(marker, fs) + list.BulletGap;
                    var rt = new RichTextElement(textX, y)
                    {
                        FontFamily = list.FontFamily,
                        FontSize = list.FontSize,
                        LineHeight = list.LineHeight,
                        Alignment = TextAlignment.Left,
                        MaxWidth = maxW - (textX - xStart)
                    };
                    rt.Runs.AddRange(it.Content);
                    float consumed = RichTextRenderer.Append(sb2, rt, pageHeight, fontObjId, outLinks);

                    y -= consumed + list.ItemSpacing;

                    if (it.Children.Count > 0)
                        RenderItems(sb2, it.Children, level + 1);
                }
            }
        }

        private static string MarkerText(ListMarker m, int index, int level)
        {
            return m switch
            {
                ListMarker.Bullet => "\u2022",
                ListMarker.Decimal => (index + 1).ToString() + ".",
                ListMarker.LowerAlpha => $"{(char)('a' + (index % 26))}.",
                ListMarker.UpperAlpha => $"{(char)('A' + (index % 26))}.",
                ListMarker.LowerRoman => ToRoman(index + 1).ToLowerInvariant() + ".",
                ListMarker.UpperRoman => ToRoman(index + 1).ToUpperInvariant() + ".",
                _ => "\u2022"
            };
        }

        private static string ToRoman(int number)
        {
            var map = new (int, string)[] {
                (1000,"M"),(900,"CM"),(500,"D"),(400,"CD"),
                (100,"C"),(90,"XC"),(50,"L"),(40,"XL"),
                (10,"X"),(9,"IX"),(5,"V"),(4,"IV"),(1,"I")
            };
            var sb = new StringBuilder();
            foreach (var (v, s) in map) { while (number >= v) { sb.Append(s); number -= v; } }
            return sb.ToString();
        }

        private static float EstimateText(string s, float fs) =>
            PdfBuilder.Document.PdfLayoutUtils.EstimateTextWidth(s, "Helvetica", fs);

        private static readonly Encoding WinAnsi = GetWinAnsi();
        private static Encoding GetWinAnsi()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        }

        private static string PdfText(string s)
        {
            var bytes = WinAnsi.GetBytes(s ?? string.Empty);
            if (bytes.Length == 0) return "()";
            bool asciiSafe = true;
            foreach (var b in bytes)
            {
                if (b < 0x20 || b > 0x7E || b == (byte)'(' || b == (byte)')' || b == (byte)'\\')
                {
                    asciiSafe = false;
                    break;
                }
            }
            if (asciiSafe)
            {
                var literal = Encoding.ASCII.GetString(bytes);
                literal = literal.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
                return $"({literal})";
            }
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) hex.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            return $"<{hex}>";
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
