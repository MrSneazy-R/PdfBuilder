using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class CanonicalApiTests
{
    [Fact]
    public void CanonicalApi_MinimalDocument_GeneratesValidPdf()
    {
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Metadata(metadata => { metadata.Title = "Canonical test"; metadata.Author = "PdfBuilder"; });
            descriptor.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Content().Column(column => column.Item().Text("Canonical composition API").Bold());
            });
        });

        var bytes = document.GenerateBytes();
        Assert.True(bytes.Length > 0);
        Assert.Contains("%PDF-", System.Text.Encoding.ASCII.GetString(bytes));
        Assert.Contains("Canonical test", System.Text.Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void CanonicalApi_MultiplePages_GeneratesExpectedPageCount()
    {
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Page(page => page.Content().Text("First"));
            descriptor.Page(page => page.Content().Text("Second"));
        });

        Assert.Equal(2, document.Pages.Count);
        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void CanonicalApi_ColumnContent_DoesNotRequireAdd()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("One");
            column.Item().Text("Two").FontSize(18).Bold();
        })));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void CanonicalApi_RowRelativeAndConstantItems_RenderCorrectly()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Row(row =>
        {
            row.ConstantItem(100).Text("Fixed");
            row.RelativeItem().Text("Flexible");
        })));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void CanonicalApi_StreamOutput_MatchesByteOutput()
    {
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Metadata(metadata => metadata.CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            descriptor.Page(page => page.Content().Text("Stream"));
        });
        var bytes = document.GenerateBytes();
        using var stream = new MemoryStream();
        document.Generate(stream);
        Assert.Equal(bytes, stream.ToArray());
    }

    [Fact]
    public void CanonicalApi_ExistingLegacyApi_RemainsFunctional()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddElement(new TextElement("Legacy", 72, 720));
        Assert.NotEmpty(new PdfBuilder.Writer.PdfWriter().GenerateBytes(document));
    }
}
