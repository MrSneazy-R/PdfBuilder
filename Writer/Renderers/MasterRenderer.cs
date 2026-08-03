using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

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
            PdfRenderContext context,
            bool aboveContent)
        {
            if (wm == null) return;

            // TEXT watermark (image path can be added similarly)
            if (!string.IsNullOrEmpty(wm.Text))
            {
                var request = new TextShapingRequest(
                    wm.Text!,
                    wm.FontFamily,
                    wm.FontSize > 0 ? wm.FontSize : 80f,
                    1.0f,
                    float.PositiveInfinity,
                    bold: false,
                    italic: false,
                    smallCaps: false,
                    monospace: false,
                    fallbackFonts: null);

                var paragraph = TextShaper.Shared.ShapeParagraph(request);
                if (paragraph.Lines.Count > 0)
                {
                    string rgb = ColorRgb(wm.Color);
                    float x = wm.CenterOnPage ? (page.Width / 2f) : wm.X;
                    float y = wm.CenterOnPage ? (page.Height / 2f) : wm.Y;

                    double rad = wm.RotationDegrees * Math.PI / 180.0;
                    double a = Math.Cos(rad), b = Math.Sin(rad), c = -b, d = a;

                    sb.Append("q ");
                    if (!string.IsNullOrEmpty(wm.ExtGStateResourceName))
                        sb.Append($"{wm.ExtGStateResourceName} gs ");
                    sb.Append($"{N(1)} {N(0)} {N(0)} {N(1)} {N(x)} {N(y)} cm ");
                    sb.Append($"{N(a)} {N(b)} {N(c)} {N(d)} 0 0 cm ");

                    float blockHeight = paragraph.TotalHeight;
                    float top = blockHeight / 2f;
                    float baseline = top - paragraph.Lines[0].Ascent;

                    foreach (var line in paragraph.Lines)
                    {
                        float lineWidth = line.Width;
                        float cursorX = -lineWidth / 2f;

                        foreach (var run in line.Runs)
                        {
                            if (run.Glyphs.Count == 0)
                                continue;

                            var encoded = GlyphRunEncoder.Encode(run, context);
                            sb.Append("BT ");
                            sb.Append($"{encoded.FontResourceName} {N(run.FontSize)} Tf {rgb} rg ");
                            sb.Append($"{N(cursorX)} {N(baseline)} Td ");
                            sb.Append($"{encoded.TjCommand} ET\n");
                            cursorX += run.Width;
                        }

                        baseline -= line.LineHeight;
                    }

                    sb.Append("Q\n");
                }
            }
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

        private static string ColorRgb(string? hex)
        {
            var col = ParseColor(hex);
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}",
                col.R / 255.0, col.G / 255.0, col.B / 255.0);
        }
    }
}
