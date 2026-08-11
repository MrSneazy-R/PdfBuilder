using PdfBuilder.Document;

ReportModel model = new(
    "Quarterly operations report",
    Enumerable.Range(1, 36)
        .Select(index => new ReportFinding(index, $"Control {index:00}", index % 5 == 0 ? "Review" : "Effective"))
        .ToArray());

PdfDocument report = PdfDocument.Create(document =>
{
    document.Metadata(metadata =>
    {
        metadata.Title = model.Title;
        metadata.Author = "PdfBuilder canonical report sample";
    });
    document.Theme(theme =>
    {
        theme.Color("Brand", "#1E4F8A");
        theme.Color("Panel", "#EAF2FB");
        theme.Spacing("Section", 14);
        theme.TextStyle("ReportTitle", style => style.FontSize(14).Bold().Color("Brand"));
        theme.TextStyle("SectionTitle", style => style.FontSize(16).Bold().Color("Brand"));
    });
    document.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(42);
        page.FirstPageHeader().Text(model.Title).Style("ReportTitle");
        page.ContinuationHeader().Row(row =>
        {
            row.RelativeItem().Text(model.Title).Bold().Color("Brand");
            row.AutoItem().Text("Continued").Italic();
        });
        page.Footer().PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}")
            .AlignRight();

        page.Content().Column(column =>
        {
            column.Spacing("Section");
            column.Item().Text("Table of contents").Style("SectionTitle");
            column.Item().TableOfContents(options => options.PageNumberFormat("page {0}"));
            column.Item().Row(row =>
            {
                row.RelativeItem().InternalLink("Jump to detailed findings", "findings").Underline();
                row.RelativeItem().ExternalLink("Project website", "https://example.com/pdfbuilder").Underline();
            });

            column.Item().Section("summary", "Executive summary", section =>
            {
                section.Text("Executive summary").Style("SectionTitle");
                section.Padding("Section").Background("Panel")
                    .Text("The report demonstrates final page context, forward navigation, outlines, and page-aware repeated content without raw elements.");
            }, options => options.StartOnNewPage());

            column.Item().Section("findings", "Detailed findings", section =>
            {
                section.Text("Detailed findings").Style("SectionTitle");
                section.Table(table =>
                {
                    table.Columns(columns =>
                    {
                        columns.ConstantColumn(52);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });
                    table.Header(row =>
                    {
                        row.Cell().Text("No.").Bold();
                        row.Cell().Text("Control").Bold();
                        row.Cell().Text("Status").Bold();
                    });
                    foreach (ReportFinding finding in model.Findings)
                    {
                        table.Row(row =>
                        {
                            row.Cell().Text(finding.Number, "00");
                            row.Cell().Text(finding.Control);
                            row.Cell().Text(finding.Status);
                        });
                    }
                    table.HeaderBackground("Panel");
                    table.Border(0.5f, "Brand");
                });
            }, options => options.StartOnNewPage());

            column.Item().LastPageOnly().Padding("Section").Border(1, "Brand")
                .Text("End of canonical report").Bold().AlignCenter();
        });
    });
});

report.GenerationOptions.Deterministic = true;
report.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;
report.GenerationOptions.ModificationTime = DateTimeOffset.UnixEpoch;
report.GenerationOptions.DocumentIdSeed = "phase-one-canonical-report";

string outputPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(AppContext.BaseDirectory, "canonical-report.pdf");
report.Save(outputPath);
Console.WriteLine($"Generated {report.Pages.Count} pages at {outputPath}");

internal sealed record ReportModel(string Title, IReadOnlyList<ReportFinding> Findings);
internal sealed record ReportFinding(int Number, string Control, string Status);
