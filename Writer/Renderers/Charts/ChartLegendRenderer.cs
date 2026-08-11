using System.Text;
using PdfBuilder.Document;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal static class ChartLegendRenderer
{
    public static void Append(StringBuilder content, CanonicalChartModel model, ChartLayout layout, PdfRenderContext context)
    {
        if (model.Legend == ChartLegendPosition.Hidden || model.Series.Count == 0) return;

        float x = model.Legend switch
        {
            ChartLegendPosition.TopLeft => layout.Plot.Left + 8f,
            ChartLegendPosition.TopRight => Math.Max(layout.Plot.Left + 8f, layout.Plot.Right - 110f),
            _ => layout.Legend.Left
        };
        float y = model.Legend == ChartLegendPosition.Below ? layout.Legend.Top - 12f : layout.Plot.Top - 13f;

        for (int index = 0; index < model.Series.Count; index++)
        {
            CanonicalChartSeries series = model.Series[index];
            PdfBuilder.Models.PdfColor color = series.Color ?? model.Palette[index % model.Palette.Count];
            ChartDrawing.Rectangle(content, x, y - 1f, 8f, 8f, color);
            ChartLabelRenderer.Draw(content, context, series.Name, model.FontFamily, model.FontSize, model.TextColor, x + 12f, y);
            y -= 13f;
        }
    }
}
