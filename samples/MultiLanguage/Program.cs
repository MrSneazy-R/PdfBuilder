using PdfBuilder.Document;
var document = PdfDocument.Create(descriptor => descriptor.Page(page => { page.Margin(40); page.Content().Column(column => { column.Item().Text("English: PdfBuilder"); column.Item().Text("Français: Résumé"); column.Item().Text("العربية: مرحبا"); column.Item().Text("日本語: こんにちは"); }); }));
document.Save(Path.Combine(AppContext.BaseDirectory, "multilanguage.pdf"));
