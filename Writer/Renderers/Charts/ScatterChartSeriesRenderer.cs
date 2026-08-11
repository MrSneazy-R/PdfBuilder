using System.Text;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal static class ScatterChartSeriesRenderer
{
    public static void Append(StringBuilder content, CanonicalChartModel model, ChartScales scales, PdfRenderContext context)
    {
        foreach (CanonicalScatterSeries series in model.Series.OfType<CanonicalScatterSeries>())
        {
            PdfBuilder.Models.PdfColor color = series.Color ?? model.Palette[model.Series.IndexOf(series) % model.Palette.Count];
            LinearChartScale yScale = series.SecondaryAxis ? scales.SecondaryY ?? scales.PrimaryY : scales.PrimaryY;
            foreach (Document.ChartPoint point in series.Points)
            {
                float x = scales.X.Map(point.X);
                float y = yScale.Map(point.Y);
                ChartDrawing.Marker(content, series.Marker, x, y, series.MarkerSize, series.MarkerFill ?? color, color);
                if (series.ShowLabels)
                    ChartLabelRenderer.Draw(content, context, series.LabelFormatter(point), model.FontFamily, model.FontSize, model.TextColor, x, y + 5f, centered: true);
            }
        }
    }
}
