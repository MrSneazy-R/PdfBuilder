using PdfBuilder.Document;
using PdfBuilder.Models;
using PdfBuilder.Writer;

internal static class BenchmarkScenarios
{
    private static readonly IReadOnlyDictionary<string, ScenarioDefinition> Definitions =
        new[]
        {
            Pdf("minimal-document", BenchmarkDocuments.Minimal),
            Pdf("invoice", () => BenchmarkDocuments.Invoice(20)),
            Pdf("multi-page-invoice", () => BenchmarkDocuments.Invoice(200)),
            Pdf("table-100-rows", () => BenchmarkDocuments.Table(100), exactOutputGate: false, allocationGate: false, rowCount: 100),
            Pdf("table-500-rows", () => BenchmarkDocuments.Table(500), exactOutputGate: false, allocationGate: false, rowCount: 500),
            Pdf("table-1000-rows", () => BenchmarkDocuments.Table(1_000), exactOutputGate: false, allocationGate: false, rowCount: 1_000),
            Pdf("table-2000-rows", () => BenchmarkDocuments.Table(2_000), exactOutputGate: false, allocationGate: false, rowCount: 2_000),
            Pdf("table-5000-rows", () => BenchmarkDocuments.Table(5_000), exactOutputGate: false, allocationGate: false, rowCount: 5_000),
            Pdf("500-page-report", BenchmarkDocuments.FiveHundredPages, exactOutputGate: false, allocationGate: false),
            Pdf("multilingual-shaping", BenchmarkDocuments.Multilingual, exactOutputGate: false, allocationGate: false),
            Pdf("image-heavy-report", BenchmarkDocuments.ImageHeavy),
            Pdf("core-charts", BenchmarkDocuments.CoreCharts),
            Pdf("advanced-charts", BenchmarkDocuments.AdvancedCharts),
            new("previews", RunPreviews, ExactOutputGate: false, AllocationGate: false),
            new("streaming", RunStreaming, ExactOutputGate: true, AllocationGate: true),
            new("concurrent-invoices", RunConcurrentInvoices, ExactOutputGate: false, AllocationGate: false),
            new("cancellation", RunCancellation, ExactOutputGate: false, AllocationGate: false),
            Pdf("resource-deduplication", BenchmarkDocuments.ResourceDeduplication),
            new("deterministic-generation", RunDeterministicGeneration, ExactOutputGate: true, AllocationGate: true)
        }.ToDictionary(definition => definition.Name, StringComparer.Ordinal);

    public static IReadOnlyCollection<ScenarioDefinition> All => Definitions.Values.ToArray();

    public static ScenarioResult Run(string name) =>
        Definitions.TryGetValue(name, out ScenarioDefinition? definition)
            ? definition.Execute()
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown benchmark scenario.");

    private static ScenarioDefinition Pdf(
        string name,
        Func<PdfDocument> factory,
        bool exactOutputGate = true,
        bool allocationGate = true,
        int? rowCount = null) =>
        new(
            name,
            () => Generate(factory),
            exactOutputGate,
            allocationGate,
            rowCount,
            rowCount.HasValue ? checked((rowCount.Value + 1L) * 3L) : null);

    private static ScenarioResult Generate(Func<PdfDocument> factory)
    {
        var document = factory();
        var writer = new PdfWriter();
        byte[] output = writer.GenerateBytes(document);
        return FromMetrics(output.LongLength, writer.LastGenerationMetrics, deterministic: true);
    }

    private static ScenarioResult RunStreaming()
    {
        var document = BenchmarkDocuments.Invoice(200);
        var writer = new PdfWriter();
        using var stream = new MemoryStream();
        writer.GenerateStream(document, stream);
        return FromMetrics(stream.Length, writer.LastGenerationMetrics, deterministic: true);
    }

    private static ScenarioResult RunPreviews()
    {
        var document = BenchmarkDocuments.Minimal();
        IReadOnlyList<PdfPreviewPage> previews = document.GeneratePreviewImages(72, new[] { 1 }, CancellationToken.None);
        return new ScenarioResult(
            previews.Sum(preview => (long)preview.ImageData.Length),
            previews.Count,
            0,
            0,
            0,
            0,
            0,
            0,
            true);
    }

