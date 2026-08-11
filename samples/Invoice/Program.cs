using System.Globalization;
using PdfBuilder.Document;

var invoice = Invoice.CreateDemo();
var template = new InvoiceTemplate();
template.Save(Path.Combine(AppContext.BaseDirectory, "invoice.pdf"), invoice, CancellationToken.None);

public sealed record Address(string Line1, string City, string PostalCode, string Country);

public sealed record Company(string Name, string Email, Address Address);

public sealed record Customer(string Name, Address Address);

public sealed record InvoiceLine(
    string Description,
    string Details,
    decimal Quantity,
    decimal UnitPrice,
    bool IsContinuationExample);

public sealed record Invoice(
    string Number,
    DateOnly IssuedOn,
    DateOnly DueOn,
    Company Seller,
    Customer Customer,
    IReadOnlyList<InvoiceLine> Lines,
    decimal TaxRate)
{
    public decimal Subtotal => Lines.Sum(line => line.Quantity * line.UnitPrice);
    public decimal Tax => Subtotal * TaxRate;
    public decimal Total => Subtotal + Tax;

    public static Invoice CreateDemo()
    {
        var lines = Enumerable.Range(1, 28)
            .Select(index => new InvoiceLine(
                $"Service line {index}",
                index == 10
                    ? string.Join(" ", Enumerable.Repeat("This controlled continuation describes the delivered work, evidence, acceptance result, and follow-up action.", 36))
                    : $"Professional services delivered for work package {index}.",
                1m,
                12.5m,
                index == 10))
            .ToList()
            .AsReadOnly();
        var seller = new Company(
            "Northwind Services",
            "support@example.test",
            new Address("1 Harbour Road", "Cape Town", "8001", "South Africa"));
        var customer = new Customer(
            "Contoso Operations",
            new Address("42 Market Street", "Johannesburg", "2001", "South Africa"));

        return new Invoice(
            "INV-1001",
            new DateOnly(2026, 8, 11),
            new DateOnly(2026, 9, 10),
            seller,
            customer,
            lines,
            .15m);
    }
}

public sealed class InvoiceTemplate : PdfTemplate<Invoice>
{
    private readonly SellerHeaderComponent _sellerHeader = new();
    private readonly CustomerAddressComponent _customerAddress = new();
    private readonly InvoiceTotalsComponent _invoiceTotals = new();

