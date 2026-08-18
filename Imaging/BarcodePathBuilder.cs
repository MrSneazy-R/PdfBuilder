using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZXingCpp;

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

            var creator = new BarcodeCreator(ToFormat(kind));
            using var barcode = creator.From(value);
            if (!barcode.IsValid)
            {
                throw new InvalidOperationException(
                    $"Unable to encode barcode value '{value}': {barcode.ErrorMsg}");
            }

            using var writerOptions = new WriterOptions
            {
                Scale = 1,
                AddHRT = false,
                AddQuietZones = false
            };
            using var image = barcode.ToImage(writerOptions);
            var pixels = image.ToArray();
            if (image.Format != ImageFormat.Lum || pixels.Length < image.Width * image.Height)
            {
                throw new InvalidOperationException(
                    $"Barcode encoder returned an unsupported {image.Format} image.");
            }

            int matrixWidth = image.Width + (quietZone * 2);
            int matrixHeight = image.Height + (quietZone * 2);
            float width = matrixWidth * moduleSize;
            float height = Math.Max(1, matrixHeight) * moduleSize;

            var pathBuilder = new StringBuilder();
            string fill = ToRgb(foregroundColor) ?? "0 0 0";

            for (int y = 0; y < image.Height; y++)
            {
                int runStart = -1;
                for (int x = 0; x <= image.Width; x++)
                {
                    bool isSet = x < image.Width && pixels[(y * image.Width) + x] < 128;
                    if (isSet && runStart < 0)
                    {
                        runStart = x;
                    }
                    else if (!isSet && runStart >= 0)
                    {
                        int runLength = x - runStart;
                        float rectX = (runStart + quietZone) * moduleSize;
                        float rectY = (image.Height - y - 1 + quietZone) * moduleSize;
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

        private static BarcodeFormat ToFormat(Elements.BarcodeKind kind) =>
            kind switch
            {
                Elements.BarcodeKind.Code128 => BarcodeFormat.Code128,
                Elements.BarcodeKind.Code39 => BarcodeFormat.Code39,
                Elements.BarcodeKind.Ean13 => BarcodeFormat.EAN13,
                Elements.BarcodeKind.QrCode => BarcodeFormat.QRCode,
                _ => BarcodeFormat.QRCode
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
