using PdfBuilder.Document;
var app = WebApplication.CreateBuilder(args).Build();
app.MapGet("/invoice", (HttpResponse response, CancellationToken cancellationToken) =>
{
    var renderer = new InvoiceRenderer();
    response.ContentType = "application/pdf";
    response.Headers.ContentDisposition = "inline; filename=invoice.pdf";
    renderer.Generate(response.Body, cancellationToken);
});
app.Run();
sealed class InvoiceRenderer
{
    public void Generate(Stream destination, CancellationToken cancellationToken)
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Text("PDF streamed from ASP.NET Core")));
        document.Generate(destination, cancellationToken);
    }
}
