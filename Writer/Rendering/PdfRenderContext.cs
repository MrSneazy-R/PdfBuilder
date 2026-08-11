using System;
using System.Collections.Generic;
using PdfBuilder.Document;
using PdfBuilder.Writer.Fonts;

namespace PdfBuilder.Writer
{
    public sealed class PdfRenderContext
    {
        internal PdfRenderContext(Dictionary<string, int> legacyFontMap, EmbeddedFontRegistry embeddedFonts, PaginationRegistry? pagination = null)
        {
            LegacyFontMap = legacyFontMap ?? throw new ArgumentNullException(nameof(legacyFontMap));
            EmbeddedFonts = embeddedFonts ?? throw new ArgumentNullException(nameof(embeddedFonts));
            Pagination = pagination;
        }

        public Dictionary<string, int> LegacyFontMap { get; }

        internal EmbeddedFontRegistry EmbeddedFonts { get; }
        internal PaginationRegistry? Pagination { get; }
    }
}
