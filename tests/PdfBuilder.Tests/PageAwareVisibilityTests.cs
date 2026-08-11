using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class PageAwareVisibilityTests
{
    [Fact]
    public void HeaderVariants_AndPageNumbers_UseFinalPagination()
    {
        PdfDocument document = CreateFlowingDocument(page =>
        {
            page.FirstPageHeader().Text("FIRST REPORT HEADER").Bold();
            page.ContinuationHeader().Text("CONTINUED REPORT HEADER").Bold();
            page.FirstPageFooter().Row(row =>
            {
                row.RelativeItem().Text("FIRST FOOTER").FontSize(6);
                row.AutoItem().PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}").FontSize(6);
            });
            page.ContinuationFooter().Row(row =>
            {
                row.RelativeItem().Text("CONTINUED FOOTER").FontSize(6);
                row.AutoItem().PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}").FontSize(6);
            });
        });

        string text = ExtractText(document.GenerateBytes());

        document.Pages.Should().HaveCountGreaterThan(2);
        Count(text, "FIRST REPORT HEADER").Should().Be(1);
        Count(text, "CONTINUED REPORT HEADER").Should().Be(document.Pages.Count - 1);
        Count(text, "FIRST FOOTER").Should().Be(1);
        Count(text, "CONTINUED FOOTER").Should().Be(document.Pages.Count - 1);
        text.Should().Contain($"Page 1 of {document.Pages.Count}");
        text.Should().Contain($"Page {document.Pages.Count} of {document.Pages.Count}");
    }

    [Fact]
    public void RepeatedContent_ShowOnceSkipOnceOddAndEven_AreDeterministic()
    {
        PdfDocument document = CreateFlowingDocument(page => page.Header().Stack(stack =>
        {
            stack.Item().ShowOnce().Text("SHOW-ONCE");
            stack.Item().SkipOnce().Text("SKIP-ONCE");
            stack.Item().OddPagesOnly().Text("ODD-PAGE");
            stack.Item().EvenPagesOnly().Text("EVEN-PAGE");
        }));

        string text = ExtractText(document.GenerateBytes());

        Count(text, "SHOW-ONCE").Should().Be(1);
        Count(text, "SKIP-ONCE").Should().Be(document.Pages.Count - 1);
        Count(text, "ODD-PAGE").Should().Be((document.Pages.Count + 1) / 2);
        Count(text, "EVEN-PAGE").Should().Be(document.Pages.Count / 2);
    }

    [Fact]
    public void LastPageOnly_BodyContent_ReflowsOntoTheFinalPage()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(new PageSize(300, 260));
            page.Margin(20);
            page.Content().Column(column =>
            {
                for (int index = 0; index < 24; index++)
                    column.Item().Text($"Body row {index}");
                column.Item().LastPageOnly().DebugLabel("Final certification").Text("FINAL-PAGE-CERTIFICATION");
            });
        }));

        byte[] first = document.GenerateBytes();
        byte[] second = document.GenerateBytes();

        document.Pages.Should().HaveCountGreaterThan(1);
        Count(ExtractText(first), "FINAL-PAGE-CERTIFICATION").Should().Be(1);
        second.Should().Equal(first);
    }

    [Fact]
    public void FirstPageOnlyHeader_ReservesSpaceOnlyWhereVisible()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Page(page =>
            {
                page.Size(new PageSize(300, 260));
                page.Margin(20);
                page.Header().FirstPageOnly().Text("FIRST ONLY");
                page.Content().Text("FIRST BODY");
            });
            descriptor.Page(page =>
            {
                page.Size(new PageSize(300, 260));
                page.Margin(20);
                page.Content().Text("SECOND BODY");
            });
        });

        document.GenerateBytes();

        TextElement firstBody = document.Pages[0].Elements.OfType<TextElement>().Single(element => element.Text == "FIRST BODY");
        TextElement secondBody = document.Pages[1].Elements.OfType<TextElement>().Single(element => element.Text == "SECOND BODY");
        secondBody.Y.Should().BeApproximately(firstBody.Y + 28f, 0.1f);
    }

    [Fact]
    public void HiddenRepeatedContent_CreatesNoAnnotationsAnchorsOutlinesOrResources()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(new PageSize(300, 260));
            page.Margin(20);
            IContainer hidden = page.Header().FirstPageOnly().EvenPagesOnly();
            hidden.Anchor("hidden-anchor");
            hidden.Bookmark("hidden-bookmark", "Hidden bookmark");
            hidden.ExternalLink("Hidden external link", "https://example.com");
            hidden.InternalLink("Hidden internal link", "missing-target");
            hidden.Image([0x00, 0x01, 0x02], 10, 10);
            page.Content().Text("Visible body");
            page.Content().PageBreak().Text("Visible continuation");
        }));

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().NotContain("/Subtype /Link");
        pdf.Should().NotContain("/Subtype /Image");
        pdf.Should().NotContain("/Type /Outlines");
        pdf.Should().NotContain("hidden-anchor").And.NotContain("hidden-bookmark");
        document.NavigationDiagnostics.Entries.Should().BeEmpty();
        ExtractText(Encoding.Latin1.GetBytes(pdf)).Should().NotContain("Hidden");
    }

    [Fact]
    public async Task PageAwareVisibility_ParallelGeneration_IsStable()
    {
        PdfDocument document = CreateFlowingDocument(page =>
        {
            page.FirstPageHeader().Text("FIRST");
            page.ContinuationHeader().Text("CONTINUED");
            page.Footer().PageText($"{PageTextTokens.CurrentPage}/{PageTextTokens.TotalPages}");
        });

        byte[][] outputs = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Task.Run(document.GenerateBytes)));

        outputs.Skip(1).Should().OnlyContain(output => output.SequenceEqual(outputs[0]));
    }

    [Fact]
    public void PageAwareStabilization_LoopRespectsRenderLimitWithDiagnosticPath()
    {
        Action compose = () => PdfDocument.Create(descriptor =>
        {
            descriptor.RenderLimits(limits => limits.MaximumPaginationPasses = 3);
            descriptor.Page(page =>
            {
                page.Size(new PageSize(300, 260));
                page.Margin(20);
                page.Content().LastPageOnly().DebugLabel("Final-page marker").Text("FINAL");
            });
            descriptor.Page(page => page.Content().Text("TRAILING PAGE"));
        });

        PdfPaginationStabilizationException exception = compose.Should()
            .Throw<PdfPaginationStabilizationException>()
            .Which;
        exception.PassLimit.Should().Be(3);
        exception.PassCount.Should().Be(4);
        exception.Message.Should().Contain("Final-page marker")
            .And.Contain(nameof(PdfRenderLimits.MaximumPaginationPasses));
    }

    private static PdfDocument CreateFlowingDocument(Action<IPageDescriptor> configurePage)
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(new PageSize(300, 260));
            page.Margin(20);
            configurePage(page);
            page.Content().Column(column =>
            {
                for (int index = 0; index < 28; index++)
                    column.Item().Text($"Report row {index}");
            });
        }));
        document.GenerationOptions.Deterministic = true;
        document.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.ModificationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.DocumentIdSeed = "page-aware-visibility";
        return document;
    }

    private static int Count(string value, string marker)
        => value.Split(marker, StringSplitOptions.None).Length - 1;

    private static string ExtractText(byte[] bytes)
        => string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes));
}
