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
            column.Spacing(8);
            column.Item().Text("PdfBuilder").FontSize(22).Bold();
            column.Item().Text("Canonical composition API");
        });
    });
});

pdf.Save("hello.pdf");
