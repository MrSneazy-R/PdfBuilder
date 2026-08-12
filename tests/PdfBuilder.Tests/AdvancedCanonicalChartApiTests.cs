using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class AdvancedCanonicalChartApiTests
{
    [Fact]
    public void AdvancedChartTypes_RenderThroughTypedCanonicalDescriptors()
    {
        var document = PdfDocument.Create(document =>
        {
            Add(document, chart => chart.Bubble("Bubble", [new(1, 2, 4, "A"), new(2, 3, 8, "B")]).Radius(2, 9));
            Add(document, chart => chart.Waterfall("Waterfall", [new(12), new(-4), new(8, true)]).Colors(Rgb(20, 150, 80), Rgb(190, 60, 60), Rgb(80, 100, 190)));
            Add(document, chart => { chart.Categories("A", "B", "C"); chart.Radar("Radar", [2, 5, 3]).Fill(new PdfColor(40, 120, 200, 90)); });
            Add(document, chart => chart.Funnel("Funnel", [new("Lead", 100), new("Qualified", 60), new("Won", 20)]).Gap(3));
            Add(document, chart => { chart.Categories("Plan", "Build"); chart.Gantt("Gantt", [new(0, 0, 4, "Plan"), new(1, 3, 9, "Build")]).Geometry(2, 0.7f); });
            Add(document, chart => { chart.Categories("Mon", "Tue"); chart.Candlestick("OHLC", [new(0, 10, 14, 8, 12), new(1, 12, 15, 9, 10)]).Colors(Rgb(40, 150, 70), Rgb(190, 60, 60), Rgb(30, 30, 30)); });
            Add(document, chart => chart.Bullet("KPI", 72, 80, [new(0, 50, Rgb(220, 220, 220)), new(50, 100, Rgb(180, 210, 180))]).TargetStyle(Rgb(10, 10, 10), 1.5f));
            Add(document, chart => { chart.Categories("A", "B", "C"); chart.Pareto("Pareto", [40, 25, 10]).CumulativeStyle(Rgb(190, 50, 50), 1.5f); });
            Add(document, chart => { chart.Categories("A", "B", "C"); chart.RangeArea("Band", [new(0, 2, 5), new(1, 3, 7), new(2, 1, 6)]).Smooth(); });
            Add(document, chart => { chart.Categories("A", "B"); chart.ErrorBars("Errors", [new(0, 10, 2, 3), new(1, 15, 1, 2)]).CapWidth(5); });
            Add(document, chart => chart.Histogram("Histogram", [1, 1.5f, 2, 2.2f, 4, 4.1f]).Bins(3));
            Add(document, chart => { chart.Categories("A", "B"); chart.BoxPlot("Box", [new(0, new float[] { 1, 2, 3, 4 }), new(1, new float[] { 2, 4, 5, 8 })]).BoxWidth(0.6f); });
            Add(document, chart => chart.Heatmap("Heatmap", new float[,] { { 1, 2 }, { 3, 4 } }).ColorScale(value => value < 2.5f ? Rgb(80, 130, 220) : Rgb(220, 90, 70)));
        });
        document.OutputOptions.ReadableContentStreams = true;

        byte[] pdf = document.GenerateBytes();

        pdf.Should().StartWith(Encoding.ASCII.GetBytes("%PDF-"));
        Encoding.ASCII.GetString(pdf).Should().Contain("/Count 13");
    }

    [Fact]
    public void CoreAndAdvancedSeries_InOneChart_FailExplicitly()
    {
        Action action = () => PdfDocument.Create(document => document.Page(page => page.Content().Chart(chart =>
        {
            chart.Line("Core", [1, 2, 3]);
            chart.Histogram("Advanced", [1, 2, 3]);
        })));

        action.Should().Throw<InvalidOperationException>().WithMessage("*cannot be mixed*");
    }

    private static void Add(IDocumentDescriptor document, Action<IChartDescriptor> configure)
        => document.Page(page => page.Content().Chart(chart =>
        {
            chart.Size(420, 220);
            chart.Legend();
            configure(chart);
        }));

    private static PdfColor Rgb(byte red, byte green, byte blue) => PdfColor.Rgb(red, green, blue);
}
