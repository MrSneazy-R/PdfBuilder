using System;
using System.Globalization;

namespace PdfBuilder.Writer
{
    /// <summary>Encodes a timestamp and its UTC offset using PDF date syntax.</summary>
    internal static class PdfDateEncoder
    {
        public static string Encode(DateTimeOffset value)
        {
            string prefix = value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            if (value.Offset == TimeSpan.Zero)
                return $"(D:{prefix}Z)";

            char sign = value.Offset < TimeSpan.Zero ? '-' : '+';
            TimeSpan absolute = value.Offset.Duration();
            return $"(D:{prefix}{sign}{absolute.Hours:00}'{absolute.Minutes:00}')";
        }
    }
}
