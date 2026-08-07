# Components, templates, and themes

Reusable components compose into the same coordinate-free content pipeline as document content. Components should be stateless, must not retain the supplied container, and should treat input models as immutable.

```csharp
public sealed class AddressComponent : IPdfComponent<Address>
{
    public void Compose(IContainer container, Address model)
    {
        container.Column(column =>
        {
            column.Text(model.Name);
            column.Text(model.City);
        });
    }
}
```

Typed templates create a fresh `PdfDocument` for every call, allowing one template instance to render concurrently:

```csharp
public sealed class InvoiceTemplate : PdfTemplate<Invoice>
{
    public override void Compose(IDocumentDescriptor document, Invoice model)
    {
        document
            .Theme(theme =>
            {
                theme.Color("Primary", "#163A5F");
                theme.TextStyle("Heading1", style =>
                    style.FontSize(24).Bold().Color("Primary"));
                theme.Spacing("Section", 16);
            })
            .Compose(composer => composer.Page(page =>
                page.Content(content =>
                {
                    content.Text("Invoice", "Heading1");
                    content.Component(new AddressComponent(), model.Customer);
                })));
    }
}
```

Themes are cloned into pages. Named styles and colors therefore cannot leak to another document, and component cycles fail with a `PdfComponentCompositionException` containing the component path.
