using PdfBuilder.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfBuilder.Writer
{
    public static class HeaderFooterRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public static void Append(StringBuilder sb,
            PdfBuilder.Document.PdfDocument doc, PdfPage page,
            HeaderFooterSpec hf, Dictionary<string, int> fontObjId,
            int pageIndex1, int pageCount, DateTime nowUtc)
        {
            if (hf == null) return;
            bool isFirst = pageIndex1 == 1;
            bool isLast = pageIndex1 == pageCount;

            // Hide-on-last
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
            float topY = page.Height - page.MarginTop + (hf.HeaderHeight > 0 ? (hf.HeaderHeight - 18f) / 2f : 0f);
            float botY = page.MarginBottom - (hf.FooterHeight > 0 ? (hf.FooterHeight - 18f) / 2f : 0f);

            // Draw header text
            if (!string.IsNullOrWhiteSpace(head))
                DrawLine(sb, head, hf, fontObjId, left, right, page.Height - page.MarginTop + (hf.HeaderHeight * 0.5f), hf.HeaderAlign);

            // Draw footer text
            if (!string.IsNullOrWhiteSpace(foot))
                DrawLine(sb, foot, hf, fontObjId, left, right, page.MarginBottom - (hf.FooterHeight * 0.5f), hf.FooterAlign);
        }

        private static void DrawLine(StringBuilder sb, string text, HeaderFooterSpec hf,
            Dictionary<string, int> fontObjId, float left, float right, float centerY, TextAlignment align)
        {
            string fontKey = FontManager.NormalizeFontKey(hf.FontFamily, bold: false, italic: false);
            int fontId = fontObjId.ContainsKey(fontKey)
                ? fontObjId[fontKey]
                : (fontObjId.ContainsKey("Helvetica") ? fontObjId["Helvetica"] : fontObjId.Values.First());

            // Simple color -> rgb fill
            var c = ParseColor(hf.Color);
            string rgb = $"{(c.R / 255.0).ToString("0.###", CultureInfo.InvariantCulture)} {(c.G / 255.0).ToString("0.###", CultureInfo.InvariantCulture)} {(c.B / 255.0).ToString("0.###", CultureInfo.InvariantCulture)} rg";

            float textW = PdfBuilder.Document.PdfLayoutUtils.EstimateTextWidth(text, hf.FontFamily, hf.FontSize);
            float x = align switch
            {
                TextAlignment.Left => left,
                TextAlignment.Center => (left + right - textW) / 2f,
                TextAlignment.Right => right - textW,
                _ => left
            };

            // Baseline (Helvetica ascent ≈ 0.72 * size)
            float baselineY = centerY - (hf.FontSize * 0.3f);

            sb.Append("BT ");
            sb.Append($"/F{fontId} {N(hf.FontSize)} Tf {rgb} {N(x)} {N(baselineY)} Td ");
            sb.Append($"{PdfText(text)} Tj ET\n");
        }

        private static string PdfText(string s) =>
            s.Any(ch => ch > 0x7F) ? Utf16Hex(s) : $"({Escape(s)})";

        private static string Escape(string s) => s.Replace(@"\", @"\\").Replace("(", @"\(").Replace(")", @"\)");

        private static string Utf16Hex(string s)
        {
            var raw = Encoding.BigEndianUnicode.GetBytes(s);
            var withBom = new byte[raw.Length + 2];
            withBom[0] = 0xFE; withBom[1] = 0xFF;
            Buffer.BlockCopy(raw, 0, withBom, 2, raw.Length);
            return $"<{BitConverter.ToString(withBom).Replace("-", "")}>";
        }

        private static Color ParseColor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Color.Black;
            hex = hex.Trim();
            if (hex.StartsWith("#")) hex = hex[1..];
            if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                return Color.FromArgb(r, g, b);
            return Color.Black;
        }
    }
}
