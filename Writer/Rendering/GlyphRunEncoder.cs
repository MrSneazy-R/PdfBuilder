using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PdfBuilder.TextShaping;
using PdfBuilder.Writer.Fonts;
using PdfBuilder.Writer.Rendering;

namespace PdfBuilder.Writer
{
    internal static class GlyphRunEncoder
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;

        public static EncodedGlyphRun Encode(ShapedRun run, PdfRenderContext context)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (run.Glyphs.Count == 0)
                throw new InvalidOperationException("Shaped run contains no glyphs.");

            if (TryEncodeBaseFont(run, context, out var baseEncoded))
                return baseEncoded;

            var chunks = new List<string>();
            var glyphBuffer = new StringBuilder();
            EmbeddedFont? embeddedFont = null;

            float accumulatedDelta = 0f;
            int glyphsSinceFlush = 0;

            foreach (var glyph in run.Glyphs)
            {
                var registration = context.EmbeddedFonts.RegisterGlyph(run.Typeface, glyph.GlyphId, glyph.Unicode);
                var font = registration.Font;
                var embeddedGlyph = registration.Glyph;

                if (embeddedFont == null)
                {
                    embeddedFont = font;
                }
                else if (!ReferenceEquals(embeddedFont, font))
                {
                    FlushPending(glyphBuffer, chunks, run.FontSize, ref accumulatedDelta, ref glyphsSinceFlush);
                    embeddedFont = font;
                }

                glyph.AssignedCid ??= embeddedGlyph.Cid;
                glyphBuffer.AppendFormat("{0:X4}", glyph.AssignedCid.Value);

                float designAdvance = glyph.DesignAdvance;
                float actualAdvance = glyph.AdvanceX;
                float delta = designAdvance - actualAdvance;
                if (Math.Abs(delta) > 0.01f)
                {
                    accumulatedDelta += delta;
                    glyphsSinceFlush++;

                    if (glyphsSinceFlush >= 16 || TextJustification.IsWordSpacingGlyph(glyph.Unicode))
                        FlushPending(glyphBuffer, chunks, run.FontSize, ref accumulatedDelta, ref glyphsSinceFlush);
                }
            }

            FlushPending(glyphBuffer, chunks, run.FontSize, ref accumulatedDelta, ref glyphsSinceFlush, force: true);
            FlushGlyphBuffer(glyphBuffer, chunks);

            var tjBuilder = new StringBuilder();
            tjBuilder.Append("[ ");
            for (int i = 0; i < chunks.Count; i++)
            {
                tjBuilder.Append(chunks[i]);
                if (i < chunks.Count - 1)
                    tjBuilder.Append(' ');
            }
            tjBuilder.Append(" ] TJ");

            return new EncodedGlyphRun(
                embeddedFont?.ResourceName ?? throw new InvalidOperationException("No embedded font resolved."),
                run.FontSize,
                tjBuilder.ToString());
        }

        private static bool TryEncodeBaseFont(ShapedRun run, PdfRenderContext context, out EncodedGlyphRun encoded)
        {
            encoded = null!;

            string normalizedKey = FontManager.NormalizeFontKey(run.FontFamily, run.Bold, run.Italic);
            string baseFont = FontManager.MapToBase14(normalizedKey);
            if (!context.LegacyFontMap.TryGetValue(baseFont, out int fontObjectId))
                return false;

            var chunks = new List<string>();
            var byteBuffer = new List<byte>();

            float accumulatedDelta = 0f;
            int glyphsSinceFlush = 0;

            foreach (var glyph in run.Glyphs)
            {
                if (string.IsNullOrEmpty(glyph.Unicode))
                    return false;

                foreach (char ch in glyph.Unicode)
                {
                    if (ch < 0x20 || ch > 0x7E)
                        return false;
                    byteBuffer.Add((byte)ch);
                }

                float designAdvance = glyph.DesignAdvance;
                float actualAdvance = glyph.AdvanceX;
                float delta = designAdvance - actualAdvance;
                if (Math.Abs(delta) > 0.01f)
                {
                    accumulatedDelta += delta;
                    glyphsSinceFlush++;

                    if (glyphsSinceFlush >= 16 || TextJustification.IsWordSpacingGlyph(glyph.Unicode))
                        FlushPending(byteBuffer, chunks, run.FontSize, ref accumulatedDelta, ref glyphsSinceFlush);
                }
            }

            FlushPending(byteBuffer, chunks, run.FontSize, ref accumulatedDelta, ref glyphsSinceFlush, force: true);
            FlushByteBuffer(byteBuffer, chunks);
            if (chunks.Count == 0)
                return false;

            var tjBuilder = new StringBuilder();
            tjBuilder.Append("[ ");
            for (int i = 0; i < chunks.Count; i++)
            {
                tjBuilder.Append(chunks[i]);
                if (i < chunks.Count - 1)
                    tjBuilder.Append(' ');
            }
            tjBuilder.Append(" ] TJ");

            encoded = new EncodedGlyphRun($"/F{fontObjectId}", run.FontSize, tjBuilder.ToString());
            return true;
        }

        private static void FlushPending(StringBuilder glyphBuffer, List<string> chunks, float fontSize, ref float accumulatedDelta, ref int glyphsSinceFlush, bool force = false)
        {
            if (!force && Math.Abs(accumulatedDelta) <= 0.1f)
                return;

            if (Math.Abs(accumulatedDelta) <= 0.1f)
            {
                accumulatedDelta = 0f;
                glyphsSinceFlush = 0;
                return;
            }

            FlushGlyphBuffer(glyphBuffer, chunks);
            float adjustment = MathF.Round((accumulatedDelta / fontSize) * 1000f, 2);
            if (Math.Abs(adjustment) > 0.1f)
                chunks.Add(adjustment.ToString("0.##", Inv));

            accumulatedDelta = 0f;
            glyphsSinceFlush = 0;
        }

        private static void FlushPending(List<byte> buffer, List<string> chunks, float fontSize, ref float accumulatedDelta, ref int glyphsSinceFlush, bool force = false)
        {
            if (!force && Math.Abs(accumulatedDelta) <= 0.1f)
                return;

            if (Math.Abs(accumulatedDelta) <= 0.1f)
            {
                accumulatedDelta = 0f;
                glyphsSinceFlush = 0;
                return;
            }

            FlushByteBuffer(buffer, chunks);
            float adjustment = MathF.Round((accumulatedDelta / fontSize) * 1000f, 2);
            if (Math.Abs(adjustment) > 0.1f)
                chunks.Add(adjustment.ToString("0.##", Inv));

            accumulatedDelta = 0f;
            glyphsSinceFlush = 0;
        }

        private static void FlushGlyphBuffer(StringBuilder glyphBuffer, List<string> chunks)
        {
            if (glyphBuffer.Length == 0)
                return;
            chunks.Add($"<{glyphBuffer}>");
            glyphBuffer.Clear();
        }

        private static void FlushByteBuffer(List<byte> buffer, List<string> chunks)
        {
            if (buffer.Count == 0)
                return;

            var sb = new StringBuilder(buffer.Count * 2);
            foreach (byte b in buffer)
                sb.Append(b.ToString("X2"));

            chunks.Add($"<{sb}>");
            buffer.Clear();
        }
    }

    internal sealed record EncodedGlyphRun(string FontResourceName, float FontSize, string TjCommand);
}