    private static ScenarioResult RunConcurrentInvoices()
    {
        const int count = 8;
        var results = new ScenarioResult[count];
        Parallel.For(0, count, index => results[index] = Generate(() => BenchmarkDocuments.Invoice(40)));
        return new ScenarioResult(
            results.Sum(result => result.OutputBytes),
            results.Sum(result => result.Pages),
            results.Max(result => result.MaximumRetainedStreams),
            results.Sum(result => result.ResourceCount),
            results.Sum(result => result.FontResources),
            results.Sum(result => result.ImageReferences),
            results.Sum(result => result.UniqueImageResources),
            results.Sum(result => result.ImageDeduplicationHits),
            results.All(result => result.Deterministic),
            results.Sum(result => result.TableMeasurementCount),
            results.Sum(result => result.TableRowMeasurementCount),
            results.Sum(result => result.TableCellMeasurementCount),
            results.Sum(result => result.TableCloneCount),
            results.Sum(result => result.TableRowCloneCount),
            results.Sum(result => result.ContentFactoryInvocationCount),
            results.Sum(result => result.TableCellDrawBufferAllocationCount));
    }

    private static ScenarioResult RunCancellation()
    {
        var document = BenchmarkDocuments.FiveHundredPages();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            document.GenerateBytes(cancellation.Token);
            throw new InvalidOperationException("Cancelled generation completed unexpectedly.");
        }
        catch (OperationCanceledException)
        {
            return new ScenarioResult(0, 0, 0, 0, 0, 0, 0, 0, true);
        }
    }

    private static ScenarioResult RunDeterministicGeneration()
    {
        ScenarioResult firstMetrics;
        byte[] first;
        byte[] second;
        {
            var document = BenchmarkDocuments.Invoice(80);
            var writer = new PdfWriter();
            first = writer.GenerateBytes(document);
            firstMetrics = FromMetrics(first.LongLength, writer.LastGenerationMetrics, deterministic: true);
        }
        {
            var document = BenchmarkDocuments.Invoice(80);
            second = document.GenerateBytes();
        }

        if (!first.AsSpan().SequenceEqual(second))
            throw new InvalidOperationException("Deterministic benchmark documents produced different byte sequences.");
        return firstMetrics;
    }

    private static ScenarioResult FromMetrics(long outputBytes, PdfGenerationMetrics? metrics, bool deterministic)
    {
        if (metrics == null)
            throw new InvalidOperationException("Generation did not publish metrics.");
        int fontResources = metrics.BaseFontResources + metrics.EmbeddedFontResources;
        return new ScenarioResult(
            outputBytes,
            metrics.PagesPlanned,
            metrics.MaximumRetainedPageContentStreams,
            fontResources + metrics.UniqueImageResources + metrics.ExtGStateResources,
            fontResources,
            metrics.ImageReferences,
            metrics.UniqueImageResources,
            Math.Max(0, metrics.ImageReferences - metrics.UniqueImageResources),
            deterministic,
            metrics.TableMeasurementCount,
            metrics.TableRowMeasurementCount,
            metrics.TableCellMeasurementCount,
            metrics.TableCloneCount,
            metrics.TableRowCloneCount,
            metrics.ContentFactoryInvocationCount,
            metrics.TableCellDrawBufferAllocationCount);
    }
}

internal sealed record ScenarioDefinition(
    string Name,
    Func<ScenarioResult> Execute,
    bool ExactOutputGate,
    bool AllocationGate,
    int? RowCount = null,
    long? ExpectedContentFactoryInvocationCount = null);

internal sealed record ScenarioResult(
    long OutputBytes,
    int Pages,
    int MaximumRetainedStreams,
    int ResourceCount,
    int FontResources,
    int ImageReferences,
    int UniqueImageResources,
    int ImageDeduplicationHits,
    bool Deterministic,
    long TableMeasurementCount = 0,
    long TableRowMeasurementCount = 0,
    long TableCellMeasurementCount = 0,
    long TableCloneCount = 0,
    long TableRowCloneCount = 0,
    long ContentFactoryInvocationCount = 0,
    long TableCellDrawBufferAllocationCount = 0);
