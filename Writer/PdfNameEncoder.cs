using System;
using System.Text;

namespace PdfBuilder.Writer
{
    /// <summary>Encodes caller-controlled text as a PDF name object.</summary>
    internal static class PdfNameEncoder
    {
        public static string Encode(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var bytes = Encoding.UTF8.GetBytes(value.Length > 0 && value[0] == '/' ? value[1..] : value);
            var result = new StringBuilder(bytes.Length + 1).Append('/');
            foreach (byte valueByte in bytes)
            {
                if (valueByte is >= 0x21 and <= 0x7E &&
                    valueByte is not (byte)'#' and not (byte)'%' and not (byte)'(' and not (byte)')' and
                    not (byte)'<' and not (byte)'>' and not (byte)'[' and not (byte)']' and
                    not (byte)'{' and not (byte)'}' and not (byte)'/')
                {
                    result.Append((char)valueByte);
                }
                else
                {
                    result.Append('#').Append(valueByte.ToString("X2"));
                }
            }
            return result.ToString();
        }
    }
}
