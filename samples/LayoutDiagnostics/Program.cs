using PdfBuilder.Document;

var pdf = PdfDocument.Create(document =>
{
    document.Diagnostics(diagnostics => diagnostics.EnableLayoutTrace = true);
    document.Page(page =>
    {
        page.Margin(40);
        page.Content().Column(column =>
        {
            column.Item().Text("Layout diagnostics").FontSize(20).Bold();
            column.Item().DebugLabel("InvoiceTotals").Padding(12).Background("#EFF6FF").Text("Trace labels identify this component without exposing business content.");
        });
    });
});

pdf.Save(Path.Combine(AppContext.BaseDirectory, "layout-diagnostics.pdf"));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "layout-trace.json"), pdf.LayoutTrace.ToJson());
var preview = pdf.GeneratePreviewImages(96, new[] { 1 }).Single();
File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "layout-preview.png"), preview.ImageData);
