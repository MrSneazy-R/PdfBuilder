using System.Globalization;
using PdfBuilder.Document;
using PdfBuilder.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PdfBuilder.ValidationTests;

internal static class ProductionFixtureFactory
{
    public static PdfDocument Invoice() => BusinessTable(
        "Synthetic invoice INV-2026-0042",
        "INVOICE FIXTURE",
        64,
        index => ($"Consulting service {index:000}", $"2026-07-{(index % 28) + 1:00}", Currency(25m + index * 1.25m)));

    public static PdfDocument CreditNote() => BusinessTable(
        "Synthetic credit note CR-2026-0007",
        "CREDIT NOTE FIXTURE",
        18,
        index => ($"Reversal line {index:000}", $"INV-2026-{index:0000}", Currency(-(5m + index))));

    public static PdfDocument CustomerStatement() => BusinessTable(
        "Synthetic customer statement ST-2026-08",
        "CUSTOMER STATEMENT FIXTURE",
        82,
        index => ($"Account transaction {index:000}", $"REF-{index:000000}", Currency((index % 3 == 0 ? -1 : 1) * index * 7.5m)));

    public static PdfDocument FuelTransactions() => BusinessTable(
        "Synthetic fuel transaction report",
        "FUEL TRANSACTION FIXTURE",
        96,
        index => ($"Vehicle TEST-{index % 12:00}", $"Site {(index % 5) + 1}", $"{20 + index % 45:N1} L"));

    public static PdfDocument OperationalReport() => BusinessTable(
        "Synthetic delivery and job report",
        "OPERATIONAL JOB FIXTURE",
        72,
        index => ($"Job JOB-{index:00000}", index % 7 == 0 ? "Requires review" : "Completed", $"Route {(index % 9) + 1}"));

    public static PdfDocument ManagementReport() => Themed("Synthetic management report", "MANAGEMENT REPORT FIXTURE", content =>
    {
        content.Column(column =>
        {
            column.Spacing("Section");
            column.Item().Text("Synthetic management report").Style("Title");
            column.Item().Text("All values are deterministic demonstration data.");
            column.Item().Chart(chart =>
            {
                chart.Size(500, 180);
                chart.Title("Quarterly throughput");
                chart.Categories("Q1", "Q2", "Q3", "Q4");
                chart.Legend(ChartLegendPosition.TopRight);
                chart.GroupedBars("Delivered", new[] { 82f, 96f, 104f, 118f }).Color("Brand");
                chart.Line("Quality", new[] { 91f, 94f, 96f, 98f }).Color("Accent");
            });
            column.Item().Chart(chart =>
            {
                chart.Size(500, 150);
                chart.Title("Synthetic portfolio mix");
                chart.Donut("Mix", new[]
                {
                    new ChartValue("Operations", 52),
                    new ChartValue("Projects", 31),
                    new ChartValue("Support", 17)
                }).Colors("Brand", "Accent", "Muted");
            });
            column.Item().Chart(chart =>
            {
                chart.Size(500, 100);
                chart.Title("Service target");
                chart.Bullet("On-time delivery", 94, 96, new[]
                {
                    new ChartBulletRange(0, 85, PdfColor.Parse("#E8E8E8")),
                    new ChartBulletRange(85, 95, PdfColor.Parse("#D6E4F0")),
                    new ChartBulletRange(95, 100, PdfColor.Parse("#C9E5D1"))
                }).ValueColor(PdfColor.Parse("#2F6B9A"));
            });
        });
    });

    public static PdfDocument MultilingualLatin() => LanguageFixture(
        "Synthetic multilingual Latin report",
        "MULTILINGUAL LATIN FIXTURE",
        "Résumé français; información española; relatório português; zażółć gęślą jaźń.",
        TextDirection.LeftToRight,
        "Noto Sans", "Segoe UI", "Arial");

    public static PdfDocument ArabicRtl() => LanguageFixture(
        "Synthetic Arabic RTL report",
        "ARABIC RTL FIXTURE",
        "تقرير تشغيلي تجريبي آمن يحتوي على بيانات مصطنعة فقط",
        TextDirection.RightToLeft,
        "Noto Sans Arabic", "Segoe UI", "Arial");

