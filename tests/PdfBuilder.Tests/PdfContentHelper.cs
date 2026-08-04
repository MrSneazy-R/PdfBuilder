using System;
using System.Collections.Generic;
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

            string ascii = Encoding.ASCII.GetString(pdfBytes);
            int cursor = 0;
            while (true)
            {
                int streamIndex = ascii.IndexOf("stream\n", cursor, StringComparison.Ordinal);
                if (streamIndex < 0) break;
                int endIndex = ascii.IndexOf("\nendstream", streamIndex, StringComparison.Ordinal);
                if (endIndex < 0) break;
                int dataStart = streamIndex + "stream\n".Length;
                streams.Add(ascii.Substring(dataStart, endIndex - dataStart));
                cursor = endIndex + "\nendstream".Length;
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

            string pdf = Encoding.ASCII.GetString(pdfBytes);
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

                    var streamMatch = Regex.Match(contentBody, @"stream\s*(?<content>.*?)\s*endstream", RegexOptions.Singleline);
                    if (streamMatch.Success)
                        result.Add(streamMatch.Groups["content"].Value);
                }

            }

            return result;
        }
    }
}
