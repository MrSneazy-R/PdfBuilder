using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Writer
{
    public static class ListRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public static void Append(StringBuilder sb, ListElement list, float pageHeight,
                                  PdfRenderContext context,
                                  List<RichTextRenderer.LinkRect> outLinks)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (outLinks == null) throw new ArgumentNullException(nameof(outLinks));

            float y = list.Y;
            float xStart = list.X;
            float maxW = list.MaxWidth ?? 1_000_000f;
            string col = TryRgb(list.Color) ?? "0 0 0";

            RenderItems(list.Items, 0);

            void RenderItems(List<ListItem> items, int level)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    string marker = MarkerText(list.Marker, i, level);
                    float xLeft = xStart + level * list.IndentPerLevel;

                    float markerWidth = 0f;
                    var markerRun = ShapeMarker(marker, list);
                    if (markerRun != null && markerRun.Glyphs.Count > 0)
                    {
                        var encoded = GlyphRunEncoder.Encode(markerRun, context);
                        sb.Append("BT ");
                        sb.Append($"{encoded.FontResourceName} {N(markerRun.FontSize)} Tf {col} rg ");
                        sb.Append($"{N(xLeft)} {N(y)} Td ");
                        sb.Append($"{encoded.TjCommand} ET\n");
                        markerWidth = markerRun.Width;
                    }

                    float textX = xLeft + markerWidth + list.BulletGap;
                    float available = Math.Max(0f, maxW - (textX - xStart));
                    var rt = new RichTextElement(textX, y)
                    {
                        FontFamily = list.FontFamily,
                        FontSize = list.FontSize,
                        LineHeight = list.LineHeight,
                        Alignment = TextAlignment.Left,
                        MaxWidth = available,
                        FlowDirection = list.FlowDirection
                    };
                    rt.Runs.AddRange(item.Content);
                    float consumed = RichTextRenderer.Append(sb, rt, pageHeight, context, outLinks);

                    y -= consumed + list.ItemSpacing;

                    if (item.Children.Count > 0)
                        RenderItems(item.Children, level + 1);
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

        private static ShapedRun? ShapeMarker(string marker, ListElement list)
        {
            if (string.IsNullOrEmpty(marker))
                return null;

            var request = new TextShapingRequest(
                marker,
                list.FontFamily,
                list.FontSize,
                list.LineHeight > 0 ? list.LineHeight : 1.2f,
                float.PositiveInfinity,
                bold: false,
                italic: false,
                smallCaps: false,
                monospace: false,
                fallbackFonts: null,
                list.FlowDirection);

            var paragraph = TextShaper.Shared.ShapeParagraph(request);
            var line = paragraph.Lines.FirstOrDefault();
            return line?.Runs.FirstOrDefault();
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
