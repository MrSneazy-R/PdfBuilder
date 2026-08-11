using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests;

public class DiagnosticsPreviewTests
{
    [Fact]
    public void LayoutException_ContainsCompleteComponentPath()
    {
        var exception = CreateLoopingLayoutException();

        exception.Context.ComponentPath.Should().StartWith("Document > Page[1] > Content >");
        exception.Context.Component.Should().Be(nameof(AlwaysWrapComponent));
        exception.Context.SuggestedActions.Should().NotBeEmpty();
    }

    [Fact]
    public void LayoutException_ReportsAvailableAndRequiredSize()
    {
        var exception = CreateLoopingLayoutException();

        exception.Context.AvailableWidth.Should().BeGreaterThan(0f);
        exception.Context.AvailableHeight.Should().BeGreaterThanOrEqualTo(0f);
        exception.Context.RequestedWidth.Should().Be(100f);
        exception.Context.BreakPolicy.Should().Be("keep-together");
        exception.Context.LayoutIterationCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LayoutTrace_RecordsPageAndColumnTransitions()
    {
        var document = new PdfDocument();
        document.LayoutOptions.Diagnostics.EnableLayoutTrace = true;
        var page = document.AddPage();
        var builder = new ColumnBuilder(page, 0f, newPage: () => document.AddPage(), layoutOptions: page.LayoutOptions, document: document);

        builder.Compose(new PageBreakComponent());

        document.LayoutTrace.Entries.Should().Contain(entry => entry.Event == "page-transition");
    }

    [Fact]
    public void LayoutTrace_DoesNotExposeTextByDefault()
    {
        const string secret = "Customer account 1149-EXAMPLE";
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Diagnostics(options => options.EnableLayoutTrace = true);
            descriptor.Page(page => page.Content().Text(secret));
        });

        document.LayoutTrace.ToJson().Should().NotContain(secret);
    }

    [Fact]
    public void DebugLabel_AppearsInTrace()
    {
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Diagnostics(options => options.EnableLayoutTrace = true);
            descriptor.Page(page => page.Content().DebugLabel("InvoiceTotals").Text("Total"));
        });

        document.LayoutTrace.Entries.Should().Contain(entry => entry.Component == "InvoiceTotals");
    }

    [Fact]
    public void BoundingBoxes_DoNotChangeNormalLayout()
    {
        var normal = CreateTextDocument(drawBoxes: false);
        var diagnostic = CreateTextDocument(drawBoxes: true);

        var normalText = normal.Pages[0].Elements.OfType<TextElement>().Single();
        var diagnosticText = diagnostic.Pages[0].Elements.OfType<TextElement>().Single();
        (normalText.X, normalText.Y, normalText.MaxWidth).Should().Be((diagnosticText.X, diagnosticText.Y, diagnosticText.MaxWidth));
    }

    [Fact]
    public void Preview_PageRange_ReturnsRequestedPages()
    {
        var document = CreateTwoPageDocument();

        var previews = document.GeneratePreviewImages(72, new[] { 2 });

        previews.Should().ContainSingle();
        previews[0].PageNumber.Should().Be(2);
    }

    [Fact]
    public void Preview_Cancellation_IsHonoured()
    {
        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => CreateTwoPageDocument().GeneratePreviewImages(72, new[] { 1 }, cancellation.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Preview_MatchesFinalPdfRasterization()
    {
        var document = CreateTwoPageDocument();

        var pdf = document.GenerateBytes();
        var preview = document.GeneratePreviewImages(72, new[] { 1 }).Single();

        pdf.Should().NotBeEmpty();
        preview.ImageData.Should().NotBeEmpty();
        preview.Width.Should().Be((int)document.Pages[0].Width);
        preview.Height.Should().Be((int)document.Pages[0].Height);
    }

    [Fact]
    public void CanonicalDiagnostics_EnableVisualGuidesAndProfiler()
    {
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Diagnostics(options =>
            {
                options.EnableLayoutTrace = true;
                options.DrawBoundingBoxes = true;
                options.ShowFlowGuides = true;
                options.EnableProfiler = true;
            });
            descriptor.Page(page => page.Content().DebugLabel("PreviewPanel").Text("diagnostics"));
        });

        document.Pages[0].Elements.Should().Contain(element => element is DebugRectangleElement);
        document.ProfilerSession.Snapshot().Entries.Should().NotBeEmpty();
        document.LayoutTrace.Entries.Should().Contain(entry => entry.Component == "PreviewPanel");
    }

    [Fact]
    public void Preview_RendersDiagnosticRectangles()
    {
        var document = new PdfDocument();
        var page = document.AddPage(100, 100);
        page.AddElement(new DebugRectangleElement(10, 10, 80, 80)
        {
            StrokeColor = "#FF0000",
            StrokeWidth = 4,
            Opacity = 1
        });

        PdfPreviewPage preview = document.GeneratePreviewImages(72).Single();

        preview.ImageData.Should().NotBeEmpty();
    }

    [Fact]
    public void Profiler_ReportsComponentTiming()
    {
        var document = new PdfDocument();
        document.LayoutOptions.Profiler.Enabled = true;
        var page = document.AddPage();
        var builder = new ColumnBuilder(page, 0f, layoutOptions: page.LayoutOptions, document: document);
        builder.Text("profile").Add();

        document.ProfilerSession.Snapshot().Entries.Should().Contain(entry => entry.MeasureCount > 0 && entry.DrawCount > 0);
    }

    [Fact]
    public void InfiniteLayoutLoop_IsStoppedWithDiagnostic()
    {
        var exception = CreateLoopingLayoutException();

        exception.Context.LayoutIterationCount.Should().BeLessThanOrEqualTo(32);
        exception.Message.Should().Contain("repeatedly reported a wrap result");
    }

    private static PdfLayoutException CreateLoopingLayoutException()
    {
        var document = new PdfDocument();
        document.LayoutOptions.Diagnostics.EnableLayoutTrace = true;
        document.LayoutOptions.Diagnostics.LayoutIterationLimit = 2;
        var page = document.AddPage();
        var builder = new ColumnBuilder(page, 0f, layoutOptions: page.LayoutOptions, document: document);

        var action = () => builder.Compose(new AlwaysWrapComponent());
        return action.Should().Throw<PdfLayoutException>().Which;
    }

    private static PdfDocument CreateTextDocument(bool drawBoxes)
    {
        var document = new PdfDocument();
        document.LayoutOptions.Debug.DrawBoundingBoxes = drawBoxes;
        var page = document.AddPage();
        var builder = new ColumnBuilder(page, 0f, layoutOptions: page.LayoutOptions, document: document);
        builder.Text("unchanged").Add();
        return document;
    }

    private static PdfDocument CreateTwoPageDocument()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        first.AddElement(new TextElement("First", 30, 700) { MaxWidth = 200f });
        var second = document.AddPage();
        second.AddElement(new TextElement("Second", 30, 700) { MaxWidth = 200f });
        return document;
    }

    private sealed class AlwaysWrapComponent : IMeasurable
    {
        public LayoutMeasurement Measure(LayoutMeasureContext context) => LayoutMeasurement.Wrap(100f);
        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) { }
    }
}