    public override void Compose(IDocumentDescriptor document, Invoice model)
    {
        document.Metadata(metadata =>
        {
            metadata.Title = $"Invoice {model.Number}";
            metadata.Author = model.Seller.Name;
        });
        document.Theme(theme =>
        {
            theme.Color("Primary", "#163A5F");
            theme.Color("Surface", "#F5F7FA");
            theme.Color("Border", "#A9B7C6");
            theme.Color("HeaderBackground", "#E8EEF7");
            theme.Color("PageBackground", "#FFFFFF");
            theme.TextStyle("Heading1", style => style.FontSize(22).Bold().Color("Primary"));
            theme.TextStyle("Heading2", style => style.FontSize(12).Bold().Color("Primary"));
            theme.TextStyle("TableHeader", style => style.Bold().Color("Primary"));
            theme.TextStyle("Total", style => style.FontSize(12).Bold().Color("Primary"));
            theme.Spacing("Section", 16);
            theme.Spacing("Compact", 2);
            theme.Page(page => { page.Margin = 36; page.BackgroundColor = "PageBackground"; });
        });
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Header().Component(_sellerHeader, model.Seller);
            page.Footer().PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}");
            var content = page.Content();
            content.Column(column =>
            {
                column.Spacing("Section");
                column.Item().Text($"Invoice {model.Number}").Style("Heading1");
                column.Item().Text($"Issued {model.IssuedOn:dd MMM yyyy} - Due {model.DueOn:dd MMM yyyy}");
                column.Item().Component(_customerAddress, model.Customer);
            });
            content.Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.RelativeColumn(1, minWidth: 180, maxWidth: 360);
                    columns.FixedColumn(60, minWidth: 55, maxWidth: 65);
                    columns.FixedColumn(80, minWidth: 75, maxWidth: 85);
                });
                table.CellPadding(5);
                table.Border(0.75f, "Border");
                table.HeaderBackground("HeaderBackground");
                table.RepeatHeaders();
                table.RepeatFooters(TableFooterRepeatMode.EveryPage);
                table.Header(header =>
                {
                    header.Cell().Padding(5, 12, 5, 5).Text("Description").Style("TableHeader");
                    header.Cell().Padding(5, 12, 5, 5).AlignRight().Text("Qty").Style("TableHeader");
                    header.Cell().Padding(5, 12, 5, 5).AlignRight().Text("Amount").Style("TableHeader");
                });
                foreach (var line in model.Lines)
                {
                    table.Row(row =>
                    {
                        row.AllowSplit(line.IsContinuationExample);
                        row.Cell().Padding(5, 12, 5, 5).Column(description =>
                        {
                            description.Spacing("Compact");
                            description.Item().RichText(paragraph =>
                            {
                                paragraph.Span(line.Description).Bold();
                                paragraph.Span(" - ");
                                paragraph.Span(line.Details);
                            });
                            if (!line.IsContinuationExample)
                                description.Item().Text($"Unit price: {InvoiceFormatting.Currency(line.UnitPrice)}");
                        });
                        row.Cell().Padding(5, 12, 5, 5).AlignRight().Text(line.Quantity, "N0");
                        row.Cell().Padding(5, 12, 5, 5).AlignRight().Text(InvoiceFormatting.Currency(line.Quantity * line.UnitPrice));
                    });
                }
                table.Footer(footer =>
                {
                    footer.Background("Surface");
                    footer.Cell().Padding(5, 12, 5, 5).ColumnSpan(2).Text($"Invoice {model.Number} - {model.Lines.Count:N0} lines").Style("TableHeader");
                    footer.Cell().Padding(5, 12, 5, 5).AlignRight().Text(InvoiceFormatting.Currency(model.Total)).Style("TableHeader");
                });
            });
            content.Column(column =>
            {
                column.Item().Component(_invoiceTotals, model);
                column.Item().Padding("Compact").Background("Surface").Border(0.5f, "Border")
                    .Text("Terms: payment is due within 30 days.");
            });
        });
    }
}

public sealed class SellerHeaderComponent : IPdfComponent<Company>
{
    public void Compose(IContainer container, Company model)
    {
        container.Padding("Compact").Background("HeaderBackground").Border(0.5f, "Border").Row(row =>
        {
            row.RelativeItem().Text(model.Name).Style("Heading2");
            row.RelativeItem().AlignRight().Text(model.Email);
        });
    }
}

public sealed class CustomerAddressComponent : IPdfComponent<Customer>
{
    public void Compose(IContainer container, Customer model)
    {
        container.Padding("Compact").Background("Surface").Column(column =>
        {
            column.Spacing("Compact");
            column.Item().Text("Bill to").Style("Heading2");
            column.Item().Text(model.Name);
            column.Item().Text(model.Address.Line1);
            column.Item().Text($"{model.Address.City}, {model.Address.PostalCode}");
            column.Item().Text(model.Address.Country);
        });
    }
}

public sealed class InvoiceTotalsComponent : IPdfComponent<Invoice>
{
    public void Compose(IContainer container, Invoice model)
    {
        container.AlignRight().Padding("Compact").Border(0.75f, "Border").Column(column =>
        {
            column.Spacing("Compact");
            column.Item().AlignRight().Text($"Subtotal: {InvoiceFormatting.Currency(model.Subtotal)}");
            column.Item().AlignRight().Text($"Tax: {InvoiceFormatting.Currency(model.Tax)}");
            column.Item().AlignRight().Text($"Total: {InvoiceFormatting.Currency(model.Total)}").Style("Total");
        });
    }
}

public static class InvoiceFormatting
{
    public static string Currency(decimal value) => $"USD {value.ToString("N2", CultureInfo.InvariantCulture)}";
}
