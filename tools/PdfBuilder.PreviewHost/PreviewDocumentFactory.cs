using PdfBuilder.Document;
using PdfBuilder.Models;

internal static class PreviewDocumentFactory
{
    public static PdfDocument Create(bool visualGuides = true)
    {
        return PdfDocument.Create(document =>
        {
            document.Metadata(metadata =>
            {
                metadata.Title = "PdfBuilder local diagnostics preview";
                metadata.Author = "PdfBuilder PreviewHost";
            });
            document.OutputPreset(PdfOutputPreset.Debug);
            document.Diagnostics(options =>
            {
                options.EnableLayoutTrace = true;
                options.DrawBoundingBoxes = visualGuides;
                options.ShowFlowGuides = visualGuides;
                options.EnableProfiler = true;
            });
            document.Theme(theme =>
            {
                theme.Color("Brand", "#164E63");
                theme.Color("Panel", "#ECFEFF");
                theme.Spacing("Section", 12);
                theme.TextStyle("Title", style => style.FontSize(20).Bold().Color("Brand"));
                theme.TextStyle("Heading", style => style.FontSize(13).Bold().Color("Brand"));
            });
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(46);
                page.Header().DebugLabel("RepeatedHeader")
                    .Text("PdfBuilder diagnostics preview").Style("Heading");
                page.Footer().DebugLabel("RepeatedFooter")
                    .PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}").AlignRight();
                page.Content().DebugLabel("ReportRoot").Column(column =>
                {
                    column.Spacing("Section");
                    column.Item().DebugLabel("ReportTitle").Text("Local preview and diagnostics host").Style("Title");
                    column.Item().DebugLabel("SummaryPanel").Padding("Section").Background("Panel").Border(1, "Brand")
                        .Text("Use the controls beside the preview to inspect final pages, trace paths, timing, margins, and flow guides.");

                    for (int index = 1; index <= 42; index++)
                    {
                        int item = index;
                        column.Item().DebugLabel($"Finding[{item}]").KeepTogether().Padding(8).BorderBottom(0.5f, "#94A3B8")
                            .Row(row =>
                            {
                                row.ConstantItem(54).Text(item.ToString("00")).Bold().Color("Brand");
                                row.RelativeItem().Text($"Operational finding {item}: deterministic layout diagnostics remain document scoped and contain no telemetry.");
                            });
                    }
                });
            });
        });
    }
}
