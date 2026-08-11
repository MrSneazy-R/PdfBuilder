using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class PageContextTests
{
    [Theory]
    [InlineData(1, "1 of 1")]
    [InlineData(9, "1 of 9")]
    [InlineData(10, "10 of 10")]
    public void PageText_FinalPagination_ResolvesExpectedNumbers(int pageCount, string expected)
    {
        PdfDocument document = CreateNumberedDocument(pageCount);

        string text = ExtractText(document.GenerateBytes());

        text.Should().Contain(expected);
    }

    [Fact]
    public void PageText_DigitCountTransition_DoesNotChangeFinalPageCount()
    {
        PdfDocument document = CreateNumberedDocument(10);

        byte[] bytes = document.GenerateBytes();

        document.Pages.Should().HaveCount(10);
        ExtractText(bytes).Should().Contain("9 of 10").And.Contain("10 of 10");
    }

    [Fact]
    public void PageText_HeaderFooterAndContentTokens_UseFinalPagination()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Page(page =>
            {
                page.Header().PageText($"Header {PageTextTokens.CurrentPage}/{PageTextTokens.TotalPages}");
                page.Content().PageText($"Content {PageTextTokens.CurrentPage}/{PageTextTokens.TotalPages}");
                page.Footer().PageText($"Footer {PageTextTokens.CurrentPage}/{PageTextTokens.TotalPages}");
            });
        });
        MakeDeterministic(document);

        string text = ExtractText(document.GenerateBytes());

        text.Should().Contain("Header 1/1").And.Contain("Content 1/1").And.Contain("Footer 1/1");
    }

    [Fact]
    public void LegacyHeaderFooterTemplate_PageTokens_RemainSupported()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Page(page => page.Content().Text("First"));
            descriptor.Page(page => page.Content().Text("Second"));
        });
        document.HeaderFooter.FooterTemplate = "Legacy {page} of {pages}";
        MakeDeterministic(document);

        string text = ExtractText(document.GenerateBytes());

        text.Should().Contain("Legacy 1 of 2").And.Contain("Legacy 2 of 2");
    }

    [Fact]
    public void PageContext_FinalPagination_ContainsImmutablePageFlagsAndDimensions()
    {
        var contexts = new List<PageContext>();
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Page(page => page.Content().Text("First"));
            descriptor.Page(page => page.Content().Text("Second"));
        });
        document.HeaderFooter.HeaderHeight = 20f;
        document.HeaderFooter.HeaderLayout = new HeaderFooterLayoutDefinition(composer =>
        {
            contexts.Add(HeaderFooterTokens.Context);
            composer.Text("Header");
        });
        MakeDeterministic(document);

        document.GenerateBytes();

        contexts.Should().HaveCount(2);
        contexts[0].CurrentPage.Should().Be(1);
        contexts[0].TotalPages.Should().Be(2);
        contexts[0].IsFirstPage.Should().BeTrue();
        contexts[0].IsLastPage.Should().BeFalse();
        contexts[0].IsOddPage.Should().BeTrue();
        contexts[0].IsEvenPage.Should().BeFalse();
        contexts[1].IsLastPage.Should().BeTrue();
        contexts[1].IsEvenPage.Should().BeTrue();
        contexts[0].PageWidth.Should().Be(document.Pages[0].Width);
        contexts[0].PageHeight.Should().Be(document.Pages[0].Height);
        contexts[0].AvailableContentWidth.Should().BeGreaterThan(0f);
        contexts[0].AvailableContentHeight.Should().BeGreaterThan(0f);
        typeof(PageContext).GetProperties().Should().OnlyContain(property => property.SetMethod == null);
    }

    [Fact]
    public void PageContext_CancelledGeneration_StopsBeforeFinalization()
    {
        PdfDocument document = CreateNumberedDocument(2);
        using var source = new CancellationTokenSource();
        source.Cancel();

        Action generate = () => document.GenerateBytes(source.Token);

        generate.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void PageContext_RenderLimit_TerminatesFinalization()
    {
        PdfDocument document = CreateNumberedDocument(1);
        document.RenderLimits.MaximumPaginationPasses = 0;

        Action generate = () => document.GenerateBytes();

        generate.Should().Throw<PdfPaginationStabilizationException>()
            .Which.Message.Should().Contain(nameof(PdfRenderLimits.MaximumPaginationPasses));
    }

    [Fact]
    public void PageContext_PageMutatingCallback_FailsWithActionableBoundedDiagnostic()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
            descriptor.Page(page => page.Content().Text("Initial")));
        document.RenderLimits.MaximumPaginationPasses = 2;
        document.HeaderFooter.HeaderLayout = new HeaderFooterLayoutDefinition(composer =>
        {
            document.AddPage();
            composer.Text("Unstable");
        });

        Action generate = () => document.GenerateBytes();

        PdfPaginationStabilizationException exception = generate.Should()
            .Throw<PdfPaginationStabilizationException>()
            .Which;
        exception.PassLimit.Should().Be(2);
        exception.Message.Should().Contain("did not stabilize");
    }

    [Fact]
    public void PageText_DeterministicGeneration_ProducesIdenticalBytes()
    {
        PdfDocument document = CreateNumberedDocument(10);

        byte[] first = document.GenerateBytes();
        byte[] second = document.GenerateBytes();

        second.Should().Equal(first);
    }

    [Fact]
    public async Task PageText_ParallelGeneration_ProducesIdenticalBytes()
    {
        PdfDocument document = CreateNumberedDocument(10);

        byte[][] outputs = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Task.Run(document.GenerateBytes)));

        outputs.Skip(1).Should().OnlyContain(output => output.SequenceEqual(outputs[0]));
    }

    private static PdfDocument CreateNumberedDocument(int pageCount)
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
            {
                int capturedPage = pageNumber;
                descriptor.Page(page =>
                {
                    page.Content().Text($"Body {capturedPage}");
                    page.Footer().PageText($"{PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}");
                });
            }
        });
        MakeDeterministic(document);
        return document;
    }

    private static void MakeDeterministic(PdfDocument document)
    {
        document.GenerationOptions.Deterministic = true;
        document.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.ModificationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.DocumentIdSeed = "page-context-tests";
    }

    private static string ExtractText(byte[] bytes)
        => string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes));
}
