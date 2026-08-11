using FluentAssertions;
using PdfBuilder.Writer;
using SkiaSharp;
using Xunit;

namespace PdfBuilder.Tests;

[Collection("Font catalogue serial")]
public sealed class FontSubsetIntegrityTests
{
    [Fact]
    public void HarfBuzzSubset_RetainsGlyphIdsUsedByCidToGidMap()
    {
        string? fontPath = FindSystemFont();
        if (fontPath == null) return;
        const string text = "Compliance validation fixture";
        byte[] originalData = File.ReadAllBytes(fontPath);
        using SKTypeface originalTypeface = SKTypeface.FromFile(fontPath);
        using var originalFont = new SKFont(originalTypeface, 12);
        ushort[] originalGlyphs = GetGlyphs(originalFont, text);

        bool subsetCreated = FontSubsetter.TrySubset(
            originalData,
            originalGlyphs.Select(glyph => (uint)glyph),
            text.EnumerateRunes().Select(rune => rune.Value),
            originalTypeface.FamilyName,
            out byte[] subset);

        subsetCreated.Should().BeTrue();
        using SKData subsetData = SKData.CreateCopy(subset);
        using SKTypeface subsetTypeface = SKTypeface.FromData(subsetData);
        using var subsetFont = new SKFont(subsetTypeface, 12);
        GetGlyphs(subsetFont, text).Should().Equal(originalGlyphs,
            "the PDF CIDToGIDMap intentionally stores original HarfBuzz glyph identifiers");
    }

    [Fact]
    public void EmbeddedFontDescriptor_UsesAscendingPdfBoundingBox()
    {
        string? fontPath = FindSystemFont();
        if (fontPath == null) return;
        string alias = $"BBox-{Guid.NewGuid():N}";
        PdfBuilder.Fonts.FontCatalog.RegisterFile(fontPath, alias);
        PdfBuilder.Document.PdfDocument document = PdfBuilder.Document.PdfDocument.Create(descriptor =>
            descriptor.Page(page => page.Content().Text("Bounding box").FontFamily(alias)));

        string pdf = System.Text.Encoding.Latin1.GetString(document.GenerateBytes());
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            pdf,
            @"/FontBBox \[(-?\d+) (-?\d+) (-?\d+) (-?\d+)\]");

        match.Success.Should().BeTrue();
        int.Parse(match.Groups[1].Value).Should().BeLessThan(int.Parse(match.Groups[3].Value));
        int.Parse(match.Groups[2].Value).Should().BeLessThan(int.Parse(match.Groups[4].Value));
    }

    private static string? FindSystemFont()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf"
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static ushort[] GetGlyphs(SKFont font, string text)
    {
        var glyphs = new ushort[font.CountGlyphs(text)];
        font.GetGlyphs(text, glyphs);
        return glyphs;
    }
}
