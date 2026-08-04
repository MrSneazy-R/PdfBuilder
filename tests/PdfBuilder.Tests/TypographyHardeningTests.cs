using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Fonts;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using Xunit;

namespace PdfBuilder.Tests;

public class TypographyHardeningTests
{
    private static readonly object FontCatalogLock = new();

    [Fact]
    public void TextShaping_LatinLigatures_RenderCorrectly()
    {
        var paragraph = Shape("office affine", 240f);

        paragraph.Lines.Should().ContainSingle();
        paragraph.Lines[0].Glyphs().Should().NotBeEmpty();
        paragraph.Lines[0].Text.Should().Be("office affine");
    }

    [Fact]
    public void TextShaping_Arabic_ContextualForms_RenderCorrectly()
    {
        const string text = "مرحبا بالعالم";
        var paragraph = Shape(text, 300f, FlowDirection.RightToLeft);

        paragraph.Lines.Should().ContainSingle();
        paragraph.Lines[0].Glyphs().Should().NotBeEmpty();
    }

    [Fact]
    public void TextShaping_MixedDirection_PreservesLogicalExtractionOrder()
    {
        const string text = "Report مرحبا 2026";
        var pdf = Generate(text, FlowDirection.RightToLeft);

        Encoding.ASCII.GetString(pdf).Should().Contain("/ToUnicode");
        PdfTextExtractor.ExtractTextBlocks(pdf).Should().NotBeEmpty();
    }

    [Fact]
    public void FontFallback_MultipleScripts_SelectExpectedFonts()
    {
        lock (FontCatalogLock)
        {
            var previous = FontCatalog.FallbackFonts.ToArray();
            try
            {
                FontCatalog.SetFallbackFonts("Noto Sans", "Noto Sans Arabic", "Noto Sans CJK SC");
                var paragraph = Shape("Latin العربية 中文", 400f);
                paragraph.Lines.SelectMany(line => line.Runs).Should().NotBeEmpty();
            }
            finally
            {
                FontCatalog.SetFallbackFonts(previous);
            }
        }
    }

    [Fact]
    public void EmbeddedFont_SubsetContainsOnlyRequiredGlyphs()
    {
        var pdf = Generate("Café Ω");
        string ascii = Encoding.ASCII.GetString(pdf);

        ascii.Should().Contain("/FontFile2").And.Contain("/ToUnicode");
        ascii.Split("/FontFile2", StringSplitOptions.None).Length.Should().Be(2);
    }

    [Fact]
    public void EmbeddedFont_ToUnicodeMapsGlyphsCorrectly()
    {
        const string text = "Café Ω";
        var pdf = Generate(text);

        PdfTextExtractor.ExtractTextBlocks(pdf).Should().Contain(text);
    }

    [Fact]
    public void TextMeasurement_EqualsRenderedLineBreaks()
    {
        var element = new TextElement("A deterministic wrapping sample with several words.", 36f, 720f)
        {
            FontSize = 12f,
            MaxWidth = 110f
        };
        var shaped = TextElementLayouter.Layout(element, element.MaxWidth.Value);
        element.ShapedLayout = shaped;
        element.ShapedLineCount = shaped.Lines.Count;

        var document = new PdfDocument();
        document.AddPage().AddElement(element);
        string stream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(document));

        CountOccurrences(stream, "BT ").Should().Be(shaped.Lines.Count);
    }

    [Fact]
    public void MissingFont_StrictModeThrowsUsefulException()
    {
        lock (FontCatalogLock)
        {
            bool previous = FontCatalog.StrictMatching;
            try
            {
                FontCatalog.StrictMatching = true;
                Action action = () => TextShaper.Shared.ShapeParagraph(new TextShapingRequest(
                    "Missing font sample",
                    $"PdfBuilder-Missing-{Guid.NewGuid():N}",
                    12f,
                    1.2f,
                    200f,
                    false,
                    false,
                    false,
                    false,
                    null));
                action.Should().Throw<FontNotFoundException>().WithMessage("*could not be resolved*");
            }
            finally
            {
                FontCatalog.StrictMatching = previous;
            }
        }
    }

    [Fact]
    public void SimpleEmbeddedText_DoesNotCreateBloatedPdf()
    {
        var pdf = Generate("A short embedded line: café.");
        string stream = PdfContentHelper.ExtractFirstStream(pdf);

        pdf.Length.Should().BeLessThan(1_500_000);
        CountOccurrences(stream, "BT ").Should().BeLessThanOrEqualTo(2);
        stream.Length.Should().BeLessThan(2_000);
    }

    [Fact]
    public void ParallelTextShaping_DoesNotCorruptSharedState()
    {
        var failures = new List<Exception>();

        Parallel.For(0, 32, index =>
        {
            try
            {
                var paragraph = Shape($"office café {index}", 200f);
                if (paragraph.Lines.Count == 0 || paragraph.Lines[0].Glyphs().Count == 0)
                    throw new InvalidOperationException("Expected shaped glyphs.");
            }
            catch (Exception exception)
            {
                lock (failures)
                    failures.Add(exception);
            }
        });

        failures.Should().BeEmpty();
    }

    private static ShapedParagraph Shape(string text, float width, FlowDirection direction = FlowDirection.LeftToRight) =>
        TextShaper.Shared.ShapeParagraph(new TextShapingRequest(text, "Helvetica", 12f, 1.2f, width, false, false, false, false, null, direction));

    private static byte[] Generate(string text, FlowDirection direction = FlowDirection.LeftToRight)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(36).Content(column =>
            column.Text(text).FontSize(12).FlowDirection(direction).Add());
        return PdfContentHelper.Generate(document);
    }

    private static int CountOccurrences(string value, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

}

internal static class TypographyHardeningTestExtensions
{
    public static IReadOnlyList<ShapedGlyph> Glyphs(this ShapedLine line) => line.Runs.SelectMany(run => run.Glyphs).ToArray();
}
