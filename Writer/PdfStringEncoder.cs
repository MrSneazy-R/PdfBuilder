using System;
using System.Collections.Generic;
using System.Text;

namespace PdfBuilder.Writer
{
    /// <summary>Encodes human-readable values using PDF literal or UTF-16BE hexadecimal strings.</summary>
    internal static class PdfStringEncoder
    {
        private static readonly IReadOnlyDictionary<char, byte> SpecialPdfDocEncoding =
            new Dictionary<char, byte>
            {
                ['\u2022'] = 0x7F,
                ['\u2020'] = 0x80,
                ['\u2021'] = 0x81,
                ['\u2026'] = 0x82,
                ['\u2014'] = 0x83,
                ['\u2013'] = 0x84,
                ['\u0192'] = 0x85,
                ['\u2044'] = 0x86,
                ['\u2039'] = 0x87,
                ['\u203A'] = 0x88,
                ['\u2212'] = 0x89,
                ['\u2030'] = 0x8A,
                ['\u201E'] = 0x8B,
                ['\u201C'] = 0x8C,
                ['\u201D'] = 0x8D,
                ['\u2018'] = 0x8E,
                ['\u2019'] = 0x8F,
                ['\u201A'] = 0x90,
                ['\u2122'] = 0x91,
                ['\uFB01'] = 0x92,
                ['\uFB02'] = 0x93,
                ['\u0141'] = 0x94,
                ['\u0152'] = 0x95,
                ['\u0160'] = 0x96,
                ['\u0178'] = 0x97,
                ['\u017D'] = 0x98,
                ['\u0131'] = 0x99,
                ['\u0142'] = 0x9A,
                ['\u0153'] = 0x9B,
                ['\u0161'] = 0x9C,
                ['\u017E'] = 0x9D,
                ['\u20AC'] = 0xA0
            };

        public static string Encode(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var bytes = new List<byte>(value.Length);
            foreach (char character in value)
            {
                if (TryEncodePdfDocCharacter(character, out byte encoded))
                {
                    bytes.Add(encoded);
                    continue;
                }

                byte[] unicode = Encoding.BigEndianUnicode.GetBytes(value);
                return $"<FEFF{Convert.ToHexString(unicode)}>";
            }

            var literal = new StringBuilder(bytes.Count + 2).Append('(');
            foreach (byte encoded in bytes)
            {
                switch (encoded)
                {
                    case (byte)'(': literal.Append("\\("); break;
                    case (byte)')': literal.Append("\\)"); break;
                    case (byte)'\\': literal.Append("\\\\"); break;
                    case (byte)'\n': literal.Append("\\n"); break;
                    case (byte)'\r': literal.Append("\\r"); break;
                    case (byte)'\t': literal.Append("\\t"); break;
                    case (byte)'\b': literal.Append("\\b"); break;
                    case (byte)'\f': literal.Append("\\f"); break;
                    default:
                        if (encoded < 0x20)
                            literal.Append('\\').Append(Convert.ToString(encoded, 8).PadLeft(3, '0'));
                        else
                            literal.Append((char)encoded);
                        break;
                }
            }
            return literal.Append(')').ToString();
        }

        private static bool TryEncodePdfDocCharacter(char character, out byte encoded)
        {
            if (character <= 0x7E)
            {
                encoded = (byte)character;
                return true;
            }

            if (character >= 0xA1 && character <= 0xFF && character != 0xAD)
            {
                encoded = (byte)character;
                return true;
            }

            return SpecialPdfDocEncoding.TryGetValue(character, out encoded);
        }
    }
}
