using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfBuilder.Encoder
{
    internal static class PdfEnc
    {
        // Characters where Windows-1252 differs from ISO-8859-1
        private static readonly Dictionary<char, byte> W1252Extras = new()
        {
            ['€'] = 0x80,
            ['‚'] = 0x82,
            ['ƒ'] = 0x83,
            ['„'] = 0x84,
            ['…'] = 0x85,
            ['†'] = 0x86,
            ['‡'] = 0x87,
            ['ˆ'] = 0x88,
            ['‰'] = 0x89,
            ['Š'] = 0x8A,
            ['‹'] = 0x8B,
            ['Œ'] = 0x8C,
            ['Ž'] = 0x8E,
            ['‘'] = 0x91,
            ['’'] = 0x92,
            ['“'] = 0x93,
            ['”'] = 0x94,
            ['•'] = 0x95,
            ['–'] = 0x96,
            ['—'] = 0x97,
            ['˜'] = 0x98,
            ['™'] = 0x99,
            ['š'] = 0x9A,
            ['›'] = 0x9B,
            ['œ'] = 0x9C,
            ['ž'] = 0x9E,
            ['Ÿ'] = 0x9F,
        };

        // Remove BOM/zero-widths that cause junk before rotated text
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            Span<char> drop = stackalloc char[] { '\uFEFF', '\u200B', '\u200C', '\u200D', '\u2060' };
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                bool skip = false;
                for (int i = 0; i < drop.Length; i++) if (ch == drop[i]) { skip = true; break; }
                if (!skip) sb.Append(ch);
            }
            return sb.ToString();
        }

        // Encode to single-byte WinAnsi (Windows-1252) without using Encoding.GetEncoding(1252)
        public static byte[] ToWinAnsiBytes(string s)
        {
            s = Sanitize(s) ?? string.Empty;
            var bytes = new byte[s.Length];
            int j = 0;

            foreach (var ch in s)
            {
                if (ch <= 0x7F) bytes[j++] = (byte)ch;                   // ASCII
                else if (ch >= 0xA0 && ch <= 0xFF) bytes[j++] = (byte)ch;                   // Latin-1 block (incl. ° = 0xB0)
                else if (W1252Extras.TryGetValue(ch, out var b)) bytes[j++] = b;                        // smart quotes, en-dash, …
                else bytes[j++] = (byte)'?';                 // fallback
            }
            if (j == bytes.Length) return bytes;
            Array.Resize(ref bytes, j);
            return bytes;
        }

        // Return a PDF hex string for Tj/TJ: e.g., <48656C6C6F>
        public static string WinAnsiHex(string s)
        {
            var data = ToWinAnsiBytes(s);
            if (data.Length == 0) return "<>";
            var sb = new StringBuilder(data.Length * 2 + 2);
            sb.Append('<');
            foreach (var b in data) sb.Append(b.ToString("X2"));
            sb.Append('>');
            return sb.ToString();
        }
    }
}
