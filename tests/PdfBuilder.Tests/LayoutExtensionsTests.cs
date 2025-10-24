using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests
{
    public class LayoutExtensionsTests
    {
        [Fact]
        public void ShowOnce_AddsContentOnlyFirstTime()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var builder = new ColumnBuilder(page, margin: 0f);
            var collection = new LayoutComponentCollection(builder);

            collection.ShowOnce("welcome", inner => inner.Text("Hello"));
            collection.ShowOnce("welcome", inner => inner.Text("Again"));

            collection.Components.Should().HaveCount(1);
        }

        [Fact]
        public void DelegateComponent_InvokesProvidedCallbacks()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var builder = new ColumnBuilder(page, margin: 0f);
            var collection = new LayoutComponentCollection(builder);
            bool measured = false;
            bool drawn = false;

            collection.Component(
                measure: ctx =>
                {
                    measured = true;
                    return new LayoutMeasurement(0f, 10f, 0f, ctx.Column.Width);
                },
                draw: (ctx, measurement) => drawn = true);

            collection.Components.Should().HaveCount(1);
            var component = collection.Components[0];

            var measureContext = new LayoutMeasureContext(page, builder.GetFlow(), page.LayoutOptions);
            var measurement = component.Measure(measureContext);

            var drawContext = new LayoutDrawContext(page, builder.GetFlow(), builder.GetFlow().X, builder.GetFlow().Y, builder.GetFlow().Width, page.LayoutOptions);
            component.Draw(drawContext, measurement);

            measured.Should().BeTrue();
            drawn.Should().BeTrue();
        }

        [Fact]
        public void ContentComposerBarcode_AddsCanvasComponent()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var builder = new ColumnBuilder(page, margin: 0f);
            var collection = new LayoutComponentCollection(builder);
            var composer = new ContentComposer(collection);

            composer.Barcode("12345", BarcodeKind.Code128);

            collection.Components.Should().HaveCount(1);
            collection.Components[0].Should().BeOfType<CanvasComponent>();
        }

        [Fact]
        public void ContentComposerSvg_AddsCanvasComponent()
        {
            var doc = new PdfDocument();
            var page = doc.AddPage();
            var builder = new ColumnBuilder(page, margin: 0f);
            var collection = new LayoutComponentCollection(builder);
            var composer = new ContentComposer(collection);

            const string svgMarkup = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><rect x='0' y='0' width='10' height='10' fill='#FF0000'/></svg>";
            composer.Svg(100f, 100f, svg => svg.SvgContent = svgMarkup);

            collection.Components.Should().HaveCount(1);
            collection.Components[0].Should().BeOfType<ImageComponent>();
        }
    }
}
