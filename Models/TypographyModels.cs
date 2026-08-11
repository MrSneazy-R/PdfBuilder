using System.Text;

namespace PdfBuilder.Models
{
    /// <summary>Controls how paragraph direction is selected.</summary>
    public enum TextDirection
    {
        Automatic,
        LeftToRight,
        RightToLeft
    }

    /// <summary>Controls line wrapping for canonical text.</summary>
    public enum TextWrapping
    {
        Wrap,
        NoWrap,
        Hyphenate
    }

    internal static class TypographyDirectionResolver
    {
        internal static bool ContainsRightToLeft(string? text)
        {
            foreach (var rune in (text ?? string.Empty).EnumerateRunes())
            {
                int value = rune.Value;
                if ((value >= 0x0590 && value <= 0x08FF) || (value >= 0xFB1D && value <= 0xFEFC))
                    return true;
            }
            return false;
        }

        internal static FlowDirection Resolve(TextDirection direction, string? text, FlowDirection fallback = FlowDirection.LeftToRight)
        {
            if (direction == TextDirection.LeftToRight) return FlowDirection.LeftToRight;
            if (direction == TextDirection.RightToLeft) return FlowDirection.RightToLeft;

            foreach (var rune in (text ?? string.Empty).EnumerateRunes())
            {
                int value = rune.Value;
                if ((value >= 0x0590 && value <= 0x08FF) || (value >= 0xFB1D && value <= 0xFEFC))
                    return FlowDirection.RightToLeft;
                if (System.Text.Rune.IsLetter(rune))
                    return FlowDirection.LeftToRight;
            }

            return fallback;
        }
    }
}
