using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PdfBuilder.Document;
using PdfBuilder.Writer;

namespace PdfBuilder.Tests
{
    internal static class PdfContentHelper
    {
        public static byte[] Generate(PdfDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            return new PdfWriter().GenerateBytes(doc);
        }

        public static List<string> ExtractStreams(byte[] pdfBytes)
        {
            var streams = new List<string>();
            if (pdfBytes == null || pdfBytes.Length == 0) return streams;

            string pdf = Encoding.Latin1.GetString(pdfBytes);
            foreach (Match match in Regex.Matches(pdf, @"(?ms)\d+\s+0\s+obj\s*(.*?)endobj"))
            {
                string? decoded = DecodeStream(match.Groups[1].Value);
                if (decoded != null)
                    streams.Add(decoded);
            }
            return streams;
        }

        public static string ExtractFirstStream(byte[] pdfBytes)
        {
            var contentStreams = ExtractPageContentStreams(pdfBytes);
            if (contentStreams.Count > 0)
                return contentStreams[0];

            var streams = ExtractStreams(pdfBytes);
            foreach (var stream in streams)
            {
                if (stream.IndexOf("BT", StringComparison.Ordinal) >= 0 ||
                    stream.IndexOf("rg", StringComparison.Ordinal) >= 0 ||
                    stream.IndexOf("RG", StringComparison.Ordinal) >= 0)
                {
                    return stream;
                }
            }
            return streams.FirstOrDefault() ?? string.Empty;
        }

        public static HashSet<string> CollectFonts(PdfDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var method = typeof(PdfWriter).GetMethod(
                "CollectBaseFonts",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
                throw new InvalidOperationException("CollectBaseFonts method not found via reflection.");

            return (HashSet<string>)method.Invoke(null, new object[] { doc })!;
        }

        public static List<string> ExtractPageContentStreams(byte[] pdfBytes)
        {
            var result = new List<string>();
            if (pdfBytes == null || pdfBytes.Length == 0)
                return result;

            string pdf = Encoding.Latin1.GetString(pdfBytes);
            var objectMap = new Dictionary<int, string>();
            var objectRegex = new Regex(@"(?ms)(\d+)\s+0\s+obj\s*(.*?)endobj");
            foreach (Match match in objectRegex.Matches(pdf))
            {
                if (!match.Success || match.Groups.Count < 3) continue;
                int id = int.Parse(match.Groups[1].Value);
                objectMap[id] = match.Groups[2].Value;
            }

            foreach (var (id, body) in objectMap)
            {
                if (!body.Contains("/Type /Page", StringComparison.Ordinal))
                    continue;

                var contentsMatch = Regex.Match(body, @"/Contents\s*(\[(?<array>[^\]]+)\]|(?<single>\d+\s+0\s+R))", RegexOptions.Singleline);
                if (!contentsMatch.Success)
                    continue;

                var ids = new List<int>();
                if (contentsMatch.Groups["single"].Success)
                {
                    ids.Add(int.Parse(Regex.Match(contentsMatch.Groups["single"].Value, @"\d+").Value));
                }
                else if (contentsMatch.Groups["array"].Success)
                {
                    var array = contentsMatch.Groups["array"].Value;
                    foreach (Match entry in Regex.Matches(array, @"(\d+)\s+0\s+R"))
                    {
                        ids.Add(int.Parse(entry.Groups[1].Value));
                    }
                }

                foreach (int contentId in ids)
                {
                    if (!objectMap.TryGetValue(contentId, out var contentBody))
                        continue;

                    string? decoded = DecodeStream(contentBody);
                    if (decoded != null)
                        result.Add(decoded);
                }

                if (result.Count > 0)
                    break;
            }

            return result;
        }

        private static string? DecodeStream(string objectBody)
        {
            int marker = objectBody.IndexOf("stream\n", StringComparison.Ordinal);
            int markerLength = "stream\n".Length;
            if (marker < 0)
            {
                marker = objectBody.IndexOf("stream\r\n", StringComparison.Ordinal);
                markerLength = "stream\r\n".Length;
            }
            if (marker < 0)
                return null;

            var lengthMatch = Regex.Match(objectBody[..marker], @"/Length\s+(?<length>\d+)");
            if (!lengthMatch.Success || !int.TryParse(lengthMatch.Groups["length"].Value, out int length))
                return null;

            int dataStart = marker + markerLength;
            if (length < 0 || dataStart + length > objectBody.Length)
                return null;

            byte[] data = Encoding.Latin1.GetBytes(objectBody.Substring(dataStart, length));
            if (objectBody[..marker].Contains("/Filter /FlateDecode", StringComparison.Ordinal))
            {
                using var input = new MemoryStream(data);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                data = output.ToArray();
            }

            return Encoding.Latin1.GetString(data);
        }
    }
}
