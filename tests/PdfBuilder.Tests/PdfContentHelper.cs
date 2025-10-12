using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
            => ExtractStreams(pdfBytes).FirstOrDefault() ?? string.Empty;

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
    }
}
