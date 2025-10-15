using System;
using System.Collections.Generic;
using PdfBuilder.Writer.Fonts;

namespace PdfBuilder.Writer
{
    public sealed class PdfRenderContext
    {
        internal PdfRenderContext(Dictionary<string, int> legacyFontMap, EmbeddedFontRegistry embeddedFonts)
        {
            LegacyFontMap = legacyFontMap ?? throw new ArgumentNullException(nameof(legacyFontMap));
            EmbeddedFonts = embeddedFonts ?? throw new ArgumentNullException(nameof(embeddedFonts));
        }

        public Dictionary<string, int> LegacyFontMap { get; }

        internal EmbeddedFontRegistry EmbeddedFonts { get; }
    }
}
