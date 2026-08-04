using PdfBuilder.Document;
var report = PdfDocument.Create(document => document.Page(page => { page.Margin(40); page.Header().Text("Operations report").Bold(); page.Footer().Text("{page} / {pages}"); page.Content().Column(column => { column.Item().Text("Monthly operations report").FontSize(20).Bold(); foreach (var index in Enumerable.Range(1, 180)) column.Item().Text($"Report observation {index}: normal operating status."); }); }));
report.Save(Path.Combine(AppContext.BaseDirectory, "multi-page-report.pdf"));
