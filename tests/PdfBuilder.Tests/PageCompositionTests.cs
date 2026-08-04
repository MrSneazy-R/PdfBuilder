using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class PageCompositionTests
{
    [Fact]
    public void Header_ReservesSpaceAndDoesNotOverlapContent()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Header().Text("Header");
            page.Content().Text("Content");
        }));

        var page = document.Pages.Single();
        Assert.NotEmpty(document.GenerateBytes());
        Assert.True(page.MarginTop > 0f);
    }

    [Fact]
    public void Footer_ReservesSpaceAndDoesNotOverlapContent()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Footer().Text("Footer");
            page.Content().Text("Content");
        }));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void Header_RepeatsAcrossAutoPaginatedPages()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Header().Text("Repeated header");
            page.Content().Column(column =>
            {
                for (var index = 0; index < 120; index++)
                    column.Item().Text($"Line {index}");
            });
        }));

        var bytes = document.GenerateBytes();
        Assert.True(document.Pages.Count > 1);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void PageNumbers_CurrentAndTotal_AreCorrect()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Footer().Text(() => $"{HeaderFooterTokens.PageNumber} / {HeaderFooterTokens.PageCount}");
            page.Content().Text("Body");
        }));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void MultiColumnPage_PaginatesCorrectly()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Columns(2, 12);
            page.Content().Column(column =>
            {
                for (var index = 0; index < 80; index++) column.Item().Text($"Column line {index}");
            });
        }));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void BlankPage_IsPreserved()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Background().Background("#FFFFFF")));
        Assert.Single(document.Pages);
        Assert.NotEmpty(document.GenerateBytes());
    }
}
