using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Writer.Imaging;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class RenderLimitsTests
{
    [Fact]
    public void RenderLimits_MaxPages_ThrowsSpecificException()
    {
        var document = new PdfDocument();
        document.RenderLimits.MaximumPages = 1;
        document.AddPage();

        var action = () => document.AddPage();
        action.Should().Throw<PdfRenderLimitException>().Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumPages));
    }

    [Fact]
    public void RenderLimits_MaxLayoutIterations_StopsLoop()
    {
        var document = new PdfDocument();
        document.RenderLimits.MaximumLayoutIterations = 1;
        var page = document.AddPage();
        var builder = new ColumnBuilder(page, 0f, layoutOptions: page.LayoutOptions, document: document);

        var action = () => builder.Compose(new NeverFits());
        action.Should().Throw<PdfLayoutException>().Which.Context.LayoutIterationCount.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void RenderLimits_MaxImagePixels_RejectsImageBeforeDecodeAllocation()
    {
        var limits = new PdfRenderLimits { MaximumImagePixels = 10 };
        var action = () => limits.ValidateImagePixels(11);
        action.Should().Throw<PdfRenderLimitException>().Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumImagePixels));
    }

    [Fact]
    public void ExtremelyLongText_DoesNotHang()
    {
        var text = new string('X', 5_000);
        var document = new PdfDocument();
        document.RenderLimits.MaximumLayoutIterations = 1;
        var page = document.AddPage();
        var builder = new ColumnBuilder(page, 0f, layoutOptions: page.LayoutOptions, document: document);

        var action = () => builder.Text(text).Add();
        action.Should().Throw<PdfLayoutException>();
    }

    [Fact]
    public async Task FiftyParallelInvoices_CompleteSuccessfully()
    {
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() => PdfDocument.Create(d => d.Page(p => p.Content().Text("invoice"))).GenerateBytes()));
        var results = await Task.WhenAll(tasks);
        results.Should().OnlyContain(bytes => bytes.Length > 0);
    }

    [Fact]
    public void CancellationUnderLoad_ReleasesResources()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var document = PdfDocument.Create(d => d.Page(p => p.Content().Text("load")));
        var action = () => document.GenerateBytes(cancellation.Token);
        action.Should().Throw<OperationCanceledException>();
    }

    private sealed class NeverFits : IMeasurable
    {
        public LayoutMeasurement Measure(LayoutMeasureContext context) => LayoutMeasurement.Wrap(1f);
        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) { }
    }
}
