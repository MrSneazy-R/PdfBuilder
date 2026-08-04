# Components, templates, and theming

Components compose only through `IContainer`; they do not receive PDF elements,
coordinates, writers, service locators, or a database connection.

```csharp
public sealed class TitleComponent(string title) : IPdfComponent
{
    public void Compose(IContainer container) => container.Text(title).Bold();
}

public sealed class InvoiceTemplate : PdfTemplate<Invoice>
{
    public override void Compose(IDocumentDescriptor document, Invoice invoice)
    {
        document.Theme(theme =>
        {
            theme.Color("Primary", "#163A5F");
            theme.TextStyle("Heading1", style => style.FontSize(24).Bold().Color("Primary"));
            theme.SetSpacing("Section", 16);
        });

        document.Page(page => page.Content().Column(column =>
        {
            column.Item().Component(new TitleComponent(invoice.Number));
            column.Item().Text(invoice.Number).Style("Heading1");
        }));
    }
}
```

Theme instances belong to a single `PdfDocument`. Reuse components and templates
freely when they keep their input model immutable and store no document-specific
mutable static state. `ConfigureDefaultTextStyle` configures the exposed
`DocumentTheme.DefaultTextStyle` value without replacing it.
