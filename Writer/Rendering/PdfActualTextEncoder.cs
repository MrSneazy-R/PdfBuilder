using System;
using System.Linq;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Writer.Rendering
{
    internal static class PdfActualTextEncoder
    {
        public static string EncodeRightToLeftRun(ShapedRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));

            // HarfBuzz returns RTL glyph clusters in visual order. PDF text extractors
            // apply the Unicode bidi algorithm to those positioned clusters, so the
            // replacement must follow the painted cluster order to avoid a second reversal.
            string visualClusterText = string.Concat(run.Glyphs.Select(glyph => glyph.Unicode));
            return PdfStringEncoder.Encode(visualClusterText);
        }
    }
}
