using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PdfBuilder.Writer;
using SkiaSharp;

namespace PdfBuilder.Writer.Fonts
{
    internal static class FontResourceWriter
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;

        public static Dictionary<string, int> WriteEmbeddedFonts(PdfStreamWriter writer, EmbeddedFontRegistry registry)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var resources = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var font in registry.GetFonts())
            {
                if (font.Glyphs.Count == 0)
                    continue;

                var glyphEntries = font.Glyphs.Values.OrderBy(g => g.Cid).ToList();
                var originalData = font.GetFontData();
                byte[] fontData = originalData;

                var glyphIds = glyphEntries.Select(g => g.GlyphId);
                var unicodePoints = glyphEntries.SelectMany(g => EnumerateCodepoints(g.Unicode));

                try
                {
                    if (FontSubsetter.TrySubset(originalData, glyphIds, unicodePoints, font.BaseFontName, out var subsetData))
                    {
                        if (subsetData.Length > 0 && subsetData.Length + 64 < originalData.Length)
                        {
                            fontData = subsetData;
                        }
                        else if (subsetData.Length > 0)
                        {
                            FontDiagnostics.Report($"Font subset for '{font.BaseFontName}' yielded {subsetData.Length} bytes (original {originalData.Length}); retaining original.");
                        }
                    }
                }
                catch (DllNotFoundException ex)
                {
                    FontDiagnostics.Report($"HarfBuzz native library missing ({ex.Message}); embedding full font '{font.BaseFontName}'.");
                }
                catch (EntryPointNotFoundException ex)
                {
                    FontDiagnostics.Report($"HarfBuzz subset entry point not found ({ex.Message}); embedding full font '{font.BaseFontName}'.");
                }

                byte[] flatedFont = PdfCompression.Flate(fontData);
                bool compressFont = flatedFont.Length > 0 && flatedFont.Length + 12 < fontData.Length;
                int fontFileId = writer.BeginObject();
                if (compressFont)
                {
                    writer.WriteStream(
                        flatedFont,
                        ("/Filter", "/FlateDecode"),
                        ("/Length1", fontData.Length.ToString("0", Inv)));
                }
                else
                {
                    writer.WriteStream(
                        fontData,
                        ("/Length1", fontData.Length.ToString("0", Inv)));
                }
                writer.EndObject();

                using var metricsFont = new SKFont(font.Typeface, 1000f);
                var metrics = metricsFont.Metrics;
                float ascent = Math.Abs(metrics.Ascent);
                float descent = Math.Abs(metrics.Descent);
                float capHeight = metrics.CapHeight != 0 ? metrics.CapHeight : ascent * 0.7f;
                float italicAngle = font.Typeface.FontStyle.Slant switch
                {
                    SKFontStyleSlant.Italic => -12f,
                    SKFontStyleSlant.Oblique => -12f,
                    _ => 0f
                };
                float stemV = EstimateStemV(font.Typeface);
                float xMin = metrics.XMin;
                float xMax = metrics.XMax;
                float yMin = metrics.Bottom;
                float yMax = metrics.Top;

                int descriptorId = writer.BeginObject();
                writer.WriteLine("<< /Type /FontDescriptor");
                writer.WriteLine($" /FontName /{font.BaseFontName}");
                writer.WriteLine(" /Flags 32");
                writer.WriteLine($" /Ascent {ascent.ToString("0", Inv)}");
                writer.WriteLine($" /Descent {(-descent).ToString("0", Inv)}");
                writer.WriteLine($" /CapHeight {capHeight.ToString("0", Inv)}");
                writer.WriteLine($" /ItalicAngle {italicAngle.ToString("0.##", Inv)}");
                writer.WriteLine($" /StemV {stemV.ToString("0", Inv)}");
                writer.WriteLine($" /FontBBox [{Math.Floor(xMin).ToString("0", Inv)} {Math.Floor(yMin).ToString("0", Inv)} {Math.Ceiling(xMax).ToString("0", Inv)} {Math.Ceiling(yMax).ToString("0", Inv)}]");
                writer.WriteLine($" /FontFile2 {fontFileId} 0 R");
                writer.WriteLine(">>");
                writer.EndObject();

                string widthSpec = BuildWidthArray(glyphEntries);

                int cidFontId = writer.BeginObject();
                writer.WriteLine("<< /Type /Font");
                writer.WriteLine(" /Subtype /CIDFontType2");
                writer.WriteLine($" /BaseFont /{font.BaseFontName}");
                writer.WriteLine(" /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>");
                writer.WriteLine($" /FontDescriptor {descriptorId} 0 R");
                writer.WriteLine(" /DW 1000");
                writer.WriteLine($" /W {widthSpec}");
                writer.WriteLine(">>");
                writer.EndObject();

                string cmap = BuildToUnicodeCMap(glyphEntries);
                byte[] cmapBytes = Encoding.ASCII.GetBytes(cmap);
                int toUnicodeId = writer.BeginObject();
                writer.WriteStream(cmapBytes);
                writer.EndObject();

                int type0Id = writer.BeginObject();
                writer.WriteLine("<< /Type /Font");
                writer.WriteLine(" /Subtype /Type0");
                writer.WriteLine($" /BaseFont /{font.BaseFontName}");
                writer.WriteLine(" /Encoding /Identity-H");
                writer.WriteLine($" /DescendantFonts [{cidFontId} 0 R]");
                writer.WriteLine($" /ToUnicode {toUnicodeId} 0 R");
                writer.WriteLine(">>");
                writer.EndObject();

                resources[font.ResourceName] = type0Id;
            }

            return resources;
        }

        private static float EstimateStemV(SKTypeface typeface)
        {
            int weightValue = typeface.FontStyle.Weight;

            if (weightValue <= 100) return 50f;
            if (weightValue <= 200) return 60f;
            if (weightValue <= 300) return 70f;
            if (weightValue <= 400) return 80f;
            if (weightValue <= 500) return 90f;
            if (weightValue <= 600) return 100f;
            if (weightValue <= 700) return 120f;
            if (weightValue <= 800) return 140f;
            if (weightValue <= 900) return 160f;
            return 170f;
        }

        private static string BuildWidthArray(IReadOnlyList<EmbeddedGlyph> glyphs)
        {
            if (glyphs.Count == 0)
                return "[]";

            var sb = new StringBuilder();
            int index = 0;
            while (index < glyphs.Count)
            {
                var startGlyph = glyphs[index];
                int startCid = startGlyph.Cid;
                var widths = new List<string>();

                widths.Add(startGlyph.Width.ToString("0.##", Inv));
                int j = index + 1;
                int expectedCid = startCid + 1;
                while (j < glyphs.Count && glyphs[j].Cid == expectedCid)
                {
                    widths.Add(glyphs[j].Width.ToString("0.##", Inv));
                    expectedCid++;
                    j++;
                }

                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append($"{startCid} [{string.Join(" ", widths)}]");
                index = j;
            }

            return $"[{sb}]";
        }

        private static string BuildToUnicodeCMap(IReadOnlyList<EmbeddedGlyph> glyphs)
        {
            var entries = new List<string>();
            foreach (var glyph in glyphs)
            {
                if (string.IsNullOrEmpty(glyph.Unicode))
                    continue;

                string cidHex = glyph.Cid.ToString("X4");
                var unicodeBytes = Encoding.BigEndianUnicode.GetBytes(glyph.Unicode);
                var unicodeHex = BitConverter.ToString(unicodeBytes).Replace("-", "");
                entries.Add($"<{cidHex}> <{unicodeHex}>");
            }

            var sb = new StringBuilder();
            sb.AppendLine("/CIDInit /ProcSet findresource begin");
            sb.AppendLine("12 dict begin");
            sb.AppendLine("begincmap");
            sb.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> def");
            sb.AppendLine("/CMapName /Adobe-Identity-UCS def");
            sb.AppendLine("/CMapType 2 def");
            sb.AppendLine("1 begincodespacerange");
            sb.AppendLine("<0000> <FFFF>");
            sb.AppendLine("endcodespacerange");

            if (entries.Count > 0)
            {
                int processed = 0;
                while (processed < entries.Count)
                {
                    int batch = Math.Min(100, entries.Count - processed);
                    sb.AppendLine($"{batch} beginbfchar");
                    for (int i = 0; i < batch; i++)
                        sb.AppendLine(entries[processed + i]);
                    sb.AppendLine("endbfchar");
                    processed += batch;
                }
            }
            else
            {
                sb.AppendLine("0 beginbfchar");
                sb.AppendLine("endbfchar");
            }

            sb.AppendLine("endcmap");
            sb.AppendLine("CMapName currentdict /CMap defineresource pop");
            sb.AppendLine("end");
            sb.AppendLine("end");
            return sb.ToString();
        }

        private static IEnumerable<int> EnumerateCodepoints(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            foreach (var rune in text.EnumerateRunes())
                yield return rune.Value;
        }
    }
}
