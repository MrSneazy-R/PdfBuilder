using System.Globalization;
using PdfBuilder.Document;

var model = Invoice.CreateDemo();
var document = InvoiceTemplate.Create(model);
document.Save(Path.Combine(AppContext.BaseDirectory, "invoice.pdf"));

public sealed record Invoice(string Number, string Customer, IReadOnlyList<InvoiceLine> Lines, decimal TaxRate)
{
    public decimal Subtotal => Lines.Sum(line => line.Quantity * line.UnitPrice);
    public decimal Tax => Subtotal * TaxRate;
    public decimal Total => Subtotal + Tax;
    public static Invoice CreateDemo() => new("INV-1001", "Contoso Operations", Enumerable.Range(1, 80).Select(index => new InvoiceLine($"Service line {index}", 1m, 12.5m)).ToArray(), .15m);
}
public sealed record InvoiceLine(string Description, decimal Quantity, decimal UnitPrice);

public static class InvoiceTemplate
{
    public static PdfDocument Create(Invoice model) => PdfDocument.Create(document =>
    {
        document.Metadata(metadata => { metadata.Title = $"Invoice {model.Number}"; metadata.Author = "PdfBuilder sample"; });
        document.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(36);
            page.Header().DebugLabel("CompanyHeader").Text("Northwind Services").Bold();
            page.Footer().Text("Page {page} of {pages}");
            page.Content().Column(column =>
            {
                column.Item().Text($"Invoice {model.Number}").FontSize(22).Bold();
                column.Item().Row(row => { row.ConstantItem(24).Text("◆").FontSize(18); row.RelativeItem().Text("Northwind Services — support@example.test"); });
                column.Item().Text($"Bill to: {model.Customer}");
                column.Item().Table(table =>
                {
                    table.Columns(columns => { columns.RelativeColumn(); columns.ConstantColumn(60); columns.ConstantColumn(80); });
                    table.Header(header => { header.Cell().Text("Description").Bold(); header.Cell().AlignRight().Text("Qty").Bold(); header.Cell().AlignRight().Text("Amount").Bold(); });
                    foreach (var line in model.Lines) table.Row(row => { row.Cell().Text(line.Description); row.Cell().AlignRight().Text(line.Quantity, "N0"); row.Cell().AlignRight().Text(line.Quantity * line.UnitPrice, "C2"); });
                });
                column.Item().AlignRight().Text($"Subtotal: {model.Subtotal.ToString("C2", CultureInfo.CurrentCulture)}");
                column.Item().AlignRight().Text($"Tax: {model.Tax.ToString("C2", CultureInfo.CurrentCulture)}");
                column.Item().AlignRight().Text($"Total: {model.Total.ToString("C2", CultureInfo.CurrentCulture)}").Bold();
                column.Item().Padding(8).Background("#F5F5F5").Text("Terms: payment is due within 30 days.");
            });
        });
    });
}
