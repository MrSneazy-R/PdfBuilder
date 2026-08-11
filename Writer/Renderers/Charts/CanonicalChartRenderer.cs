using System.Text;
using PdfBuilder.Elements;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal static class CanonicalChartRenderer
{
    public static void Append(StringBuilder content, ChartElement chart, CanonicalChartModel model, PdfRenderContext context)
    {
        ChartLayout layout = ChartLayout.Calculate(chart, model);
        content.Append("q\n0 J 0 j\n");
        if (!string.IsNullOrWhiteSpace(chart.Title))
            ChartLabelRenderer.Draw(content, context, chart.Title, string.IsNullOrWhiteSpace(chart.TitleFont) ? model.FontFamily : chart.TitleFont, chart.TitleSize, model.TextColor, chart.X, layout.TitleY);

        bool hasCartesian = model.Series.Any(series => series is CanonicalLineSeries or CanonicalBarSeries or CanonicalScatterSeries);
        if (hasCartesian)
        {
            ChartScales scales = ChartScales.Create(model, layout.Plot);
            ChartAxesRenderer.Append(content, model, layout, scales, context);
            content.Append($"q {ChartDrawing.Number(layout.Plot.Left)} {ChartDrawing.Number(layout.Plot.Bottom)} {ChartDrawing.Number(layout.Plot.Width)} {ChartDrawing.Number(layout.Plot.Height)} re W n\n");
            BarChartSeriesRenderer.Append(content, model, layout, scales, context);
            LineChartSeriesRenderer.Append(content, model, layout, scales, context);
            ScatterChartSeriesRenderer.Append(content, model, scales, context);
            content.Append("Q\n");
        }

        PieChartSeriesRenderer.Append(content, model, layout, context);
        ChartLegendRenderer.Append(content, model, layout, context);
        content.Append("Q\n");
    }
}
