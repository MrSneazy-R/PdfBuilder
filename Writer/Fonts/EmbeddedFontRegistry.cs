using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace PdfBuilder.Writer.Fonts
{
    internal sealed class EmbeddedFontRegistry
    {
        private readonly Dictionary<long, EmbeddedFont> _fonts = new();
        private int _fontSequence = 1;

        public GlyphRegistration RegisterGlyph(SKTypeface typeface, uint glyphId, string unicode)
        {
            if (typeface == null) throw new ArgumentNullException(nameof(typeface));

            var font = EnsureFont(typeface);
            var glyph = font.RegisterGlyph(glyphId, unicode);
            return new GlyphRegistration(font, glyph);
        }

        public IReadOnlyCollection<EmbeddedFont> GetFonts() => _fonts.Values;

        public void Reset()
        {
            _fonts.Clear();
            _fontSequence = 1;
        }

        private EmbeddedFont EnsureFont(SKTypeface typeface)
        {
            long key = typeface.Handle.ToInt64();
            if (_fonts.TryGetValue(key, out var existing))
                return existing;

            string resourceName = $"/Ff{_fontSequence++}";
            var embedded = new EmbeddedFont(resourceName, typeface);
            _fonts[key] = embedded;
            return embedded;
        }
    }

    internal sealed class EmbeddedFont
    {
        private readonly Dictionary<GlyphKey, EmbeddedGlyph> _glyphs = new();
        private readonly SKTypeface _typeface;
        private readonly string _baseFontName;
        private byte[]? _fontData;
        private int _nextCid = 1;

        public EmbeddedFont(string resourceName, SKTypeface typeface)
        {
            ResourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
            _typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
            _baseFontName = BuildBaseFontName(typeface);
        }

        public string ResourceName { get; }
        public string BaseFontName => _baseFontName;
        public SKTypeface Typeface => _typeface;
        public IReadOnlyCollection<EmbeddedGlyph> Glyphs => _glyphs.Values;

        public EmbeddedGlyph RegisterGlyph(uint glyphId, string unicode)
        {
            var key = new GlyphKey(glyphId, unicode ?? string.Empty);
            if (_glyphs.TryGetValue(key, out var existing))
                return existing;

            if (_nextCid > ushort.MaxValue)
                throw new InvalidOperationException("A single embedded font cannot contain more than 65,535 glyph-to-Unicode mappings.");

            // A glyph may represent different Unicode text in separate HarfBuzz clusters.
            // Assign a CID per glyph/text mapping so /ToUnicode remains lossless.
            ushort cid = (ushort)_nextCid++;
            float width = MeasureGlyphWidth(glyphId);
            var record = new EmbeddedGlyph(glyphId, cid, key.Unicode, width);
            _glyphs[key] = record;
            return record;
        }

        public byte[] GetFontData()
        {
            if (_fontData != null)
                return _fontData;

            using var stream = _typeface.OpenStream();
            if (stream == null)
                throw new InvalidOperationException($"Unable to open font stream for '{_typeface.FamilyName}'.");

            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = stream.Read(buffer, buffer.Length)) > 0)
                memory.Write(buffer, 0, read);

            _fontData = memory.ToArray();
            return _fontData;
        }

        private float MeasureGlyphWidth(uint glyphId)
        {
            using var font = new SKFont(_typeface, 1000f);
            Span<ushort> glyph = stackalloc ushort[1];
            glyph[0] = (ushort)glyphId;

            Span<float> widths = stackalloc float[1];
            font.GetGlyphWidths(glyph, widths, Span<SKRect>.Empty);

            return widths[0];
        }

        private static string BuildBaseFontName(SKTypeface typeface)
        {
            string family = typeface.FamilyName ?? "Font";
            var sb = new StringBuilder(family.Length);
            foreach (char ch in family)
            {
                if (ch >= 0x21 && ch <= 0x7E && !char.IsWhiteSpace(ch))
                    sb.Append(ch);
            }
            return sb.Length > 0 ? sb.ToString() : "Font";
        }
    }

    internal readonly record struct GlyphKey(uint GlyphId, string Unicode);

    internal sealed class EmbeddedGlyph
    {
        public EmbeddedGlyph(uint glyphId, ushort cid, string unicode, float width)
        {
            GlyphId = glyphId;
            Cid = cid;
            Unicode = unicode ?? string.Empty;
            Width = width;
        }

        public uint GlyphId { get; }
        public ushort Cid { get; }
        public string Unicode { get; }
        public float Width { get; }
    }

    internal readonly struct GlyphRegistration
    {
        public GlyphRegistration(EmbeddedFont font, EmbeddedGlyph glyph)
        {
            Font = font ?? throw new ArgumentNullException(nameof(font));
            Glyph = glyph ?? throw new ArgumentNullException(nameof(glyph));
        }

        public EmbeddedFont Font { get; }
        public EmbeddedGlyph Glyph { get; }
    }
}
