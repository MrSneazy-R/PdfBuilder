using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class CanonicalChartApiTests
{
    [Fact]
    public void CoreChartTypes_RenderThroughCanonicalApi()
    {
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Column(column =>
        {
            column.Item().Chart(chart =>
            {
                chart.Size(420, 190);
                chart.Title("Cartesian core");
                chart.Categories("Jan", "Feb", "Mar");
                chart.Legend(ChartLegendPosition.TopRight);
                chart.Line("Line", [12f, 18f, 15f]).Markers(ChartMarkerShape.Circle);
                chart.Area("Area", [8f, 11f, 14f]).Fill(new PdfColor(45, 125, 210, 90));
                chart.GroupedBars("Grouped", [7f, 9f, 6f]);
                chart.StackedBars("Stack A", [3f, 4f, 5f]);
                chart.StackedBars("Stack B", [2f, 2f, 1f]);
                chart.Stacked100Bars("Percent A", [30f, 60f, 25f], "percent");
                chart.Stacked100Bars("Percent B", [70f, 40f, 75f], "percent");
            });
            column.Item().Chart(chart =>
            {
                chart.Size(420, 190);
                chart.Title("Scatter");
                chart.XAxis(axis => { axis.Range(0, 10); axis.Ticks(4); axis.Format(value => $"X{value:0}"); });
                chart.YAxis(axis => axis.Format(value => $"Y{value:0}"));
                chart.SecondaryYAxis(axis => axis.Range(0, 100));
                chart.Scatter("Samples", [new ChartPoint(1, 12), new ChartPoint(5, 35), new ChartPoint(9, 70)]).SecondaryAxis();
            });
            column.Item().Chart(chart =>
            {
                chart.Size(420, 190);
                chart.Pie("Pie", [new ChartValue("A", 60), new ChartValue("B", 40)]).Labels(value => $"{value.Category}:{value.Value:0}");
                chart.Donut("Donut", [new ChartValue("C", 25), new ChartValue("D", 75)]);
            });
        })));
        document.OutputOptions.ReadableContentStreams = true;

        byte[] pdf = document.GenerateBytes();

        pdf.Should().StartWith(Encoding.ASCII.GetBytes("%PDF-"));
        Encoding.ASCII.GetString(pdf).Should().Contain("/Type /Page");
    }

    [Fact]
    public void ThemePalette_ResolvesTokensAndRemainsDocumentScoped()
    {
        byte[] first = GeneratePaletteChart("#112233", "Brand");
        byte[] second = GeneratePaletteChart("#CC3300", "Brand");

        string firstText = Encoding.ASCII.GetString(first);
        string secondText = Encoding.ASCII.GetString(second);
        firstText.Should().Contain("0.067 0.133 0.2 RG");
        secondText.Should().Contain("0.8 0.2 0 RG");
        first.Should().NotEqual(second);
    }

    [Fact]
    public void CoreCharts_AreDeterministicAndSafeForParallelGeneration()
    {
        PdfDocument document = CreateSimpleChart();

        byte[][] results = Enumerable.Range(0, 8)
            .AsParallel()
            .Select(_ => document.GenerateBytes())
            .ToArray();

        results.Should().OnlyContain(bytes => bytes.SequenceEqual(results[0]));
    }

    [Fact]
    public void ChartConfiguration_InvalidValuesFailClearly()
    {
        Action invalidDonut = () => PdfDocument.Create(document => document.Page(page => page.Content().Chart(chart =>
            chart.Donut("Invalid", [new ChartValue("A", 1)], 1f))));
        Action invalidAxis = () => PdfDocument.Create(document => document.Page(page => page.Content().Chart(chart =>
            chart.YAxis(axis => axis.Range(5, 2)))));
        Action malformedThemeColor = () => GeneratePaletteChart("not-a-colour", "Brand");

        invalidDonut.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*inner ratio*");
        invalidAxis.Should().Throw<ArgumentException>().WithMessage("*minimum*");
        malformedThemeColor.Should().Throw<FormatException>().WithMessage("*resolved*");
    }

    private static byte[] GeneratePaletteChart(string color, string token)
    {
        var document = PdfDocument.Create(document =>
        {
            document.Theme(theme => theme.Color(token, color).ChartPalette(token));
            document.Page(page => page.Content().Chart(chart => chart.Line("Series", [1f, 3f, 2f])));
        });
        document.OutputOptions.ReadableContentStreams = true;
        return document.GenerateBytes();
    }

    private static PdfDocument CreateSimpleChart()
    {
        var document = PdfDocument.Create(document => document.Page(page => page.Content().Chart(chart =>
        {
            chart.Categories("A", "B", "C");
            chart.Line("Trend", [1f, 4f, 2f]).Smooth();
            chart.GroupedBars("Bars", [2f, 3f, 5f]);
        })));
        document.OutputOptions.ReadableContentStreams = true;
        return document;
    }
}
