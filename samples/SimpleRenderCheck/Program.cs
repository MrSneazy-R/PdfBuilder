using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Writer;

namespace SimpleRenderCheck;

internal static class Program
{
    public static void Main(string[] args)
    {
        var doc = BuildDocument();
        var writer = new PdfWriter();

        var sw = Stopwatch.StartNew();
        byte[] bytes = writer.GenerateBytes(doc);
        sw.Stop();

        double sizeKb = bytes.Length / 1024.0;
        double elapsedMs = sw.Elapsed.TotalMilliseconds;

        var outputPath = Path.Combine(AppContext.BaseDirectory, "simple.pdf");
        File.WriteAllBytes(outputPath, bytes);

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "Simple PDF generated: {0:0.0} KB in {1:0.00} ms -> {2}",
            sizeKb,
            elapsedMs,
            outputPath));
    }

    private static PdfDocument BuildDocument()
    {
        var doc = new PdfDocument();
        var page = doc.AddPage();

        var text = new TextElement
        {
            X = page.MarginLeft,
            Y = page.Height - page.MarginTop - 40,
            Text = "Alpha Beta Gamma Delta Epsilon",
            FontFamily = "Helvetica",
            FontSize = 12f
        };

        page.Elements.Add(text);
        return doc;
    }
}
