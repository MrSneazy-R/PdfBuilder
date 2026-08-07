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

    [Fact]
    public void CanonicalApi_DecoratedContainer_GeneratesPdf()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content()
            .Margin(Units.Millimeters(2))
            .Padding(12)
            .Background("#EAF3FF")
            .Border(1, "#1E5AA8")
            .CornerRadius(6)
            .Opacity(0.8f)
            .Text("Decorated content")));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void CanonicalApi_RowSupportsConstantRelativeAndAutoItems()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Row(row =>
        {
            row.ConstantItem(60).Text("Fixed");
            row.RelativeItem(2).Text("Relative");
            row.AutoItem().Text("Auto");
        })));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void CanonicalApi_GridStackLayerAndRepeat_GeneratePdf()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Column(column =>
        {
            column.Item().Grid(grid =>
            {
                grid.Columns(2); grid.RowSpacing(4); grid.ColumnSpacing(6);
                grid.Item().Text("One"); grid.Item().Text("Two"); grid.Item().Text("Three");
            });
            column.Item().Stack(stack => { stack.Item().Text("Base"); stack.Item().Text("Overlay"); });
            column.Item().Layer(layer => { layer.Background().Background("#EEEEEE").Text("Background"); layer.Content().Text("Content"); });
            column.Item().Repeat(2, (index, item) => item.Text($"Repeat {index}"));
        })));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void CanonicalApi_PageBreak_CreatesNewPage()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Content().Text("First page");
            page.Content().PageBreak().Text("Second page");
        }));

        Assert.Equal(2, document.Pages.Count);
    }

    [Fact]
    public void CanonicalApi_InvalidDimensionsAndConflicts_ThrowExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Padding(-1).Text("Invalid"))));
        Assert.Throws<InvalidOperationException>(() => PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().MinWidth(50).MaxWidth(20).Text("Conflict"))));
    }

    [Fact]
    public void CanonicalApi_ShowIfAndKeepTogether_ComposeThroughContainer()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Column(column =>
        {
            column.Item().ShowIf(false).Text("Hidden");
            column.Item().KeepTogether().EnsureSpace(20).Text("Visible");
        })));

        Assert.NotEmpty(document.GenerateBytes());
    }

    [Fact]
    public void Units_ConvertExplicitlyToPoints()
    {
        Assert.Equal(72f, Units.Inches(1));
        Assert.Equal(72f, Units.Millimeters(25.4f), 3);
        Assert.Equal(72f, Units.Centimeters(2.54f), 3);
    }
}
