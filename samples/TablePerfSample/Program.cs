using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using TableModels = PdfBuilder.Elements.Table;

namespace TablePerfSample;

internal sealed record PerfScenario(string Name, int Rows, int Columns, TableModels.TextWrapMode WrapMode, bool IncludeRotation, string[] TextPool);

internal static class Program
{
    private const int Iterations = 1;

    private static readonly PerfScenario[] Scenarios =
    {
        new(
            "latin-arabic",
            Rows: 12,
            Columns: 4,
            WrapMode: TableModels.TextWrapMode.Wrap,
            IncludeRotation: false,
            TextPool: new[]
            {
                "Analytics data flow",
                "مرحبا بالعالم",
                "Español con acentos",
                "Καλημέρα κόσμε"
            }),
        new(
            "cjk-hybrid",
            Rows: 12,
            Columns: 4,
            WrapMode: TableModels.TextWrapMode.Wrap,
            IncludeRotation: true,
            TextPool: new[]
            {
                "数据指标概览",
                "표 데이터",
                "データ分析",
                "Analytics data flow"
            })
    };

    public static void Main(string[] args)
    {
        var outputRoot = Path.Combine(AppContext.BaseDirectory, "output");
        Directory.CreateDirectory(outputRoot);

        Console.WriteLine("== PdfBuilder Harfbuzz table perf sample ==");
        foreach (var scenario in Scenarios)
        {
            double totalMs = 0;
            byte[]? finalPdf = null;

            for (int i = 0; i < Iterations; i++)
            {
                var doc = CreateTableDocument(scenario);
                var writer = new PdfWriter();
                var sw = Stopwatch.StartNew();
                var bytes = writer.GenerateBytes(doc);
                sw.Stop();

                totalMs += sw.Elapsed.TotalMilliseconds;
                if (i == Iterations - 1)
                    finalPdf = bytes;
            }

            if (finalPdf == null)
                continue;

            var targetPath = Path.Combine(outputRoot, $"{scenario.Name}.pdf");
            File.WriteAllBytes(targetPath, finalPdf);

            double sizeKb = finalPdf.Length / 1024.0;
            double avgMs = totalMs / Iterations;

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,-18} -> {1,6:0.0} KB | avg {2,6:0.2} ms over {3} runs | output: {4}",
                    scenario.Name,
                    sizeKb,
                    avgMs,
                    Iterations,
                    targetPath));
        }

        Console.WriteLine();
        Console.WriteLine("Inspect the generated PDFs under the output folder to compare size and glyph coverage.");
    }

    private static PdfDocument CreateTableDocument(PerfScenario scenario)
    {
        var doc = new PdfDocument();
        var page = doc.AddPage();
        float usableWidth = page.Width - page.MarginLeft - page.MarginRight;

        var table = new TableElement(page.MarginLeft, page.Height - page.MarginTop - 40)
        {
            TableWidth = usableWidth,
            ColumnWidths = Enumerable.Repeat(usableWidth / scenario.Columns, scenario.Columns).ToList(),
            CellPadding = 4,
            AutoSizeColumns = false,
            CaptionText = $"Scenario: {scenario.Name}"
        };

        var pool = scenario.TextPool;

        for (int r = 0; r < scenario.Rows; r++)
        {
            var row = new TableRow();
            for (int c = 0; c < scenario.Columns; c++)
            {
                var text = pool[(r + c) % pool.Length];
                var cell = new TableCell
                {
                    Text = text,
                    Padding = 2,
                    RotationDegrees = scenario.IncludeRotation && c == 0 && r % 10 == 0 ? 90f : 0f,
                    TextStyle = new TableModels.TextStyle
                    {
                        Wrap = scenario.WrapMode,
                        LineHeight = 1.2f,
                        HorizontalAlign = c % 3 == 0 ? HorizontalAlign.Left : c % 3 == 1 ? HorizontalAlign.Center : HorizontalAlign.Right,
                        VerticalAlign = VerticalAlign.Middle
                    }
                };

                if (c % 2 == 0)
                {
                    cell.TextStyle.BackgroundColor = Color.FromArgb(240, 248, 255);
                }

                row.Cells.Add(cell);
            }

            table.Rows.Add(row);
        }

        page.AddElement(table);
        return doc;
    }
}
