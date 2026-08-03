using System;
using System.Globalization;
using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout
{
    public sealed class CanvasBuilder
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private readonly CanvasElement _element;

        internal CanvasBuilder(CanvasElement element)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
        }

        public CanvasBuilder Margin(float all)
        {
            _element.MarginTop = all;
            _element.MarginBottom = all;
            _element.MarginLeft = all;
            _element.MarginRight = all;
            return this;
        }

        public CanvasBuilder Margin(float left, float top, float right, float bottom)
        {
            _element.MarginLeft = left;
            _element.MarginTop = top;
            _element.MarginRight = right;
            _element.MarginBottom = bottom;
            return this;
        }

        public CanvasBuilder AvoidBreakInside(bool value = true)
        {
            _element.AvoidBreakInside = value;
            return this;
        }

        public CanvasBuilder Raw(string command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                if (!command.EndsWith("\n", StringComparison.Ordinal))
                    command += "\n";
                _element.Commands.Add(command);
            }
            return this;
        }

        public CanvasBuilder MoveTo(float x, float y) => Raw($"{N(x)} {N(y)} m");

        public CanvasBuilder LineTo(float x, float y) => Raw($"{N(x)} {N(y)} l");

        public CanvasBuilder ClosePath() => Raw("h");

        public CanvasBuilder Stroke() => Raw("S");

        public CanvasBuilder Fill() => Raw("f");

        public CanvasBuilder StrokeColor(string hex)
        {
            var rgb = TryRgb(hex);
            if (rgb != null)
                Raw($"{rgb} RG");
            return this;
        }

        public CanvasBuilder FillColor(string hex)
        {
            var rgb = TryRgb(hex);
            if (rgb != null)
                Raw($"{rgb} rg");
            return this;
        }

        public CanvasBuilder Line(float x1, float y1, float x2, float y2, float width, string? color = null)
        {
            if (!string.IsNullOrWhiteSpace(color))
            {
                var rgb = TryRgb(color);
                if (rgb != null)
                    Raw($"{rgb} RG");
            }
            Raw($"{N(width)} w");
            MoveTo(x1, y1);
            LineTo(x2, y2);
            Stroke();
            return this;
        }

        public CanvasBuilder Rect(float x, float y, float width, float height, bool stroke = true, bool fill = false)
        {
            Raw($"{N(x)} {N(y)} {N(width)} {N(height)} re");
            if (stroke && fill)
                Raw("B");
            else if (stroke)
                Stroke();
            else if (fill)
                Fill();
            return this;
        }

        private static string N(double value) => value.ToString("0.###", Inv);

        private static string? TryRgb(string? color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;
            var hex = color.StartsWith("#", StringComparison.Ordinal) ? color[1..] : color;
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, null, out var b))
            {
                return $"{(r / 255.0).ToString("0.###", Inv)} {(g / 255.0).ToString("0.###", Inv)} {(b / 255.0).ToString("0.###", Inv)}";
            }
            if (hex.Equals("000000", StringComparison.OrdinalIgnoreCase))
                return "0 0 0";
            if (hex.Equals("ffffff", StringComparison.OrdinalIgnoreCase))
                return "1 1 1";
            return null;
        }
    }
}
