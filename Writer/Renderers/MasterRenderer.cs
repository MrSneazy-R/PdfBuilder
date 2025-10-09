using PdfBuilder.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfBuilder.Writer
{
    public static class MasterRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public static void AppendBackground(StringBuilder sb, PdfPage page, MasterPageSpec master)
        {
            if (master == null) return;

            if (!string.IsNullOrWhiteSpace(master.BackgroundColor))
            {
                var col = ParseColor(master.BackgroundColor);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} rg ",
                    col.R / 255.0, col.G / 255.0, col.B / 255.0);
                sb.Append($"{N(0)} {N(0)} {N(page.Width)} {N(page.Height)} re f\n");
            }

            // (Optional) background image: hook up your Image XObject draw here if needed.
        }

        public static void AppendWatermark(
            StringBuilder sb,
            PdfPage page,
            WatermarkSpec wm,
            Dictionary<string, int> fontObjId,
            bool aboveContent)
        {
            if (wm == null) return;

            // TEXT watermark (image path can be added similarly)
            if (!string.IsNullOrEmpty(wm.Text))
            {
                var col = ParseColor(wm.Color);
                string rgb = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} rg",
                    col.R / 255.0, col.G / 255.0, col.B / 255.0);

                string fontKey = FontManager.NormalizeFontKey(wm.FontFamily);
                int fontId = fontObjId.TryGetValue(fontKey, out var id)
                    ? id
                    : (fontObjId.ContainsKey("Helvetica") ? fontObjId["Helvetica"] : fontObjId.Values.First());

                float x = wm.CenterOnPage ? (page.Width / 2f) : wm.X;
                float y = wm.CenterOnPage ? (page.Height / 2f) : wm.Y;

                double rad = wm.RotationDegrees * Math.PI / 180.0;
                double a = Math.Cos(rad), b = Math.Sin(rad), c = -b, d = a;

                sb.Append("q ");
                sb.Append($"{N(1)} {N(0)} {N(0)} {N(1)} {N(x)} {N(y)} cm ");   // translate
                sb.Append($"{N(a)} {N(b)} {N(c)} {N(d)} 0 0 cm ");              // rotate
                sb.Append("BT ");
                sb.Append($"/F{fontId} {N(wm.FontSize)} Tf {rgb} ");

                var textW = PdfBuilder.Document.PdfLayoutUtils.EstimateTextWidth(wm.Text, wm.FontFamily, wm.FontSize);
                sb.Append($"{N(-textW / 2f)} {N(-wm.FontSize / 3f)} Td ");
                sb.Append($"{PdfText(wm.Text)} Tj ET\n");
                sb.Append("Q\n");
            }
        }

        private static string PdfText(string s) =>
            s.Any(ch => ch > 0x7F) ? Utf16Hex(s) : $"({Escape(s)})";

        private static string Escape(string s) =>
            s.Replace(@"\", @"\\").Replace("(", @"\(").Replace(")", @"\)");

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
                int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return Color.FromArgb(r, g, b);
            return Color.Black;
        }
    }
}
