using System.Diagnostics;
using System.Globalization;
using PdfBuilder.Document;
using PdfBuilder.Models;

namespace TablePerfSample;

internal sealed record PerfScenario(string Name, int Rows, int Columns, string[] TextPool);

internal static class Program
{
    private static readonly PerfScenario[] Scenarios =
    {
        new("latin-text", 12, 4, new[]
        {
            "office affine efficient",
            "Invoice reference 2042",
            "Operations summary",
            "Customer address block"
        }),
        new("latin-wrapping", 12, 4, new[]
        {
            "Deterministic analytics overview",
            "Compact summary text",
            "Quarterly reference TEST-2042",
            "Long synthetic description for wrapping"
        })
    };

    public static void Main()
    {
        string outputRoot = Path.Combine(AppContext.BaseDirectory, "output");
        Directory.CreateDirectory(outputRoot);

        Console.WriteLine("== PdfBuilder canonical HarfBuzz table performance sample ==");
        foreach (PerfScenario scenario in Scenarios)
        {
            PdfDocument document = CreateTableDocument(scenario);
            var stopwatch = Stopwatch.StartNew();
            byte[] bytes = document.GenerateBytes();
            stopwatch.Stop();

            string targetPath = Path.Combine(outputRoot, $"{scenario.Name}.pdf");
            File.WriteAllBytes(targetPath, bytes);
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-18} -> {1,6:0.0} KB | {2,6:0.2} ms | output: {3}",
                scenario.Name,
                bytes.Length / 1024d,
                stopwatch.Elapsed.TotalMilliseconds,
                targetPath));
        }
    }

    private static PdfDocument CreateTableDocument(PerfScenario scenario) => PdfDocument.Create(document =>
    {
        document.OutputPreset(PdfOutputPreset.Deterministic);
        document.Page(page =>
        {
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(9));
            page.Content().Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.Columns(columns =>
                    {
                        for (int index = 0; index < scenario.Columns; index++)
                            columns.RelativeColumn();
                    });
                    table.CellPadding(2);
                    table.Border(0.4f, "#8EA5B8");
                    table.RowBanding(banding =>
                    {
                        banding.Step(1);
                        banding.Fill("#FFFFFF");
                        banding.Fill("#F0F8FF");
                    });
                    for (int rowIndex = 0; rowIndex < scenario.Rows; rowIndex++)
                    {
                        int capturedRow = rowIndex;
                        table.Row(row =>
                        {
                            for (int columnIndex = 0; columnIndex < scenario.Columns; columnIndex++)
                            {
                                string text = scenario.TextPool[(capturedRow + columnIndex) % scenario.TextPool.Length];
                                ITableCellDescriptor cell = row.Cell().AlignMiddle().Wrap();
                                if (columnIndex % 3 == 1)
                                    cell.AlignCenter();
                                else if (columnIndex % 3 == 2)
                                    cell.AlignRight();
                                cell.Text(text).LineHeight(1.2f).Direction(TextDirection.Automatic);
                            }
                        });
                    }
                });
            });
        });
    });
}
