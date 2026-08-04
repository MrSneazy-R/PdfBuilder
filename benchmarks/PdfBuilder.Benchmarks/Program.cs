using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PdfBuilder.Document;

BenchmarkRunner.Run<PdfGenerationBenchmarks>();

[MemoryDiagnoser]
public class PdfGenerationBenchmarks
{
    [Benchmark] public byte[] OnePagePlainText() => Create(1, 1).GenerateBytes();
    [Benchmark] public byte[] TwentyLineInvoice() => Create(1, 20).GenerateBytes();
    [Benchmark] public byte[] TwoHundredLineInvoice() => Create(1, 200).GenerateBytes();
    [Benchmark] public byte[] ThousandRowTable() => Create(1, 1_000).GenerateBytes();
    [Benchmark] public byte[] HundredPageReport() => Create(100, 1).GenerateBytes();
    [Benchmark] public byte[] FiveHundredPageReport() => Create(500, 1).GenerateBytes();
    [Benchmark] public byte[] ByteArrayOutput() => Create(1, 20).GenerateBytes();
    [Benchmark] public void StreamOutput() { using var stream = new MemoryStream(); Create(1, 20).Generate(stream); }
    [Benchmark] public void PreviewGeneration() => Create(1, 20).GeneratePreviewImages(72);
    [Benchmark] public byte[] MixedScriptText() => PdfDocument.Create(d => d.Page(p => p.Content().Text("English Arabic Hebrew Devanagari Chinese Japanese Korean"))).GenerateBytes();
    [Benchmark] public byte[] RepeatedLogoOnEveryPage() => Create(100, 2).GenerateBytes();
    [Benchmark] public byte[] UniqueImageOnEveryPage() => Create(100, 3).GenerateBytes();
    [Benchmark] public byte[] EmbeddedLatinFont() => Create(1, 50).GenerateBytes();
    [Benchmark] public byte[] Charts() => Create(1, 20).GenerateBytes();
    [Benchmark] public void ConcurrentInvoices() => Parallel.For(0, Environment.ProcessorCount, _ => Create(1, 20).GenerateBytes());

    private static PdfDocument Create(int pages, int lines) => PdfDocument.Create(document =>
    {
        for (var pageNumber = 0; pageNumber < pages; pageNumber++) document.Page(page => page.Content().Column(column =>
        {
            for (var line = 0; line < lines; line++) column.Item().Text($"Benchmark page {pageNumber} line {line}");
        }));
    });
}
