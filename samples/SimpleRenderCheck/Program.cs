using System.Diagnostics;
using System.Globalization;
using PdfBuilder.Document;

namespace SimpleRenderCheck;

internal static class Program
{
    public static void Main()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Margin(40);
            page.Content().Text("Alpha Beta Gamma Delta Epsilon")
                .FontFamily("Helvetica")
                .FontSize(12);
        }));

        var stopwatch = Stopwatch.StartNew();
        byte[] bytes = document.GenerateBytes();
        stopwatch.Stop();

        string outputPath = Path.Combine(AppContext.BaseDirectory, "simple.pdf");
        File.WriteAllBytes(outputPath, bytes);
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "Simple PDF generated: {0:0.0} KB in {1:0.00} ms -> {2}",
            bytes.Length / 1024d,
            stopwatch.Elapsed.TotalMilliseconds,
            outputPath));
    }
}
