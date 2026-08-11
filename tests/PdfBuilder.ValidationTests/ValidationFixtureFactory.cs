using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PdfBuilder.ValidationTests;

internal static class ValidationFixtureFactory
{
    public static byte[] Generate(string name)
    {
        var document = name switch
        {
            "basic-text" => BasicText(),
            "text-styles" => TextStyles(),
            "simple-table" => SimpleTable(),
            "multi-page-table" => MultiPageTable(),
            "images" => Images(),
            "rich-text" => RichText(),
            "links-and-outline" => LinksAndOutline(),
            "header-footer" => HeaderFooter(),
            "multilingual-latin" => MultilingualLatin(),
            "layout-primitives" => LayoutPrimitives(),
            "canonical-navigation" => CanonicalNavigation(),
            "graphics-primitives" => GraphicsPrimitives(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown validation fixture.")
        };

        document.Metadata.CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        document.Metadata.ModifiedUtc = document.Metadata.CreatedUtc;
        return new PdfWriter().GenerateBytes(document);
    }

    private static PdfDocument BasicText() => CreateSinglePage("Baseline text fixture");

    private static PdfDocument TextStyles()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).Content(column =>
        {
            column.Text("Bold text").Bold().Add();
            column.Text("Italic text").Italic().Add();
            column.Text("Blue text").Color("#1A4F9C").Add();
        });
        return document;
    }

    private static PdfDocument SimpleTable()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).Content(column =>
        {
            var width = page.Width - page.MarginLeft - page.MarginRight;
            column.Table(page.MarginLeft, column.GetCurrentY(), width, 0)
                .Caption("Sanitised inventory")
                .HeaderRow(cell => cell.Text("Item").Bold(), cell => cell.Text("Quantity").Bold())
                .Row(cell => cell.Text("Coffee"), cell => cell.Text("42"))
                .Row(cell => cell.Text("Paper"), cell => cell.Text("12"))
                .Add();
        });
        return document;
    }

    private static PdfDocument MultiPageTable()
    {
        var document = new PdfDocument();
        AddTablePage(document, "Page one item", "1");
        AddTablePage(document, "Page two item", "2");
        return document;
    }

    private static void AddTablePage(PdfDocument document, string item, string quantity)
    {
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).Content(column =>
        {
            var width = page.Width - page.MarginLeft - page.MarginRight;
            column.Table(page.MarginLeft, column.GetCurrentY(), width, 0)
                .HeaderRow(cell => cell.Text("Item").Bold(), cell => cell.Text("Quantity").Bold())
                .Row(cell => cell.Text(item), cell => cell.Text(quantity))
                .Add();
        });
    }

    private static PdfDocument Images()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var png = CreatePng();
        var jpeg = CreateJpeg();
        new PdfPageBuilder(page).Margin(40).Content(column =>
        {
            column.Text("Image fixture").Bold().Add();
            column.Image(png, page.MarginLeft, column.GetCurrentY(), 96, 48).Add();
            column.Image(jpeg, page.MarginLeft, column.GetCurrentY(), 96, 72).Add();
        });
        return document;
    }

    private static PdfDocument RichText()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).Content(column =>
        {
            new RichTextBuilder(column, page.MarginLeft, column.GetCurrentY(), 400)
                .Font("Helvetica", 12)
                .Span("Rich ").Bold().EndSpan()
                .Span("styled ").Italic().EndSpan()
                .Span("text").Color("#1A4F9C").EndSpan()
                .Add();
        });
        return document;
    }

    private static PdfDocument LinksAndOutline()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).Content(column =>
        {
            column.Anchor("introduction").Title("Introduction").Level(1).Add();
            column.Text("Introduction").Bold().Add();
            new RichTextBuilder(column, page.MarginLeft, column.GetCurrentY(), 400)
                .Font("Helvetica", 12)
                .Span("Read ").EndSpan()
                .Span("OpenAI").LinkUrl("https://openai.com").Underline().EndSpan()
                .Span(" or jump ").EndSpan()
                .Span("back").LinkAnchor("introduction").Underline().EndSpan()
                .Add();
        });
        return document;
    }

    private static PdfDocument HeaderFooter()
    {
        var document = CreateSinglePage("Body content");
        document.HeaderFooter.HeaderTemplate = "Validation header";
        document.HeaderFooter.FooterTemplate = "Validation footer";
        return document;
    }

    private static PdfDocument MultilingualLatin() => CreateSinglePage("Résumé en Français; Español para pruebas.");

    private static PdfDocument LayoutPrimitives() => PdfDocument.Create(document => document.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(10));
        page.Content().Column(column =>
        {
            column.Spacing(12);
            column.Item().Text("PdfBuilder").FontSize(22).Bold();
            column.Item().Margin(Units.Millimeters(2)).Padding(12).Background("#EAF3FF").Border(1, "#1E5AA8").CornerRadius(6).Text("Canonical composition API");
            column.Item().Grid(grid =>
            {
                grid.Columns(2); grid.RowSpacing(6); grid.ColumnSpacing(8);
                grid.Item().Background("#F5F5F5").Padding(6).Text("Grid one");
                grid.Item().Background("#F5F5F5").Padding(6).Text("Grid two");
            });
            column.Item().Row(row => { row.ConstantItem(80).Text("Fixed"); row.RelativeItem().Text("Relative"); row.AutoItem().Text("Auto"); });
        });
    }));

    private static PdfDocument CanonicalNavigation() => PdfDocument.Create(document =>
    {
        document.Page(page =>
        {
            page.Content().Text("Navigation contents").Bold();
            page.Content().TableOfContents(options => options.PageNumberFormat("page {0}"));
            page.Content().InternalLink("Jump to details", "details").Underline();
            page.Content().ExternalLink("Project website", "https://example.com/pdfbuilder").Underline();
        });
        document.Page(page => page.Content().Section("introduction", "Introduction", section =>
            section.Text("Introduction body")));
        document.Page(page => page.Content().Section("details", "Details", section =>
            section.Text("Details body"), options => options.Level(2)));
    });

    private static PdfDocument GraphicsPrimitives() => PdfDocument.Create(document =>
    {
        document.Theme(theme => theme
            .Color("Navy", "#17324D")
            .Color("Blue", "#2D7DD2")
            .Color("Sky", "#D9EEFF")
            .Color("Mint", "#B8E0D2")
            .Color("Shadow", "#506070"));
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.Content().Column(column =>
            {
                column.Spacing(14);
                column.Item().Text("Graphics primitives").FontSize(20).Bold().Color("Navy");
                column.Item().Canvas(180, (canvas, size) =>
                {
                    canvas.Layer(CanvasLayer.Background, background =>
                        background.LinearGradient(0, 0, size.Width, size.Height, "Sky", "Mint", angleDegrees: 18, steps: 28));
                    canvas.Layer(CanvasLayer.Content, content =>
                    {
                        content.RectangleShadow(28, 30, 150, 92, "Shadow", offsetX: 4, offsetY: -4, blurRadius: 6, steps: 10);
                        content.FillColor("#FFFFFF").StrokeColor("Navy").LineWidth(1.5f)
                            .Rectangle(28, 30, 150, 92, stroke: true, fill: true);
                        content.State(state => state
                            .ClipRectangle(205, 24, 130, 116)
                            .Translate(270, 82)
                            .Rotate(24)
                            .Scale(1.15f, 0.85f)
                            .FillColor("Blue")
                            .Rectangle(-62, -28, 124, 56, stroke: false, fill: true));
                        content.RadialGradient(size.Width - 72, 84, 42, "#FFFFFF", "Blue", steps: 24);
                        content.StrokeColor("Navy").LineWidth(2)
                            .LinePattern(CanvasLinePattern.Dashed, 7, 4).Line(40, 142, size.Width - 40, 142)
                            .LinePattern(CanvasLinePattern.Dotted, gapLength: 5).Line(40, 18, size.Width - 40, 18);
                    });
                    canvas.Layer(CanvasLayer.Foreground, foreground => foreground
                        .StrokeColor("Navy").LineWidth(1).LinePattern(CanvasLinePattern.Solid)
                        .Rectangle(1, 1, size.Width - 2, size.Height - 2));
                });
                column.Item().DynamicSvg(54, size =>
                    $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {size.Width} {size.Height}'>" +
                    "<defs><linearGradient id='safe' x1='0' x2='1'><stop offset='0' stop-color='#17324D'/><stop offset='1' stop-color='#2D7DD2'/></linearGradient></defs>" +
                    $"<rect x='1' y='1' width='{size.Width - 2}' height='{size.Height - 2}' rx='8' fill='url(#safe)'/>" +
                    "</svg>");
            });
        });
    });

    private static byte[] CreatePng()
    {
        using var image = CreateImage(new Rgba32(26, 79, 156));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] CreateJpeg()
    {
        using var image = CreateImage(new Rgba32(40, 120, 80));
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90 });
        return stream.ToArray();
    }

    private static Image<Rgba32> CreateImage(Rgba32 color)
    {
        var image = new Image<Rgba32>(16, 16);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++) image[x, y] = color;
        return image;
    }

    private static PdfDocument CreateSinglePage(string text)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        new PdfPageBuilder(page).Margin(40).Content(column => column.Text(text).Add());
        return document;
    }
}
