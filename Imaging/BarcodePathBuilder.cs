using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZXing;
using ZXing.Common;

namespace PdfBuilder.Imaging
{
    internal static class BarcodePathBuilder
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;

        public static BarcodeGeometry Build(
            string value,
            Elements.BarcodeKind kind,
            float moduleSize,
            int quietZone,
            string foregroundColor,
            string? backgroundColor)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Barcode value cannot be null or empty.", nameof(value));

            var writer = new BarcodeWriterGeneric
            {
                Format = ToFormat(kind),
                Options = BuildOptions(kind, quietZone)
            };

            var matrix = writer.Encode(value);
            if (matrix == null)
                throw new InvalidOperationException($"Unable to encode barcode value '{value}'.");

            float width = matrix.Width * moduleSize;
            float height = Math.Max(1, matrix.Height) * moduleSize;

            var pathBuilder = new StringBuilder();
            string fill = ToRgb(foregroundColor) ?? "0 0 0";

            for (int y = 0; y < matrix.Height; y++)
            {
                int runStart = -1;
                for (int x = 0; x <= matrix.Width; x++)
                {
                    bool isSet = x < matrix.Width && matrix[x, y];
                    if (isSet && runStart < 0)
                    {
                        runStart = x;
                    }
                    else if (!isSet && runStart >= 0)
                    {
                        int runLength = x - runStart;
                        float rectX = runStart * moduleSize;
                        float rectY = (matrix.Height - y - 1) * moduleSize;
                        pathBuilder.AppendFormat(
                            Inv,
                            "{0} rg {1:0.###} {2:0.###} {3:0.###} {4:0.###} re f\n",
                            fill,
                            rectX,
                            rectY,
                            runLength * moduleSize,
                            moduleSize);
                        runStart = -1;
                    }
                }
            }

            string? backgroundCommand = null;
            var bg = ToRgb(backgroundColor);
            if (bg != null)
            {
                backgroundCommand = string.Format(
                    Inv,
                    "{0} rg 0 0 {1:0.###} {2:0.###} re f\n",
                    bg,
                    width,
                    height);
            }

            return new BarcodeGeometry(width, height, pathBuilder.ToString(), backgroundCommand);
        }

        private static EncodingOptions BuildOptions(Elements.BarcodeKind kind, int quietZone)
        {
            var opts = new EncodingOptions
            {
                Margin = quietZone,
                PureBarcode = true
            };

            switch (kind)
            {
                case Elements.BarcodeKind.Code128:
                case Elements.BarcodeKind.Code39:
                    opts.Width = 1;
                    opts.Height = 1;
                    opts.Hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
                    break;
                case Elements.BarcodeKind.Ean13:
                    opts.Width = 1;
                    opts.Height = 1;
                    break;
                case Elements.BarcodeKind.QrCode:
                    opts.Width = 1;
                    opts.Height = 1;
                    opts.Hints[EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.M;
                    break;
            }

            return opts;
        }

        private static BarcodeFormat ToFormat(Elements.BarcodeKind kind) =>
            kind switch
            {
                Elements.BarcodeKind.Code128 => BarcodeFormat.CODE_128,
                Elements.BarcodeKind.Code39 => BarcodeFormat.CODE_39,
                Elements.BarcodeKind.Ean13 => BarcodeFormat.EAN_13,
                Elements.BarcodeKind.QrCode => BarcodeFormat.QR_CODE,
                _ => BarcodeFormat.QR_CODE
            };

        private static string? ToRgb(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return null;

            var hex = color.StartsWith("#", StringComparison.Ordinal) ? color[1..] : color;
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return $"{(r / 255.0).ToString("0.###", Inv)} {(g / 255.0).ToString("0.###", Inv)} {(b / 255.0).ToString("0.###", Inv)}";
            }
            return null;
        }
    }

    internal readonly struct BarcodeGeometry
    {
        public BarcodeGeometry(float width, float height, string pathCommands, string? backgroundCommand)
        {
            Width = width;
            Height = height;
            PathCommands = pathCommands ?? throw new ArgumentNullException(nameof(pathCommands));
            BackgroundCommand = backgroundCommand;
        }

        public float Width { get; }
        public float Height { get; }
        public string PathCommands { get; }
        public string? BackgroundCommand { get; }
    }
}
