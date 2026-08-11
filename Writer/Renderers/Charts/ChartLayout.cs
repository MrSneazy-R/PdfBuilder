using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal readonly record struct ChartRect(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;
    public float Bottom => Top - Height;
}

internal readonly record struct ChartLayout(ChartRect Plot, float TitleY, ChartRect Legend)
{
    public static ChartLayout Calculate(ChartElement chart, CanonicalChartModel model)
    {
        float titleHeight = string.IsNullOrWhiteSpace(chart.Title) ? 0f : chart.TitleSize * 1.35f;
        float legendHeight = model.Legend == ChartLegendPosition.Below ? Math.Max(18f, model.Series.Count * 13f + 4f) : 0f;
        float top = chart.Y - titleHeight - chart.PaddingTop;
        float width = Math.Max(1f, chart.Width - chart.PaddingLeft - chart.PaddingRight);
        float height = Math.Max(1f, chart.Height - chart.PaddingTop - chart.PaddingBottom - legendHeight);
        var plot = new ChartRect(chart.X + chart.PaddingLeft, top, width, height);
        var legend = new ChartRect(plot.Left, plot.Bottom - 4f, plot.Width, legendHeight);
        return new ChartLayout(plot, chart.Y, legend);
    }
}
