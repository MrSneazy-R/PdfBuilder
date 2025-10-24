using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using System;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace PdfBuilder.Writer
{
    public static class HeaderFooterRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public static void Append(
            StringBuilder sb,
            PdfBuilder.Document.PdfDocument doc,
            PdfPage page,
            HeaderFooterSpec hf,
            PdfRenderContext context,
            int pageIndex1,
            int pageCount,
            DateTime nowUtc)
        {
            if (hf == null) return;
            bool isFirst = pageIndex1 == 1;
            bool isLast = pageIndex1 == pageCount;

            if (hf.HideOnLastPage && isLast) return;

            string headTpl = (isFirst && hf.FirstPageDifferent)
                ? (hf.FirstPageHeaderTemplate ?? string.Empty)
                : (hf.HeaderTemplate ?? string.Empty);

            string footTpl = (isFirst && hf.FirstPageDifferent)
                ? (hf.FirstPageFooterTemplate ?? string.Empty)
                : (hf.FooterTemplate ?? string.Empty);

            var ctx = new PdfBuilder.Document.TokenFormatter.Context(pageIndex1, pageCount, doc.Title, nowUtc);
            string head = PdfBuilder.Document.TokenFormatter.Apply(headTpl, ctx);
            string foot = PdfBuilder.Document.TokenFormatter.Apply(footTpl, ctx);

            float left = page.MarginLeft;
            float right = page.Width - page.MarginRight;

            var direction = doc.TextDefaults.FlowDirection;

            if (!string.IsNullOrWhiteSpace(head))
            {
                float headerCenter = page.Height - page.MarginTop + (hf.HeaderHeight * 0.5f);
                DrawLine(sb, head, hf, context, left, right, headerCenter, hf.HeaderAlign, direction);
            }

            if (!string.IsNullOrWhiteSpace(foot))
            {
                float footerCenter = page.MarginBottom - (hf.FooterHeight * 0.5f);
                DrawLine(sb, foot, hf, context, left, right, footerCenter, hf.FooterAlign, direction);
            }
        }

        private static void DrawLine(
            StringBuilder sb,
            string text,
            HeaderFooterSpec hf,
            PdfRenderContext context,
            float left,
            float right,
            float centerY,
            TextAlignment align,
            FlowDirection flowDirection)
        {
            var request = new TextShapingRequest(
                text ?? string.Empty,
                hf.FontFamily,
                hf.FontSize > 0 ? hf.FontSize : 9f,
                1.1f,
                float.PositiveInfinity,
                bold: false,
                italic: false,
                smallCaps: false,
                monospace: false,
                fallbackFonts: null,
                flowDirection);

            var paragraph = TextShaper.Shared.ShapeParagraph(request);
            if (paragraph == null || paragraph.Lines.Count == 0)
                return;

            string rgb = ColorRgb(hf.Color);
            float blockHeight = paragraph.TotalHeight;
            float top = centerY + (blockHeight / 2f);
            float baseline = top - paragraph.Lines[0].Ascent;

            foreach (var line in paragraph.Lines)
            {
                float lineWidth = line.Width;
                float lineX = align switch
                {
                    TextAlignment.Left => left,
                    TextAlignment.Center => (left + right - lineWidth) / 2f,
                    TextAlignment.Right => right - lineWidth,
                    _ => left
                };

                float cursorX = lineX;
                bool isRtl = flowDirection == FlowDirection.RightToLeft;
                if (isRtl)
                    cursorX = lineX + lineWidth;
                foreach (var run in line.Runs)
                {
                    if (run.Glyphs.Count == 0)
                        continue;

                    var encoded = GlyphRunEncoder.Encode(run, context);
                    sb.Append("BT ");
                    sb.Append($"{encoded.FontResourceName} {N(run.FontSize)} Tf {rgb} rg ");
                    if (isRtl)
                        cursorX -= run.Width;
                    sb.Append($"{N(cursorX)} {N(baseline)} Td ");
                    sb.Append($"{encoded.TjCommand} ET\n");
                    if (!isRtl)
                        cursorX += run.Width;
                }

                baseline -= line.LineHeight;
            }
        }

        private static string ColorRgb(string? hex)
        {
            var color = ParseColor(hex);
            return $"{(color.R / 255.0).ToString("0.###", Inv)} {(color.G / 255.0).ToString("0.###", Inv)} {(color.B / 255.0).ToString("0.###", Inv)}";
        }

        private static Color ParseColor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Color.Black;
            hex = hex.Trim();
            if (hex.StartsWith("#")) hex = hex[1..];
            if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return Color.FromArgb(r, g, b);
            return Color.Black;
        }
    }
}
