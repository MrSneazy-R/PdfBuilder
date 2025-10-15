using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PdfBuilder.TextShaping;
using PdfBuilder.Writer.Fonts;

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

            var chunks = new List<string>();
            var glyphBuffer = new StringBuilder();
            EmbeddedFont? embeddedFont = null;

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
                    // Fallback runs should have been split before shaping. Guard to avoid mixed-font sequences.
                    FlushGlyphBuffer(glyphBuffer, chunks);
                    embeddedFont = font;
                }

                glyph.AssignedCid ??= embeddedGlyph.Cid;
                glyphBuffer.AppendFormat("{0:X4}", glyph.AssignedCid.Value);

                float designAdvance = glyph.DesignAdvance;
                float actualAdvance = glyph.AdvanceX;
                float delta = designAdvance - actualAdvance;
                if (Math.Abs(delta) > 0.01f)
                {
                    FlushGlyphBuffer(glyphBuffer, chunks);
                    float adjustment = (delta / run.FontSize) * 1000f;
                    if (Math.Abs(adjustment) > 0.01f)
                        chunks.Add(adjustment.ToString("0.###", Inv));
                }
            }

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

        private static void FlushGlyphBuffer(StringBuilder glyphBuffer, List<string> chunks)
        {
            if (glyphBuffer.Length == 0)
                return;
            chunks.Add($"<{glyphBuffer}>");
            glyphBuffer.Clear();
        }
    }

    internal sealed record EncodedGlyphRun(string FontResourceName, float FontSize, string TjCommand);
}
