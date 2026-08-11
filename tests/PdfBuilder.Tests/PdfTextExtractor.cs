using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfBuilder.Tests
{
    internal static class PdfTextExtractor
    {
        static PdfTextExtractor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static List<string> ExtractTextBlocks(byte[] pdfBytes)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));

            string pdf = Encoding.ASCII.GetString(pdfBytes);
            var objects = ParseObjects(pdf);
            var fontIdByResource = ParseFontResourceMap(pdf);
            var unicodeMaps = BuildUnicodeMaps(objects, fontIdByResource);

            var streams = PdfContentHelper.ExtractPageContentStreams(pdfBytes);
            var allBlocks = new List<string>();
            foreach (var stream in streams)
            {
                if (stream.IndexOf("BT", StringComparison.Ordinal) >= 0)
                    allBlocks.AddRange(DecodeStreamText(stream, unicodeMaps));
            }

            return allBlocks;
        }

        private static Dictionary<int, string> ParseObjects(string pdf)
        {
            var map = new Dictionary<int, string>();
            var objectRegex = new Regex(@"(?ms)(\d+)\s+0\s+obj\s*(.*?)endobj");
            foreach (Match match in objectRegex.Matches(pdf))
            {
                if (!match.Success || match.Groups.Count < 3) continue;
                int id = int.Parse(match.Groups[1].Value);
                map[id] = match.Groups[2].Value;
            }
            return map;
        }

        private static Dictionary<string, int> ParseFontResourceMap(string pdf)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var fontDictRegex = new Regex(@"/Font\s*<<(?<content>.*?)>>", RegexOptions.Singleline);
            foreach (Match dictMatch in fontDictRegex.Matches(pdf))
            {
                if (!dictMatch.Success) continue;
                string content = dictMatch.Groups["content"].Value;
                var entryRegex = new Regex(@"/(?<name>[A-Za-z0-9]+)\s+(?<id>\d+)\s+0\s+R");
                foreach (Match entry in entryRegex.Matches(content))
                {
                    if (!entry.Success) continue;
                    string resource = "/" + entry.Groups["name"].Value;
                    int id = int.Parse(entry.Groups["id"].Value);
                    if (!map.ContainsKey(resource))
                        map[resource] = id;
                }
            }
            return map;
        }

        private static Dictionary<string, Dictionary<int, string>> BuildUnicodeMaps(
            Dictionary<int, string> objects,
            Dictionary<string, int> fontIdByResource)
        {
            var result = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
            foreach (var kvp in fontIdByResource)
            {
                string resource = kvp.Key;
                int fontObjectId = kvp.Value;
                if (!objects.TryGetValue(fontObjectId, out var fontBody))
                    continue;

                var toUnicodeMatch = Regex.Match(fontBody, @"/ToUnicode\s+(?<id>\d+)\s+0\s+R");
                if (!toUnicodeMatch.Success)
                    continue;

                int unicodeObjectId = int.Parse(toUnicodeMatch.Groups["id"].Value);
                if (!objects.TryGetValue(unicodeObjectId, out var unicodeBody))
                    continue;

                var streamMatch = Regex.Match(unicodeBody, @"stream\s*(?<content>.*?)\s*endstream", RegexOptions.Singleline);
                if (!streamMatch.Success)
                    continue;

                string cmap = streamMatch.Groups["content"].Value;
                var charMap = new Dictionary<int, string>();
                var entryRegex = new Regex(@"<(?<cid>[0-9A-F]{4})>\s+<(?<unicode>[0-9A-F]+)>", RegexOptions.IgnoreCase);
                foreach (Match entry in entryRegex.Matches(cmap))
                {
                    if (!entry.Success) continue;
                    int cid = Convert.ToInt32(entry.Groups["cid"].Value, 16);
                    string unicodeHex = entry.Groups["unicode"].Value;
                    charMap[cid] = DecodeUnicodeHex(unicodeHex);
                }

                result[resource] = charMap;
            }
            return result;
        }

        private static List<string> DecodeStreamText(
            string stream,
            Dictionary<string, Dictionary<int, string>> unicodeMaps)
        {
            var blocks = new List<string>();
            if (string.IsNullOrEmpty(stream))
                return blocks;

            var actualText = new Dictionary<string, string>(StringComparer.Ordinal);
            int actualIndex = 0;
            stream = Regex.Replace(stream,
                @"(?s)/Span\s*<<\s*/ActualText\s*(?<value><[0-9A-Fa-f]+>|\((?:\\.|[^\\\)])*\))\s*>>\s*BDC.*?EMC",
                match =>
                {
                    string token = $"PDFBUILDERACTUAL{actualIndex++}";
                    string value = match.Groups["value"].Value;
                    string replacement = value.StartsWith('<')
                        ? DecodeUnicodeHex(value[1..^1].StartsWith("FEFF", StringComparison.OrdinalIgnoreCase) ? value[5..^1] : value[1..^1])
                        : DecodeLiteralString(value[1..^1]);
                    actualText[token] = RestoreLogicalRtlClusterOrder(replacement);
                    return $"({token}) Tj";
                });

            string currentFont = string.Empty;
            int i = 0;
            while (i < stream.Length)
            {
                if (stream[i] == '/')
                {
                    var fontMatch = Regex.Match(stream.Substring(i),
                        @"^/(?<font>[A-Za-z0-9]+)\s+-?\d+(\.\d+)?\s+Tf");
                    if (fontMatch.Success)
                    {
                        currentFont = "/" + fontMatch.Groups["font"].Value;
                        i += fontMatch.Length;
                        continue;
                    }
                }

                if (stream[i] == '[')
                {
                    int end = stream.IndexOf("] TJ", i, StringComparison.Ordinal);
                    if (end > i)
                    {
                        string arr = stream.Substring(i + 1, end - i - 1);
                        string text = DecodeArray(arr, currentFont, unicodeMaps);
                        if (!string.IsNullOrEmpty(text))
                            blocks.Add(text);
                        i = end + 4; // skip "] TJ"
                        continue;
                    }
                }

                i++;
            }

            var literalRegex = new Regex(@"\((?<text>(?:\\.|[^\\\)])*)\)\s*Tj", RegexOptions.Singleline);
            foreach (Match literal in literalRegex.Matches(stream))
            {
                if (!literal.Success) continue;
                string decoded = DecodeLiteralString(literal.Groups["text"].Value);
                if (actualText.TryGetValue(decoded, out var replacement)) decoded = replacement;
                if (!string.IsNullOrEmpty(decoded))
                    blocks.Add(decoded);
            }

            var hexRegex = new Regex(@"<(?<hex>[0-9A-Fa-f]+)>\s*Tj", RegexOptions.Singleline);
            foreach (Match hexMatch in hexRegex.Matches(stream))
            {
                if (!hexMatch.Success) continue;
                string hex = hexMatch.Groups["hex"].Value;
                var bytes = new byte[hex.Length / 2];
                for (int j = 0; j < bytes.Length; j++)
                    bytes[j] = Convert.ToByte(hex.Substring(j * 2, 2), 16);
                string decoded = Encoding.GetEncoding(1252).GetString(bytes);
                if (!string.IsNullOrEmpty(decoded))
                    blocks.Add(decoded);
            }

            return blocks;
        }

        private static string RestoreLogicalRtlClusterOrder(string value)
        {
            static bool IsRightToLeft(char character) =>
                character is >= '\u0590' and <= '\u08FF'
                    or >= '\uFB1D' and <= '\uFDFF'
                    or >= '\uFE70' and <= '\uFEFF';

            bool hasRightToLeft = value.Any(IsRightToLeft);
            bool hasLeftToRightLetter = value.Any(character => char.IsLetter(character) && !IsRightToLeft(character));
            if (!hasRightToLeft || hasLeftToRightLetter)
            {
                return value;
            }

            var elements = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
                elements.Add(enumerator.GetTextElement());
            elements.Reverse();
            return string.Concat(elements);
        }

        private static string DecodeArray(
            string arr,
            string currentFont,
            Dictionary<string, Dictionary<int, string>> unicodeMaps)
        {
            if (!unicodeMaps.TryGetValue(currentFont, out var map))
            {
                var sbFallback = new StringBuilder();
                var tokenRegexFallback = new Regex(@"<(?<hex>[0-9A-F]+)>", RegexOptions.IgnoreCase);
                foreach (Match token in tokenRegexFallback.Matches(arr))
                {
                    if (!token.Success) continue;
                    string hex = token.Groups["hex"].Value;
                    if (hex.Length % 2 != 0) continue;
                    var bytes = new byte[hex.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    sbFallback.Append(Encoding.GetEncoding(1252).GetString(bytes));
                }
                return sbFallback.ToString();
            }

            var sb = new StringBuilder();
            var tokenRegex = new Regex(@"<(?<hex>[0-9A-F]+)>", RegexOptions.IgnoreCase);
            foreach (Match token in tokenRegex.Matches(arr))
            {
                if (!token.Success) continue;
                string hex = token.Groups["hex"].Value;
                for (int idx = 0; idx + 4 <= hex.Length; idx += 4)
                {
                    string cidHex = hex.Substring(idx, 4);
                    int cid = Convert.ToInt32(cidHex, 16);
                    if (map.TryGetValue(cid, out var text))
                        sb.Append(text);
                }
            }
            return sb.ToString();
        }

        private static string DecodeLiteralString(string content)
        {
            var bytes = new List<byte>();
            for (int i = 0; i < content.Length; i++)
            {
                char ch = content[i];
                if (ch == '\\' && i + 1 < content.Length)
                {
                    char next = content[++i];
                    switch (next)
                    {
                        case 'n': bytes.Add((byte)'\n'); break;
                        case 'r': bytes.Add((byte)'\r'); break;
                        case 't': bytes.Add((byte)'\t'); break;
                        case 'b': bytes.Add((byte)'\b'); break;
                        case 'f': bytes.Add((byte)'\f'); break;
                        case '(':
                        case ')':
                        case '\\':
                            bytes.Add((byte)next);
                            break;
                        default:
                            if (next >= '0' && next <= '7')
                            {
                                string oct = next.ToString();
                                int octDigits = 1;
                                while (octDigits < 3 && i + 1 < content.Length && content[i + 1] >= '0' && content[i + 1] <= '7')
                                {
                                    oct += content[++i];
                                    octDigits++;
                                }
                                bytes.Add(Convert.ToByte(oct, 8));
                            }
                            else
                            {
                                bytes.Add((byte)next);
                            }
                            break;
                    }
                }
                else
                {
                    bytes.Add((byte)ch);
                }
            }

            return Encoding.GetEncoding(1252).GetString(bytes.ToArray());
        }

        private static string DecodeUnicodeHex(string hex)
        {
            if (hex.Length % 2 != 0)
                hex = "0" + hex;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return Encoding.BigEndianUnicode.GetString(bytes);
        }
    }
}
