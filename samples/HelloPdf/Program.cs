using PdfBuilder.Document;

var pdf = PdfDocument.Create(document =>
{
    document.Metadata(metadata =>
    {
        metadata.Title = "Hello PdfBuilder";
        metadata.Author = "PdfBuilder";
    });

    document.Page(page =>
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
                grid.Columns(2);
                grid.RowSpacing(6);
                grid.ColumnSpacing(8);
                grid.Item().Background("#F5F5F5").Padding(6).Text("Grid one");
                grid.Item().Background("#F5F5F5").Padding(6).Text("Grid two");
            });
            column.Item().Row(row =>
            {
                row.ConstantItem(80).Text("Fixed");
                row.RelativeItem().Text("Relative");
                row.AutoItem().Text("Auto");
            });
        });
    });
});

pdf.Save("hello.pdf");
