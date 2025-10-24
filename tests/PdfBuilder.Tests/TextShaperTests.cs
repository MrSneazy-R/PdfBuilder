using System.Linq;
using FluentAssertions;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using Xunit;

namespace PdfBuilder.Tests
{
    public class TextShaperTests
    {
        [Fact]
        public void ShapeParagraph_LongWord_WrapsUsingFallbackBreaker()
        {
            const string text = "Supercalifragilisticexpialidocious";

            var request = new TextShapingRequest(
                text,
                fontFamily: "Helvetica",
                fontSize: 36f,
                lineHeight: 1.1f,
                maxWidth: 80f,
                bold: false,
                italic: false,
                smallCaps: false,
                monospace: false,
                fallbackFonts: null);

            var paragraph = TextShaper.Shared.ShapeParagraph(request);

            paragraph.Lines.Select(l => l.Text).Aggregate(string.Empty, (acc, value) => acc + value)
                .Should().Be(text);
            paragraph.MaxLineWidth.Should().BeLessOrEqualTo(request.MaxWidth + 0.5f);
        }

        [Fact]
        public void ShapeParagraph_TextWithoutSpaces_PreservesCharacters()
        {
            const string text = "ABCDEFGHIJKL";

            var request = new TextShapingRequest(
                text,
                fontFamily: "Helvetica",
                fontSize: 36f,
                lineHeight: 1.1f,
                maxWidth: 70f,
                bold: false,
                italic: false,
                smallCaps: false,
                monospace: false,
                fallbackFonts: null);

            var paragraph = TextShaper.Shared.ShapeParagraph(request);

            string recomposed = string.Concat(paragraph.Lines.Select(l => l.Text));
            recomposed.Should().Be(text);
            paragraph.MaxLineWidth.Should().BeLessOrEqualTo(request.MaxWidth + 0.5f);
        }
    }
}
