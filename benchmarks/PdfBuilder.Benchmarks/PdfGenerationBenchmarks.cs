using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 3)]
[JsonExporterAttribute.Full]
[MarkdownExporter]
public class PdfGenerationBenchmarks
{
    [Benchmark(Baseline = true)] public long MinimalDocument() => BenchmarkScenarios.Run("minimal-document").OutputBytes;
    [Benchmark] public long Invoice() => BenchmarkScenarios.Run("invoice").OutputBytes;
    [Benchmark] public long MultiPageInvoice() => BenchmarkScenarios.Run("multi-page-invoice").OutputBytes;
    [Benchmark] public long ThousandRowTable() => BenchmarkScenarios.Run("1000-row-table").OutputBytes;
    [Benchmark] public long FiveHundredPageReport() => BenchmarkScenarios.Run("500-page-report").OutputBytes;
    [Benchmark] public long MultilingualShaping() => BenchmarkScenarios.Run("multilingual-shaping").OutputBytes;
    [Benchmark] public long ImageHeavyReport() => BenchmarkScenarios.Run("image-heavy-report").OutputBytes;
    [Benchmark] public long CoreCharts() => BenchmarkScenarios.Run("core-charts").OutputBytes;
    [Benchmark] public long AdvancedCharts() => BenchmarkScenarios.Run("advanced-charts").OutputBytes;
    [Benchmark] public long Previews() => BenchmarkScenarios.Run("previews").OutputBytes;
    [Benchmark] public long Streaming() => BenchmarkScenarios.Run("streaming").OutputBytes;
    [Benchmark] public long ConcurrentInvoices() => BenchmarkScenarios.Run("concurrent-invoices").OutputBytes;
    [Benchmark] public long Cancellation() => BenchmarkScenarios.Run("cancellation").OutputBytes;
    [Benchmark] public long ResourceDeduplication() => BenchmarkScenarios.Run("resource-deduplication").OutputBytes;
    [Benchmark] public long DeterministicGeneration() => BenchmarkScenarios.Run("deterministic-generation").OutputBytes;
}
