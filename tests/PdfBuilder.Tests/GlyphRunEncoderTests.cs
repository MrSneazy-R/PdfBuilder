using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.TextShaping;
using PdfBuilder.Writer;
using PdfBuilder.Writer.Fonts;
using PdfBuilder.Writer.Rendering;
using Xunit;

namespace PdfBuilder.Tests
{
    public class GlyphRunEncoderTests
    {
        [Fact]
        public void Encode_BatchesKerningAdjustments()
        {
            var element = new TextElement("AVAVAV", 0f, 0f)
            {
                FontFamily = "Helvetica",
                FontSize = 14f,
                MaxWidth = 200f
            };

            var paragraph = TextElementLayouter.Layout(element, element.MaxWidth ?? 0f);
            var run = paragraph.Lines[0].Runs[0];

            var context = new PdfRenderContext(new Dictionary<string, int> { ["Helvetica"] = 1 }, new EmbeddedFontRegistry());
            var encoded = GlyphRunEncoder.Encode(run, context);

            var tokens = encoded.TjCommand
                .Trim()
                .TrimStart('[')
                .TrimEnd('T', 'J')
                .TrimEnd(']')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !t.StartsWith("<", StringComparison.Ordinal) && !t.EndsWith(">", StringComparison.Ordinal))
                .ToList();

            tokens.Count.Should().BeLessThanOrEqualTo(2);
        }
    }
}
