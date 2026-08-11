using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class CanonicalTableContainerCellTests
{
    [Fact]
    public void TableCell_RichTextAndReusableComponent_UseNormalContentPipeline()
    {
        var document = CreateDocument(cell =>
        {
            cell.RichText(paragraph =>
            {
                paragraph.DefaultStyle().FontSize(10);
                paragraph.Span("Rich ").Bold();
                paragraph.Span("cell").Italic().Underline();
            });
            cell.Component(new CellComponent());
        });

        var bytes = document.GenerateBytes();
        var elements = PdfContentHelper.FlattenElements(document.Pages.SelectMany(page => page.Elements)).ToList();

        string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes)).Should().Contain("Rich").And.Contain("component content");
        elements.OfType<RichTextElement>().Should().NotBeEmpty();
        elements.OfType<TextElement>().Should().Contain(text => text.Text == "component content");
    }

    [Fact]
    public void TableCell_ImageSvgAndBarcode_RetainResourcesAndVectorContent()
    {
        byte[] image = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestLogo.png"));
        var document = CreateDocument(cell => cell.Column(column =>
        {
            column.Spacing(2);
            column.Item().Image(image, 18, 18);
            column.Item().Svg("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='8'><rect width='20' height='8' fill='#336699'/></svg>", 20, 8);
            column.Item().Barcode("PR26", BarcodeKind.Code128, 0.5f, 1);
        }));

        byte[] bytes = document.GenerateBytes();
        string pdf = Encoding.ASCII.GetString(bytes);
        var elements = PdfContentHelper.FlattenElements(document.Pages.SelectMany(page => page.Elements)).ToList();

        pdf.Should().Contain("/Subtype /Image");
        elements.OfType<ImageElement>().Should().NotBeEmpty();
        elements.OfType<SvgElement>().Should().NotBeEmpty();
        elements.OfType<BarcodeElement>().Should().NotBeEmpty();
    }

    [Fact]
    public void TableCell_NestedLayoutsThemeAndCellDecoration_ArePreserved()
    {
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Theme(theme =>
            {
                theme.Color("Ink", "#163A5F");
                theme.Color("Surface", "#E8EEF7");
                theme.Color("Rule", "#AABBCC");
                theme.TextStyle("CellText", style => style.Bold().Color("Ink"));
                theme.Spacing("Cell", 7);
            });
            descriptor.Page(page => page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Row(row => row.Cell()
                    .Padding("Cell")
                    .Background("Surface")
                    .BorderLeft(2, "Rule")
                    .BorderBottom(1, "Rule")
                    .CornerRadius(3)
                    .AlignMiddle()
                    .Grid(grid =>
                    {
                        grid.Columns(2);
                        grid.RowSpacing(2);
                        grid.ColumnSpacing(3);
                        grid.Item().Text("themed nested").Style("CellText");
                        grid.Item().Stack(stack =>
                        {
                            stack.Item().Text("stack base");
                            stack.Item().Layer(layer => layer.Content().Text("layer content"));
                        });
                    }));
            }));
        });

        document.GenerateBytes().Should().NotBeEmpty();
        var tableCell = document.Pages.SelectMany(page => page.Elements).OfType<TableElement>().Single().Rows.Single().Cells.Single();
        var elements = PdfContentHelper.FlattenElements(document.Pages.SelectMany(page => page.Elements)).ToList();

        tableCell.Padding.Should().Be(7);
        tableCell.BackgroundColor.Should().Be(System.Drawing.Color.FromArgb(0xE8, 0xEE, 0xF7));
        tableCell.BorderWidthLeft.Should().Be(2);
        tableCell.BorderColorLeft.Should().Be(System.Drawing.Color.FromArgb(0xAA, 0xBB, 0xCC));
        tableCell.VerticalAlign.Should().Be(VerticalAlign.Middle);
        elements.OfType<TextElement>().Single(text => text.Text == "themed nested").Color.Should().Be("#163A5F");
    }

    [Fact]
    public void TableCell_ContentIsClippedToCellBounds()
    {
        var document = CreateDocument(cell => cell.Padding(4).Height(30).Text("clipped cell content"));
        document.OutputOptions.ReadableContentStreams = true;

        byte[] bytes = document.GenerateBytes();
        string content = string.Join("\n", PdfContentHelper.ExtractStreams(bytes));

        content.Should().Contain(" re W n");
        PdfContentHelper.FlattenElements(document.Pages.SelectMany(page => page.Elements)).OfType<TextElement>()
            .Should().Contain(text => text.Text == "clipped cell content");
    }

    [Fact]
    public void TableCell_RepeatedComponentHeaderAndThousandRows_RemainCompatible()
    {
        var header = new HeaderComponent();
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Header(row => row.Cell().Component(header));
                for (int index = 0; index < 1_000; index++)
                    table.Row(row => row.Cell().Text($"container-row-{index}"));
            });
        }));

        byte[] bytes = document.GenerateBytes();
        var blocks = PdfTextExtractor.ExtractTextBlocks(bytes);

        document.Pages.Count.Should().BeGreaterThan(1);
        blocks.Count(block => block == "component header").Should().Be(document.Pages.Count);
        for (int index = 0; index < 1_000; index++)
            blocks.Count(block => block == $"container-row-{index}").Should().Be(1);
    }

    private static PdfDocument CreateDocument(Action<ITableCellDescriptor> configure)
        => PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Table(table =>
        {
            table.CellPadding(3);
            table.Columns(columns => columns.RelativeColumn());
            table.Row(row => configure(row.Cell()));
        })));

    private sealed class CellComponent : IPdfComponent
    {
        public void Compose(IContainer container) => container.Text("component content").Color("#223344");
    }

    private sealed class HeaderComponent : IPdfComponent
    {
        public void Compose(IContainer container) => container.Text("component header").Bold();
    }
}
