# Components, templates, and themes

Reusable components compose through `IContainer` and remain independent of the writer, coordinates, infrastructure services, and the mutable theme implementation. A component must be stateless: it may hold immutable configuration, but it must not retain the supplied container, document-specific state, or mutable render state between calls. Treat every input model as immutable for the full composition and generation operation.

```csharp
public sealed record Address(string Name, string City);

public sealed class AddressComponent : IPdfComponent<Address>
{
    public void Compose(IContainer container, Address model)
    {
        container.Padding("Compact").Column(column =>
        {
            column.Spacing("Compact");
            column.Item().Text(model.Name);
            column.Item().Text(model.City);
        });
    }
}
```

Typed templates create a fresh `PdfDocument` for every call. Configuration methods such as `Theme`, `Page`, `Column`, and `Grid` return `void`; call them as statements rather than chaining them.

```csharp
public sealed record Invoice(string Number, Address Customer);

public sealed class InvoiceTemplate : PdfTemplate<Invoice>
{
    private readonly AddressComponent _address = new();

    public override void Compose(IDocumentDescriptor document, Invoice model)
    {
        document.Theme(theme =>
        {
            theme.Color("Primary", "#163A5F");
            theme.Color("Surface", "#F5F7FA");
            theme.TextStyle("Heading1", style =>
                style.FontSize(24).Bold().Color("Primary"));
            theme.Spacing("Section", 16);
            theme.Spacing("Compact", 6);
            theme.Page(page => page.BackgroundColor = "Surface");
        });

        document.Page(page =>
        {
            page.Header().Padding("Compact").Text("Invoice").Style("Heading1");
            page.Content().Column(column =>
            {
                column.Spacing("Section");
                column.Item().Text(model.Number).Style("Heading1");
                column.Item().Component(_address, model.Customer);
                column.Item().Grid(grid =>
                {
                    grid.Columns(2);
                    grid.RowSpacing("Compact");
                    grid.ColumnSpacing("Compact");
                    grid.Item().Text("Status");
                    grid.Item().Text("Open");
                });
            });
            page.Footer().Margin("Compact").Text("Page {page} of {pages}");
        });
    }
}
```

`PdfTemplate<TModel>.Create`, `GenerateBytes`, `Generate`, and `Save` all create a new document. One template instance can therefore be reused concurrently when its component fields are stateless and its models are immutable. Templates and components must not perform database or HTTP calls, use a service locator, or read/write static mutable state during composition; collect that data before calling the template.

Theme ownership is document-scoped. `DocumentThemeBuilder` is available only while configuring the document, and components resolve names through container overloads such as `Padding("Section")`, `Margin("Section")`, `column.Spacing("Section")`, and grid spacing methods. Components never receive the mutable theme object. Missing spacing names throw a `KeyNotFoundException` naming the token. Named colours resolve for text styles, container backgrounds and borders, tables and table cells, table headers, headers, footers, and page backgrounds.

Each page receives a clone of the document theme. Changes to a page clone cannot leak to another page or document, including pages created by automatic pagination.

Component composition is synchronous. An `IContainer` is valid only during the `Compose` callback that supplied it: do not cache it, use it after the callback returns, transfer it to another document, or access it from another thread. Reuse the component object instead and let each call receive a fresh container.

Composition detects recursive component cycles and a nesting safety limit. Failures throw `PdfComponentCompositionException`; `ComponentPath` records the nested component type path, and non-composition failures remain available through `InnerException`. This path is preserved when components create nested columns, rows, grids, stacks, or layers.

The complete runnable implementation is in [the Invoice sample](../samples/Invoice/Program.cs).
