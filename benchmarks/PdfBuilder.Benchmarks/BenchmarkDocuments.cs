using System.Drawing;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using static PdfBuilder.Elements.ChartElement;

internal static class BenchmarkDocuments
{
    private static readonly byte[] SharedPng = LoadEmbeddedLogo();

    public static PdfDocument Minimal() => Create(document =>
        document.Page(page => page.Content().Text("PdfBuilder benchmark")));

    public static PdfDocument Invoice(int rows) => Create(document => document.Page(page =>
    {
        page.Margin(36);
        page.Content().Column(column =>
        {
            column.Spacing(8);
            column.Item().Text(rows > 40 ? "Multi-page invoice" : "Invoice").FontSize(18).Bold();
            column.Item().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });
                table.RepeatHeaders();
                table.Header(header =>
                {
                    header.Cell().Text("Description").Bold();
                    header.Cell().Text("Quantity").Bold();
                    header.Cell().Text("Amount").Bold();
                });
                for (int index = 1; index <= rows; index++)
                {
                    table.Row(row =>
                    {
                        row.Cell().Text($"Synthetic service line {index:0000}");
                        row.Cell().Text((index % 7 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                        row.Cell().Text($"USD {index * 3.75m:N2}");
                    });
                }
            });
        });
    }));

    public static PdfDocument Table(int rows)
    {
        if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));

        return Create(document => document.Page(page =>
        {
            page.Margin(36);
            page.Content().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                });
                table.RepeatHeaders();
                table.Header(header =>
                {
                    header.Cell().Text("Description");
                    header.Cell().Text("Quantity");
                    header.Cell().Text("Amount");
                });

                for (int index = 1; index <= rows; index++)
                {
                    table.Row(row =>
                    {
                        row.Cell().Text($"Service line {index:00000}");
                        row.Cell().Text((index % 7 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                        row.Cell().Text($"USD {index * 3.75m:N2}");
                    });
                }
            });
        }), enableTableLayoutCounters: true);
    }

    public static PdfDocument FiveHundredPages()
    {
        var document = CreateRaw();
        for (int pageNumber = 1; pageNumber <= 500; pageNumber++)
        {
            var page = document.AddPage();
            page.AddElement(new TextElement($"Synthetic report page {pageNumber:000}", 45, 740));
        }
        return document;
    }

    public static PdfDocument Multilingual() => Create(document => document.Page(page => page.Content().Column(column =>
    {
        column.Item().Text("office affine café Ångström").FallbackFonts("Noto Sans", "Segoe UI", "Arial");
        column.Item().Text("تقرير تشغيلي آمن").Direction(TextDirection.RightToLeft).FallbackFonts("Noto Sans Arabic", "Segoe UI", "Arial");
        column.Item().Text("דוח בדיקה TEST-2042").Direction(TextDirection.Automatic).FallbackFonts("Noto Sans Hebrew", "Segoe UI", "Arial");
        column.Item().Text("合成業務報告書 日本語 한국어").FallbackFonts("Noto Sans CJK SC", "Microsoft YaHei", "Yu Gothic", "Malgun Gothic");
    })));

    public static PdfDocument ImageHeavy()
    {
        var document = CreateRaw();
        for (int pageNumber = 0; pageNumber < 4; pageNumber++)
        {
            var page = document.AddPage();
            for (int index = 0; index < 6; index++)
                page.AddElement(new ImageElement(SharedPng, 48, 730 - index * 110, 500, 90) { ImageId = "shared-pattern" });
        }
        return document;
    }

    public static PdfDocument CoreCharts() => Create(document => document.Page(page => page.Content().Column(column =>
    {
        column.Item().Chart(chart =>
        {
            chart.Size(500, 220);
            chart.Title("Core chart benchmark");
            chart.Categories("Q1", "Q2", "Q3", "Q4");
            chart.GroupedBars("Revenue", new[] { 20f, 35f, 42f, 51f });
            chart.Line("Quality", new[] { 75f, 82f, 88f, 94f }).Markers();
        });
        column.Item().Chart(chart =>
        {
            chart.Size(500, 190);
            chart.Donut("Mix", new[] { new ChartValue("A", 50), new ChartValue("B", 30), new ChartValue("C", 20) });
        });
    })));

    public static PdfDocument AdvancedCharts()
    {
        var document = new PdfDocument();
        document.GenerationOptions.Deterministic = true;
        document.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.ModificationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.DocumentIdSeed = "pdfbuilder-benchmark";
        var page = document.AddPage();
        var chart = new ChartElement(45, 730) { Width = 510, Height = 300, Title = "Advanced chart benchmark", ShowLegend = true };
        chart.XAxis.Categories.AddRange(new[] { "A", "B", "C", "D" });
        var bubble = new BubbleSeries { Name = "Bubble" };
        bubble.Points.Add(new BubblePoint { X = 0, Y = 12, Size = 8, Category = "A" });
        bubble.Points.Add(new BubblePoint { X = 1, Y = 28, Size = 18, Category = "B" });
        var waterfall = new WaterfallSeries { Name = "Waterfall" };
        waterfall.Steps.Add((0, 25, false));
        waterfall.Steps.Add((1, -8, false));
        waterfall.Steps.Add((2, 12, false));
        waterfall.Steps.Add((3, 29, true));
        var range = new RangeAreaSeries { Name = "Range" };
        range.Points.Add(new RangePoint { CategoryIndex = 0, Low = 5, High = 18 });
        range.Points.Add(new RangePoint { CategoryIndex = 1, Low = 9, High = 24 });
        range.Points.Add(new RangePoint { CategoryIndex = 2, Low = 12, High = 31 });
        chart.Series.Add(bubble);
        chart.Series.Add(waterfall);
        chart.Series.Add(range);
        page.AddElement(chart);
        return document;
    }

    public static PdfDocument ResourceDeduplication()
    {
        var document = CreateRaw();
        for (int pageNumber = 0; pageNumber < 40; pageNumber++)
        {
            var page = document.AddPage();
            page.AddElement(new ImageElement(SharedPng, 48, 650, 96, 96) { ImageId = "shared-pattern" });
        }
        return document;
    }

    private static PdfDocument Create(Action<IDocumentDescriptor> compose, bool enableTableLayoutCounters = false) => PdfDocument.Create(document =>
    {
        document.OutputPreset(PdfOutputPreset.Deterministic);
        if (enableTableLayoutCounters)
            document.Diagnostics(options => options.EnableTableLayoutCounters = true);
        compose(document);
    });

    private static PdfDocument CreateRaw()
    {
        var document = new PdfDocument();
        document.GenerationOptions.Deterministic = true;
        document.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.ModificationTime = DateTimeOffset.UnixEpoch;
        document.GenerationOptions.DocumentIdSeed = "pdfbuilder-benchmark";
        return document;
    }

    private static byte[] LoadEmbeddedLogo()
    {
        using Stream stream = typeof(BenchmarkDocuments).Assembly.GetManifestResourceStream("PdfBuilder.Benchmarks.TestLogo.png")
            ?? throw new InvalidOperationException("Embedded benchmark logo was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
