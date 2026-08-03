using System;
using System.Globalization;
using System.Text;
using PdfBuilder.Elements;

namespace PdfBuilder.Writer
{
    internal static class CanvasRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double value) => value.ToString("0.###", Inv);

        public static void Append(StringBuilder sb, CanvasElement canvas, float pageHeight)
        {
            if (canvas == null) return;
            if (canvas.Width <= 0f || canvas.Height <= 0f) return;

            float originY = Math.Max(0f, pageHeight - (canvas.Y + canvas.Height));
            float originX = canvas.X;

            sb.Append("q ");
            sb.Append($"1 0 0 1 {N(originX)} {N(originY)} cm ");

            foreach (var command in canvas.Commands)
            {
                if (string.IsNullOrWhiteSpace(command)) continue;
                sb.Append(command);
                if (!command.EndsWith("\n", StringComparison.Ordinal))
                    sb.Append('\n');
            }

            sb.Append("Q\n");
        }
    }
}
