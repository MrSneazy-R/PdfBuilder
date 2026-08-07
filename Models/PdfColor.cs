using System.Globalization;

namespace PdfBuilder.Models;

/// <summary>Immutable sRGB colour value independent of System.Drawing.</summary>
public readonly record struct PdfColor(byte Red, byte Green, byte Blue, byte Alpha = 255)
{
    /// <summary>Creates an opaque colour from red, green, and blue byte values.</summary>
    public static PdfColor Rgb(byte red, byte green, byte blue) => new(red, green, blue);

    /// <summary>Parses <c>#RRGGBB</c> or <c>#AARRGGBB</c> hexadecimal colour text.</summary>
    public static PdfColor Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A hexadecimal colour value is required.", nameof(value));

        var hex = value.Trim().TrimStart('#');
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
            throw new FormatException("Expected a hexadecimal colour in #RRGGBB or #AARRGGBB format.");

        return hex.Length == 6
            ? new PdfColor((byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed)
            : new PdfColor((byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed, (byte)(parsed >> 24));
    }

    /// <summary>Returns an uppercase invariant hexadecimal colour string.</summary>
    public override string ToString() => Alpha == 255
        ? $"#{Red:X2}{Green:X2}{Blue:X2}"
        : $"#{Alpha:X2}{Red:X2}{Green:X2}{Blue:X2}";
}
