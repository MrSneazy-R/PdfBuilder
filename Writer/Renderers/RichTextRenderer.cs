using PdfBuilder.Document;
using PdfBuilder.Document.TextShaping;
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
    public static class RichTextRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public sealed class LinkRect
        {
            public float X1, Y1, X2, Y2;
            public string? Url;
            public string? Anchor;
        }

        public static float Append(StringBuilder sb, RichTextElement element, float pageHeight, PdfRenderContext context, List<LinkRect> outLinks)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (outLinks == null) throw new ArgumentNullException(nameof(outLinks));

            var layout = EnsureLayout(element);
            float padTop = element.PaddingTop ?? 0f;
            float padBottom = element.PaddingBottom ?? 0f;
            float totalHeight = layout.TotalHeight + padTop + padBottom;

            float effectiveWidth = element.MaxWidth ?? layout.MaxLineWidth;
            float baseline = element.Y;

            foreach (var line in layout.Lines)
            {
                float cursorX = element.X;
                if (element.Alignment == TextAlignment.Center)
                    cursorX += (effectiveWidth - line.Width) / 2f;
                else if (element.Alignment == TextAlignment.Right)
                    cursorX += effectiveWidth - line.Width;

                foreach (var segment in line.Segments)
                {
                    if (segment.ShapedRun.Glyphs.Count == 0)
                        continue;

                    var encoded = GlyphRunEncoder.Encode(segment.ShapedRun, context);

                    sb.Append("BT ");
                    sb.Append($"{encoded.FontResourceName} {N(segment.ShapedRun.FontSize)} Tf {ColorRgb(segment.Color)} rg ");
                    sb.Append($"{N(cursorX)} {N(baseline)} Td ");
                    sb.Append($"{encoded.TjCommand} ET\n");

                    float runWidth = segment.ShapedRun.Width;
                    if (segment.Underline || segment.Strikethrough)
                    {
                        float underlineY = baseline - segment.ShapedRun.FontSize * 0.15f;
                        float strikeY = baseline + segment.ShapedRun.FontSize * 0.30f;
                        float strokeWidth = Math.Max(0.7f, segment.ShapedRun.FontSize * 0.05f);
                        if (segment.Underline)
                            DrawLine(sb, cursorX, underlineY, cursorX + runWidth, underlineY, segment.Color, strokeWidth);
                        if (segment.Strikethrough)
                            DrawLine(sb, cursorX, strikeY, cursorX + runWidth, strikeY, segment.Color, strokeWidth);
                    }

                    if (segment.HasLink)
                    {
                        float top = baseline + segment.ShapedRun.Ascent;
                        float bottom = baseline - segment.ShapedRun.Descent;
                        outLinks.Add(new LinkRect
                        {
                            X1 = cursorX,
                            Y1 = bottom,
                            X2 = cursorX + runWidth,
                            Y2 = top,
                            Url = segment.Url,
                            Anchor = segment.Anchor
                        });
                    }

                    cursorX += runWidth;
                }

                baseline -= line.LineHeight;
            }

            return totalHeight;
        }

        private static RichTextLayoutResult EnsureLayout(RichTextElement element)
        {
            float innerWidth = float.PositiveInfinity;
            float padLeft = element.PaddingLeft ?? 0f;
            float padRight = element.PaddingRight ?? 0f;
            if (element.MaxWidth.HasValue)
                innerWidth = Math.Max(0f, element.MaxWidth.Value - padLeft - padRight);

            if (element.ShapedLayout == null || Math.Abs(element.ShapedLayoutWidth - innerWidth) > 0.1f)
            {
                var layout = RichTextLayouter.Layout(element, innerWidth);
                element.ShapedLayout = layout;
                element.ShapedLayoutWidth = innerWidth;
                element.ShapedStartLine = 0;
                element.ShapedLineCount = layout.Lines.Count;
            }

            return element.ShapedLayout!;
        }

        private static void DrawLine(StringBuilder sb, float x1, float y1, float x2, float y2, string color, float width)
        {
            var rgb = ColorRgb(color);
            sb.Append($"q {rgb} RG {N(width)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S Q\n");
        }

        private static string ColorRgb(string color)
        {
            var parsed = TryRgb(color);
            return parsed ?? "0 0 0";
        }

        private static string? TryRgb(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;
            if (color.Equals("black", StringComparison.OrdinalIgnoreCase)) return "0 0 0";
            if (color.StartsWith("#") && color.Length == 7 &&
                int.TryParse(color.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(color.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(color.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return $"{(r / 255.0).ToString("0.###", Inv)} {(g / 255.0).ToString("0.###", Inv)} {(b / 255.0).ToString("0.###", Inv)}";
            }
            return null;
        }
    }
}
