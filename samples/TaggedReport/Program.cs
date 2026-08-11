using PdfBuilder.Document;

string output = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(AppContext.BaseDirectory, "tagged-report.pdf");

PdfDocument report = PdfDocument.Create(document =>
{
    document.Metadata(metadata => metadata.Title = "Tagged management report");
    document.Tagged(tagged => tagged.Language("en-ZA"));
    document.Page(page =>
    {
        page.Margin(42);
        page.Header().Text("Management report");
        page.Content().Semantic(PdfSemanticRole.Section).Column(column =>
        {
            column.Spacing(10);
            column.Item().Heading(1).Text("Quarterly performance").FontSize(22).Bold();
            column.Item().Text("This sample demonstrates semantic structure without claiming PDF/UA conformance.");
            column.Item().Semantic(PdfSemanticRole.Figure)
                .AlternativeText("A rising line representing quarterly revenue")
                .Canvas(240, 80, canvas => canvas.Line(10, 70, 230, 10));
            column.Item().Semantic(PdfSemanticRole.Caption).Text("Figure 1 - Revenue trend").Italic();
            column.Item().ExternalLink("Supporting information", "https://example.com");
            column.Item().Decorative().Text("Decorative classification rule");
        });
        page.Footer().PageText("Page {page} of {pages}");
    });
});

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
report.Save(output);
Console.WriteLine(output);
