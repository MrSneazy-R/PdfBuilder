using System.Text;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal static class BarChartSeriesRenderer
{
    public static void Append(StringBuilder content, CanonicalChartModel model, ChartLayout layout, ChartScales scales, PdfRenderContext context)
    {
        CanonicalBarSeries[] all = model.Series.OfType<CanonicalBarSeries>().ToArray();
        CanonicalBarSeries[] grouped = all.Where(series => series.Stack == null).ToArray();
        float bandWidth = layout.Plot.Width / Math.Max(1, scales.CategoryCount);

        for (int seriesIndex = 0; seriesIndex < grouped.Length; seriesIndex++)
        {
            CanonicalBarSeries series = grouped[seriesIndex];
            float slotWidth = bandWidth / Math.Max(1, grouped.Length);
            float width = slotWidth * (1f - series.GapRatio);
            for (int index = 0; index < series.Values.Count; index++)
                DrawBar(content, context, model, layout, scales, series, index, series.Values[index], index * bandWidth + seriesIndex * slotWidth + (slotWidth - width) / 2f, width, 0f);
        }

        foreach (IGrouping<string, CanonicalBarSeries> stack in all.Where(series => series.Stack != null).GroupBy(series => series.Stack!))
        {
            CanonicalBarSeries[] members = stack.ToArray();
            int count = members.Select(series => series.Values.Count).DefaultIfEmpty(0).Max();
            for (int index = 0; index < count; index++)
            {
                float positive = 0f;
                float total = members.Sum(series => index < series.Values.Count ? Math.Max(0f, series.Values[index]) : 0f);
                foreach (CanonicalBarSeries series in members)
                {
                    if (index >= series.Values.Count) continue;
                    float original = series.Values[index];
                    float value = series.Normalise ? (total <= 0f ? 0f : original / total * 100f) : original;
                    float width = bandWidth * (1f - series.GapRatio);
                    DrawBar(content, context, model, layout, scales, series, index, value, index * bandWidth + (bandWidth - width) / 2f, width, positive);
                    positive += value;
                }
            }
        }
    }

    private static void DrawBar(StringBuilder content, PdfRenderContext context, CanonicalChartModel model, ChartLayout layout, ChartScales scales, CanonicalBarSeries series, int index, float value, float relativeX, float width, float start)
    {
        LinearChartScale scale = series.SecondaryAxis ? scales.SecondaryY ?? scales.PrimaryY : scales.PrimaryY;
        float y0 = scale.Map(start);
        float y1 = scale.Map(start + value);
        PdfBuilder.Models.PdfColor color = series.Color ?? model.Palette[model.Series.IndexOf(series) % model.Palette.Count];
        ChartDrawing.Rectangle(content, layout.Plot.Left + relativeX, Math.Min(y0, y1), width, Math.Max(0.25f, Math.Abs(y1 - y0)), color);
        if (series.ShowLabels)
            ChartLabelRenderer.Draw(content, context, series.LabelFormatter(value), model.FontFamily, model.FontSize, model.TextColor, layout.Plot.Left + relativeX + width / 2f, Math.Max(y0, y1) + 3f, centered: true);
    }
}
