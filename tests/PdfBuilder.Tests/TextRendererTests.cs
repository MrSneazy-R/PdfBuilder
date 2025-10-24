using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using PdfBuilder.Writer.Fonts;
using Xunit;

namespace PdfBuilder.Tests
{
    public class TextRendererTests
    {
        [Fact]
        public void Append_WithJustifyAlignment_EmitsPositiveWordSpacing()
        {
            var element = new TextElement("alpha beta gamma delta", 40, 720)
            {
                FontSize = 12f,
                MaxWidth = 160f,
                Alignment = TextAlignment.Justify,
                LineHeight = 1.2f
            };

            var sb = new StringBuilder();
            var fontMap = new Dictionary<string, int> { ["Helvetica"] = 1 };
            var context = new PdfRenderContext(fontMap, new EmbeddedFontRegistry());

            TextRenderer.Append(sb, element, 792f, context);

            var output = sb.ToString();
            Regex.IsMatch(output, @"\b(?!0(?:\.0*)?)\d+(\.\d+)?\sTw\b").Should().BeTrue("a positive word spacing value should be set for justification");
            Regex.Matches(output, @"0\sTw\b").Count.Should().BeGreaterOrEqualTo(1);
        }
    }
}
