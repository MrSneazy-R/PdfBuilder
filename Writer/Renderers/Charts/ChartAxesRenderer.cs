using System.Text;
using PdfBuilder.Elements.CanonicalCharts;
using PdfBuilder.Models;

namespace PdfBuilder.Writer.Charts;

internal static class ChartAxesRenderer
{
    private static readonly PdfColor Grid = PdfColor.Parse("#D9DEE7");

    public static void Append(StringBuilder content, CanonicalChartModel model, ChartLayout layout, ChartScales scales, PdfRenderContext context)
    {
        foreach (float tick in scales.PrimaryYTicks)
        {
            float y = scales.PrimaryY.Map(tick);
            ChartDrawing.Line(content, layout.Plot.Left, y, layout.Plot.Right, y, Grid);
            ChartLabelRenderer.Draw(content, context, model.YAxis.Formatter(tick), model.FontFamily, model.FontSize, model.TextColor, layout.Plot.Left - 5f, y - model.FontSize * 0.35f, centered: true);
        }

        ChartDrawing.Line(content, layout.Plot.Left, layout.Plot.Bottom, layout.Plot.Right, layout.Plot.Bottom, model.TextColor);
        ChartDrawing.Line(content, layout.Plot.Left, layout.Plot.Bottom, layout.Plot.Left, layout.Plot.Top, model.TextColor);

        if (scales.NumericX)
        {
            foreach (float tick in scales.XTicks)
                ChartLabelRenderer.Draw(content, context, model.XAxis.Formatter(tick), model.FontFamily, model.FontSize, model.TextColor, scales.X.Map(tick), layout.Plot.Bottom - 13f, centered: true);
        }
        else
        {
            for (int index = 0; index < scales.CategoryCount; index++)
            {
                string label = index < model.Categories.Count ? model.Categories[index] : (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                ChartLabelRenderer.Draw(content, context, label, model.FontFamily, model.FontSize, model.TextColor, scales.CategoryCenter(index, layout.Plot), layout.Plot.Bottom - 13f, centered: true);
            }
        }

        if (scales.SecondaryY is LinearChartScale secondary)
        {
            ChartDrawing.Line(content, layout.Plot.Right, layout.Plot.Bottom, layout.Plot.Right, layout.Plot.Top, model.TextColor);
            CanonicalChartAxis axis = model.SecondaryYAxis ?? new CanonicalChartAxis();
            foreach (float tick in scales.SecondaryYTicks)
                ChartLabelRenderer.Draw(content, context, axis.Formatter(tick), model.FontFamily, model.FontSize, model.TextColor, layout.Plot.Right + 4f, secondary.Map(tick) - model.FontSize * 0.35f);
        }
    }
}