    public static PdfDocument HebrewMixed() => LanguageFixture(
        "Synthetic Hebrew mixed-direction report",
        "HEBREW MIXED FIXTURE",
        "דוח בדיקה סינתטי — Reference TEST-2042 — נתונים לדוגמה בלבד",
        TextDirection.Automatic,
        "Noto Sans Hebrew", "Segoe UI", "Arial");

    public static PdfDocument Cjk() => LanguageFixture(
        "Synthetic CJK report",
        "CJK FIXTURE",
        "合成業務報告書。测试数据仅用于验证。운영 보고서 테스트 데이터.",
        TextDirection.LeftToRight,
        "Noto Sans CJK SC", "Microsoft YaHei", "Yu Gothic", "Malgun Gothic");

    public static PdfDocument ImageHeavy()
    {
        ImageSource shared = ImageSource.FromBytes(CreatePatternPng(320, 180, new Rgba32(24, 72, 120), new Rgba32(216, 235, 250))).Preload();
        ImageSource alternate = ImageSource.FromBytes(CreatePatternPng(240, 160, new Rgba32(126, 52, 64), new Rgba32(250, 225, 213))).Preload();
        return Themed("Synthetic image-heavy report", "IMAGE HEAVY FIXTURE", content => content.Column(column =>
        {
            column.Spacing("Compact");
            column.Item().Text("Synthetic image-heavy report").Style("Title");
            for (int index = 0; index < 18; index++)
            {
                ImageSource source = index % 4 == 0 ? alternate : shared;
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Image(source, 112, 63).Contain().MaximumEffectiveDpi(144);
                    row.RelativeItem().Padding("Compact").Text($"Synthetic image record {index + 1:00}; shared sources exercise content-hash deduplication.");
                });
            }
        }));
    }

    public static PdfDocument ThousandRowTable() => Themed("Synthetic 1,000-row table", "THOUSAND ROW FIXTURE", content =>
        content.Table(table =>
        {
            table.Columns(columns =>
            {
                columns.ConstantColumn(52);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });
            table.RepeatHeaders();
            table.Header(header =>
            {
                header.Cell().Text("Row").Style("TableHeader");
                header.Cell().Text("Synthetic item").Style("TableHeader");
                header.Cell().Text("Date").Style("TableHeader");
                header.Cell().AlignRight().Text("Amount").Style("TableHeader");
            });
            for (int index = 1; index <= 1_000; index++)
            {
                int rowNumber = index;
                table.Row(row =>
                {
                    row.Cell().Text(rowNumber, "0000");
                    row.Cell().Text($"Synthetic ledger item {rowNumber:0000}");
                    row.Cell().Text($"2026-{((rowNumber - 1) % 12) + 1:00}-{((rowNumber - 1) % 28) + 1:00}");
                    row.Cell().AlignRight().Text(Currency(rowNumber / 10m));
                });
            }
            table.RowBanding(banding =>
            {
                banding.Step(1);
                banding.Fill("#FFFFFF");
                banding.Fill("Panel");
            });
            table.Border(0.35f, "Border");
            table.HeaderBackground("Header");
        }));

    public static PdfDocument SpannedAndSplitRow() => Themed("Synthetic spanned and split-row table", "SPANNED SPLIT ROW FIXTURE", content =>
        content.Table(table =>
        {
            table.Columns(columns =>
            {
                columns.ConstantColumn(96);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });
            table.RepeatHeaders();
            table.AllowRowSplitting();
            table.Header(header =>
            {
                header.Cell().Text("Group").Style("TableHeader");
                header.Cell().Text("Description").Style("TableHeader");
                header.Cell().Text("Status").Style("TableHeader");
            });
            table.Row(row =>
            {
                row.Cell().RowSpan(2).Text("Synthetic group A").Bold();
                row.Cell().Text("First spanned detail");
                row.Cell().Text("Complete");
            });
            table.Row(row =>
            {
                row.Cell().Position(1).Text("Second spanned detail");
                row.Cell().Position(2).Text("Review");
            });
            table.Row(row =>
            {
                row.AllowSplit();
                row.Cell().ColumnSpan(3).Column(column =>
                {
                    column.Spacing("Compact");
                    column.Item().Text("CONTROLLED SPLIT ROW MARKER").Bold();
                    for (int paragraph = 1; paragraph <= 54; paragraph++)
                        column.Item().Text($"Synthetic continuation paragraph {paragraph:00}: deterministic operational evidence and follow-up notes with no customer data.");
                });
            });
            table.Border(0.5f, "Border");
            table.HeaderBackground("Header");
        }));

    public static PdfDocument Navigation() => PdfDocument.Create(document =>
    {
        ConfigureDocument(document, "Synthetic navigation report");
        document.Page(page =>
        {
            ConfigurePage(page);
            page.Content().Column(column =>
            {
                column.Spacing("Section");
                column.Item().Text("NAVIGATION FIXTURE").Style("Title");
                column.Item().TableOfContents(options => options.PageNumberFormat("page {0}"));
                column.Item().InternalLink("Jump to synthetic detail", "synthetic-detail").Underline();
                column.Item().ExternalLink("Safe example link", "https://example.test/pdfbuilder").Underline();
            });
        });
        document.Page(page =>
        {
            ConfigurePage(page);
            page.Content().Section("synthetic-summary", "Synthetic summary", section => section.Text("SYNTHETIC SUMMARY MARKER"));
        });
        document.Page(page =>
        {
            ConfigurePage(page);
            page.Content().Section("synthetic-detail", "Synthetic detail", section => section.Text("SYNTHETIC DETAIL MARKER"), options => options.Level(2));
        });
    });

    public static PdfDocument PageVariants() => Themed("Synthetic repeated-content report", "PAGE VARIANTS FIXTURE", content =>
    {
        content.Column(column =>
        {
            column.Spacing("Compact");
            column.Item().FirstPageOnly().Text("FIRST PAGE BODY MARKER").Bold();
            for (int index = 1; index <= 150; index++)
                column.Item().Text($"Synthetic repeated-content line {index:000}: predictable content for pagination validation.");
            column.Item().LastPageOnly().Text("LAST PAGE BODY MARKER").Bold();
        });
    }, page =>
    {
        page.FirstPageHeader().Text("FIRST PAGE HEADER MARKER").Bold().FontSize(8).LineHeight(1);
        page.ContinuationHeader().Text("CONTINUATION HEADER MARKER").Bold().FontSize(8).LineHeight(1);
        page.Footer().PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}").AlignRight().FontSize(8).LineHeight(1);
    }, includeDefaultRepeatedContent: false);

    public static PdfDocument ConcurrentBatchSummary() => Themed("Synthetic concurrent batch summary", "CONCURRENT BATCH FIXTURE", content =>
        content.Column(column =>
        {
            column.Item().Text("Concurrent batch generation fixture").Style("Title");
            column.Item().Text("The companion test generates this immutable fixture concurrently and compares deterministic bytes.");
        }));

    public static PdfDocument SerializerEdge() => PdfDocument.Create(document =>
    {
        ConfigureDocument(document, "Metadata (edge) \\ control");
        document.Metadata(metadata =>
        {
            metadata.Author = "Synthetic Åuthor 東京";
            metadata.Subject = "Parentheses () backslash \\ and non-Latin البيانات";
            metadata.Keywords = new string('K', 8_192);
            metadata.Language = "en-ZA";
            metadata.CustomXmp = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><synthetic>serializer-edge</synthetic></x:xmpmeta>";
        });
        document.Generation(generation => generation.DocumentIdentifier = "AABBCCDDEEFF00112233445566778899");
        document.Page(page =>
        {
            ConfigurePage(page);
            page.Content().Column(column =>
            {
                column.Item().Bookmark("serializer-edge", "Unicode outline – 東京 – résumé", 1);
                column.Item().Text("SERIALIZER EDGE FIXTURE").Style("Title");
                column.Item().ExternalLink("Unicode URI", "https://example.test/résumé?q=東京").Underline();
                column.Item().InternalLink("Internal edge link", "serializer-edge").Underline();
            });
        });
    });

    private static PdfDocument BusinessTable(
        string title,
        string marker,
        int rowCount,
        Func<int, (string First, string Second, string Third)> rowFactory) => Themed(title, marker, content =>
        content.Column(column =>
        {
            column.Spacing("Section");
            column.Item().Text(title).Style("Title");
            column.Item().Text("Synthetic organisation - no real customer or operational data.");
            column.Item().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });
                table.RepeatHeaders();
                table.RepeatFooters(TableFooterRepeatMode.EveryPage);
                table.Header(header =>
                {
                    header.Cell().Text("Description").Style("TableHeader");
                    header.Cell().Text("Reference").Style("TableHeader");
                    header.Cell().AlignRight().Text("Value").Style("TableHeader");
                });
                for (int index = 1; index <= rowCount; index++)
                {
                    var values = rowFactory(index);
                    table.Row(row =>
                    {
                        row.Cell().Text(values.First);
                        row.Cell().Text(values.Second);
                        row.Cell().AlignRight().Text(values.Third);
                    });
                }
                table.Footer(footer =>
                {
                    footer.Background("Panel");
                    footer.Cell().ColumnSpan(2).Text($"{rowCount:N0} synthetic rows").Bold();
                    footer.Cell().AlignRight().Text("VALIDATED").Bold();
                });
                table.RowBanding(banding =>
                {
                    banding.Step(1);
                    banding.Fill("#FFFFFF");
                    banding.Fill("Panel");
                });
                table.Border(0.4f, "Border");
                table.HeaderBackground("Header");
            });
        }));

    private static PdfDocument LanguageFixture(string title, string marker, string text, TextDirection direction, params string[] fallbackFonts) =>
        Themed(title, marker, content => content.Column(column =>
        {
            column.Spacing("Section");
            column.Item().Text(title).Style("Title");
            column.Item().Text(text).FontSize(16).LineHeight(1.5f).Direction(direction).FallbackFonts(fallbackFonts);
            column.Item().Text("Synthetic identifier TEST-2042");
        }));

    private static PdfDocument Themed(
        string title,
        string marker,
        Action<IContainer> compose,
        Action<IPageDescriptor>? pageConfigure = null,
        bool includeDefaultRepeatedContent = true) =>
        PdfDocument.Create(document =>
        {
            ConfigureDocument(document, title);
            document.Page(page =>
            {
                ConfigurePage(page, includeDefaultRepeatedContent);
                pageConfigure?.Invoke(page);
                page.Content().Column(column =>
                {
                    column.Spacing("Compact");
                    column.Item().Text(marker).Bold().Color("Brand");
                    compose(column.Item());
                });
            });
        });

    private static void ConfigureDocument(IDocumentDescriptor document, string title)
    {
        document.Metadata(metadata =>
        {
            metadata.Title = title;
            metadata.Author = "PdfBuilder synthetic fixture corpus";
            metadata.Subject = "Sanitised deterministic production validation fixture";
            metadata.Language = "en";
        });
        document.Theme(theme =>
        {
            theme.Color("Brand", "#173B63");
            theme.Color("Accent", "#2F80C1");
            theme.Color("Muted", "#85A7C2");
            theme.Color("Panel", "#F2F6FA");
            theme.Color("Header", "#DDEAF5");
            theme.Color("Border", "#8EA5B8");
            theme.Spacing("Compact", 5);
            theme.Spacing("Section", 14);
            theme.TextStyle("Title", style => style.FontSize(18).Bold().Color("Brand"));
            theme.TextStyle("TableHeader", style => style.Bold().Color("Brand"));
        });
        document.OutputPreset(PdfOutputPreset.Deterministic);
    }

    private static void ConfigurePage(IPageDescriptor page, bool includeDefaultRepeatedContent = true)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(9).FallbackFonts("Noto Sans", "Segoe UI", "Arial"));
        if (includeDefaultRepeatedContent)
        {
            page.Header().Text("Synthetic production fixture").Color("Muted").FontSize(8).LineHeight(1);
            page.Footer().PageText($"Page {PageTextTokens.CurrentPage} of {PageTextTokens.TotalPages}").AlignRight().Color("Muted").FontSize(8).LineHeight(1);
        }
    }

    private static string Currency(decimal value) => $"USD {value.ToString("N2", CultureInfo.InvariantCulture)}";

    private static byte[] CreatePatternPng(int width, int height, Rgba32 foreground, Rgba32 background)
    {
        using var image = new Image<Rgba32>(width, height, background);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (((x / 16) + (y / 16)) % 2 == 0)
                    image[x, y] = foreground;
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
