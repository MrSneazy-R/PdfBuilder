using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Fonts;
using PdfBuilder.Models;
using PdfBuilder.Writer.Fonts;
using Xunit;

namespace PdfBuilder.Tests;

[Collection("Font catalogue serial")]
public sealed class TypographyV2Tests
{
    private static readonly object FontCatalogLock = new();

    [Fact]
    public void CanonicalTypography_OrdinaryRichAndTableText_UseCommonStyleSurface()
    {
        const string ordinary = "Ordinary café";

        var document = PdfDocument.Create(document =>
        {
            document.Theme(theme => theme
                .Color("Ink", "#17324D")
                .TextStyle("Common", style => style.FontFamily("Helvetica").FontSize(11).Color("Ink")
                    .LineHeight(1.25f).LetterSpacing(0.1f).WordSpacing(0.2f).Italic().Underline()
                    .Decoration("Ink", 0.6f, TextDecorationStyle.Dashed).FallbackFonts("Noto Sans", "Arial Unicode MS")));
            document.Page(page =>
            {
                page.Margin(36);
                page.Content().Column(column =>
                {
                    column.Item().Text(ordinary).Style("Common").Bold().Highlight("#FFF4CC").Overline();
                    column.Item().RichText(paragraph =>
                    {
                        paragraph.DefaultStyle().Style("Common");
                        paragraph.Span("Rich ").Bold();
                        paragraph.Span("العربية ").Direction(TextDirection.RightToLeft);
                        paragraph.Span("שלום").Strikethrough().Subscript();
                    });
                    column.Item().Table(tableDescriptor =>
                    {
                        tableDescriptor.Columns(columns => columns.RelativeColumn());
                        tableDescriptor.Row(row => row.Cell().Text("Table 中文").Style("Common").Superscript().NoWrap().Ellipsis());
                    });
                });
            });
        });

        var bytes = document.GenerateBytes();
        var extracted = string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes));

        extracted.Should().Contain(ordinary);
        document.Pages.SelectMany(page => page.Elements).OfType<RichTextElement>().Should().ContainSingle()
            .Which.Runs.Should().HaveCount(3);
        document.Pages.SelectMany(page => page.Elements).OfType<TableElement>().Should().ContainSingle()
            .Which.Rows.SelectMany(row => row.Cells).Single().TextStyle.Should().NotBeNull();
        Encoding.ASCII.GetString(bytes).Should().Contain("/ToUnicode");
    }

    [Fact]
    public void CanonicalRichText_StyledSpans_InheritThemeAndExtractIndependently()
    {
        var document = PdfDocument.Create(document =>
        {
            document.Theme(theme => theme.TextStyle("Body", style => style.FontSize(12).Color("#222222").FallbackFonts("Noto Sans")));
            document.Page(page => page.Content().RichText(paragraph =>
            {
                paragraph.DefaultStyle().Style("Body").LineHeight(1.4f);
                paragraph.Span("office ").Bold();
                paragraph.Span("café ").Italic().Underline();
                paragraph.Span("Ångström").Superscript().Overline();
            }));
        });

        var blocks = PdfTextExtractor.ExtractTextBlocks(document.GenerateBytes());
        string.Join(string.Empty, blocks).Should().Contain("office").And.Contain("café").And.Contain("Ångström");
    }

    [Fact]
    public void CanonicalRichText_LongParagraph_SplitsAcrossPages()
    {
        string text = string.Join(" ", Enumerable.Range(0, 120).Select(index => $"item-{index}"));
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(new PageSize(220, 180));
            page.Margin(18);
            page.Header().Text("Repeated header").FontSize(8);
            page.Footer().Text("Repeated footer").FontSize(8);
            page.Content().RichText(paragraph =>
            {
                paragraph.DefaultStyle().FontSize(10).LineHeight(1.2f);
                paragraph.Span(text);
            });
        }));

        var bytes = document.GenerateBytes();

        document.Pages.Count.Should().BeGreaterThan(1);
        string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes)).Should().Contain("item-0").And.Contain("item-119");
    }

    [Fact]
    public void CanonicalText_WrappingHyphenationEllipsisAndMaximumLines_AreAppliedBeforeRendering()
    {
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Margin(36);
            page.Content().Width(70).Text("extraordinarylongword followed by hidden text")
                .Hyphenate().MaximumLines(1).Ellipsis();
        }));
        document.OutputOptions.ReadableContentStreams = true;

        var bytes = document.GenerateBytes();
        var extracted = string.Join("", PdfTextExtractor.ExtractTextBlocks(bytes));
        var rendered = document.Pages.SelectMany(page => page.Elements).OfType<TextElement>().Single();

        rendered.Wrapping.Should().Be(TextWrapping.Hyphenate);
        rendered.MaximumLines.Should().Be(1);
        rendered.ShapedLayout.Should().NotBeNull();
        rendered.ShapedLayout!.Lines.Should().ContainSingle();
        rendered.ShapedLayout.Lines[0].Text.Should().MatchRegex("[-…]");
        rendered.Text.Should().MatchRegex("[-…]");
        extracted.Should().MatchRegex("[-…]");
        PdfContentHelper.ExtractFirstStream(bytes).Length.Should().BeLessThan(4_000);
    }

    [Theory]
    [InlineData("office affine café Ångström", "café")]
    [InlineData("مرحبا بالعالم", "مرحبا")]
    [InlineData("שלום עולם", "שלום")]
    [InlineData("Report مرحبا 2026 שלום", "مرحبا")]
    [InlineData("中文文本 日本語", "中文")]
    public void MultilingualText_HarfBuzzAndToUnicode_PreserveExtractableText(string text, string expected)
    {
        var document = PdfDocument.Create(document => document.Page(page =>
            page.Content().Text(text).Direction(TextDirection.Automatic)
                .FallbackFonts("Noto Sans", "Noto Sans Arabic", "Noto Sans Hebrew", "Noto Sans CJK SC")));

        var bytes = document.GenerateBytes();
        var extracted = string.Join("", PdfTextExtractor.ExtractTextBlocks(bytes));

        extracted.Should().Contain(expected);
        Encoding.ASCII.GetString(bytes).Should().Contain("/ToUnicode");
    }

    [Fact]
    public void FontCatalog_Snapshot_IsVersionedAndImmutable()
    {
        lock (FontCatalogLock)
        {
            var previous = FontCatalog.FallbackFonts.ToArray();
            try
            {
                FontCatalog.SetFallbackFonts("Snapshot-A");
                var snapshot = FontCatalog.CaptureSnapshot();
                FontCatalog.SetFallbackFonts("Snapshot-B");
                var later = FontCatalog.CaptureSnapshot();

                snapshot.FallbackFonts.Should().Equal("Snapshot-A");
                later.FallbackFonts.Should().Equal("Snapshot-B");
                later.Version.Should().BeGreaterThan(snapshot.Version);
            }
            finally
            {
                FontCatalog.SetFallbackFonts(previous);
            }
        }
    }

    [Fact]
    public void FontCatalog_StrictMissingFont_ThrowsDedicatedException()
    {
        lock (FontCatalogLock)
        {
            bool previous = FontCatalog.StrictMatching;
            try
            {
                FontCatalog.StrictMatching = true;
                Action action = () => PdfDocument.Create(document => document.Page(page =>
                    page.Content().Text("strict font").FontFamily($"Missing-{Guid.NewGuid():N}")));
                action.Should().Throw<FontNotFoundException>().WithMessage("*could not be resolved*");
            }
            finally
            {
                FontCatalog.StrictMatching = previous;
            }
        }
    }

    [Fact]
    public void FontCatalog_NonStrictMissingFont_ProducesRetainedDiagnostic()
    {
        lock (FontCatalogLock)
        {
            bool previous = FontCatalog.StrictMatching;
            try
            {
                FontCatalog.StrictMatching = false;
                FontDiagnostics.Clear();
                _ = PdfDocument.Create(document => document.Page(page =>
                    page.Content().Text("diagnostic font").FontFamily($"Missing-{Guid.NewGuid():N}")));

                FontDiagnostics.RecentMessages.Should().Contain(message => message.Contains("could not be resolved", StringComparison.Ordinal));
            }
            finally
            {
                FontCatalog.StrictMatching = previous;
            }
        }
    }

    [Fact]
    public void FontCatalog_ByteAndStreamRegistration_UseVersionedCacheKeys()
    {
        string? fontPath = FindSystemFont();
        if (fontPath == null) return;
        byte[] data = File.ReadAllBytes(fontPath);
        string aliasFromBytes = $"Bytes-{Guid.NewGuid():N}";
        string aliasFromStream = $"Stream-{Guid.NewGuid():N}";
        int before = FontCatalog.CaptureSnapshot().Version;

        FontCatalog.RegisterFont(data, aliasFromBytes);
        using var stream = new MemoryStream(data, writable: false);
        FontCatalog.RegisterFont(stream, aliasFromStream);

        FontCatalog.CaptureSnapshot().Version.Should().BeGreaterThan(before);
    }

    [Fact]
    public void FontCatalog_FileAndDirectoryRegistration_AreDeterministic()
    {
        string? fontPath = FindSystemFont();
        if (fontPath == null) return;
        string directory = Directory.CreateTempSubdirectory("pdfbuilder-fonts-").FullName;
        try
        {
            string copy = Path.Combine(directory, "01-font" + Path.GetExtension(fontPath));
            File.Copy(fontPath, copy);
            int before = FontCatalog.CaptureSnapshot().Version;

            FontCatalog.RegisterFontFile(copy, $"File-{Guid.NewGuid():N}");
            FontCatalog.RegisterFontDirectory(directory, SearchOption.TopDirectoryOnly);

            FontCatalog.CaptureSnapshot().Version.Should().BeGreaterThan(before);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FontCatalog_RejectsFontDataBeyondConfiguredGuardrail()
    {
        lock (FontCatalogLock)
        {
            long previous = FontCatalog.MaximumFontFileBytes;
            try
            {
                FontCatalog.MaximumFontFileBytes = 8;
                Action bytes = () => FontCatalog.RegisterFont(new byte[9], "TooLarge");
                Action stream = () => FontCatalog.RegisterFont(new MemoryStream(new byte[9]), "TooLargeStream");
                bytes.Should().Throw<ArgumentOutOfRangeException>();
                stream.Should().Throw<ArgumentOutOfRangeException>();
            }
            finally
            {
                FontCatalog.MaximumFontFileBytes = previous;
            }
        }
    }

    [Fact]
    public void ConcurrentCanonicalGeneration_UsesIndependentFontSnapshots()
    {
        var failures = new List<Exception>();
        Parallel.For(0, 16, index =>
        {
            try
            {
                var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
                    page.Content().RichText(paragraph =>
                    {
                        paragraph.Span($"office café {index} ").Bold();
                        paragraph.Span("مرحبا שלום 中文").Direction(TextDirection.Automatic)
                            .FallbackFonts("Noto Sans", "Noto Sans Arabic", "Noto Sans Hebrew", "Noto Sans CJK SC");
                    })));
                var bytes = document.GenerateBytes();
                if (bytes.Length == 0) throw new InvalidOperationException("No PDF bytes were generated.");
            }
            catch (Exception exception)
            {
                lock (failures) failures.Add(exception);
            }
        });

        failures.Should().BeEmpty();
    }

    [Fact]
    public void SimpleTypography_CompressedAndReadableOutputs_StayWithinBloatGuardrails()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
            page.Content().Text("A short HarfBuzz line: office café.").Underline()));
        byte[] compressed = document.GenerateBytes();
        document.OutputOptions.ReadableContentStreams = true;
        byte[] readable = document.GenerateBytes();

        compressed.Length.Should().BeLessThan(1_500_000);
        readable.Length.Should().BeLessThan(1_500_000);
        PdfContentHelper.ExtractFirstStream(readable).Length.Should().BeLessThan(4_000);
        Encoding.ASCII.GetString(compressed).Should().Contain("/FlateDecode");
    }

    private static string? FindSystemFont()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf"
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}

[CollectionDefinition("Font catalogue serial", DisableParallelization = true)]
public sealed class FontCatalogueSerialCollection;
