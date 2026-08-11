using PdfBuilder.Document;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal readonly record struct LinearChartScale(float Minimum, float Maximum, float Start, float End)
{
    public float Map(float value) => Math.Abs(Maximum - Minimum) < 0.000001f
        ? Start
        : Start + (value - Minimum) / (Maximum - Minimum) * (End - Start);
}

internal sealed class ChartScales
{
    private ChartScales() { }
    public required LinearChartScale X { get; init; }
    public required LinearChartScale PrimaryY { get; init; }
    public LinearChartScale? SecondaryY { get; init; }
    public required IReadOnlyList<float> XTicks { get; init; }
    public required IReadOnlyList<float> PrimaryYTicks { get; init; }
    public IReadOnlyList<float> SecondaryYTicks { get; init; } = [];
    public bool NumericX { get; init; }
    public int CategoryCount { get; init; }

    public float CategoryCenter(int index, ChartRect plot)
        => plot.Left + (index + 0.5f) * plot.Width / Math.Max(1, CategoryCount);

    public static ChartScales Create(CanonicalChartModel model, ChartRect plot)
    {
        int categoryCount = Math.Max(model.Categories.Count, model.Series.Select(CountCategories).DefaultIfEmpty(0).Max());
        bool numericX = model.Series.Any(series => series is CanonicalScatterSeries) && categoryCount == 0;

        float xMin = 0f, xMax = Math.Max(1, categoryCount - 1);
        if (numericX)
        {
            ChartPoint[] points = model.Series.OfType<CanonicalScatterSeries>().SelectMany(series => series.Points).ToArray();
            if (points.Length > 0) { xMin = points.Min(point => point.X); xMax = points.Max(point => point.X); }
        }
        xMin = model.XAxis.Minimum ?? xMin;
        xMax = model.XAxis.Maximum ?? xMax;
        var xTicks = ChartTicks.Create(xMin, xMax, model.XAxis.DesiredTicks);

        (float primaryMin, float primaryMax) = Domain(model, secondary: false);
        primaryMin = model.YAxis.Minimum ?? Math.Min(0f, primaryMin);
        primaryMax = model.YAxis.Maximum ?? primaryMax;
        var primaryTicks = ChartTicks.Create(primaryMin, primaryMax, model.YAxis.DesiredTicks);

        LinearChartScale? secondaryScale = null;
        IReadOnlyList<float> secondaryValues = [];
        if (model.SecondaryYAxis != null || model.Series.Any(series => series.SecondaryAxis))
        {
            CanonicalChartAxis axis = model.SecondaryYAxis ?? new CanonicalChartAxis();
            (float secondaryMin, float secondaryMax) = Domain(model, secondary: true);
            var ticks = ChartTicks.Create(axis.Minimum ?? Math.Min(0f, secondaryMin), axis.Maximum ?? secondaryMax, axis.DesiredTicks);
            secondaryScale = new LinearChartScale(ticks.Minimum, ticks.Maximum, plot.Bottom, plot.Top);
            secondaryValues = ticks.Values;
        }

        return new ChartScales
        {
            X = new LinearChartScale(xTicks.Minimum, xTicks.Maximum, plot.Left, plot.Right),
            PrimaryY = new LinearChartScale(primaryTicks.Minimum, primaryTicks.Maximum, plot.Bottom, plot.Top),
            SecondaryY = secondaryScale,
            XTicks = xTicks.Values,
            PrimaryYTicks = primaryTicks.Values,
            SecondaryYTicks = secondaryValues,
            NumericX = numericX,
            CategoryCount = Math.Max(1, categoryCount)
        };
    }

    private static int CountCategories(CanonicalChartSeries series) => series switch
    {
        CanonicalLineSeries line => line.Values.Count,
        CanonicalBarSeries bar => bar.Values.Count,
        _ => 0
    };

    private static (float Minimum, float Maximum) Domain(CanonicalChartModel model, bool secondary)
    {
        var values = new List<float>();
        foreach (CanonicalChartSeries series in model.Series.Where(series => series.SecondaryAxis == secondary))
        {
            switch (series)
            {
                case CanonicalLineSeries line: values.AddRange(line.Values); break;
                case CanonicalBarSeries bar when bar.Normalise: values.AddRange(bar.Values.Select(_ => 100f)); break;
                case CanonicalBarSeries bar: values.AddRange(bar.Values); break;
                case CanonicalScatterSeries scatter: values.AddRange(scatter.Points.Select(point => point.Y)); break;
            }
        }

        foreach (IGrouping<string, CanonicalBarSeries> stack in model.Series.OfType<CanonicalBarSeries>().Where(series => series.SecondaryAxis == secondary && series.Stack != null).GroupBy(series => series.Stack!))
        {
            int count = stack.Select(series => series.Values.Count).DefaultIfEmpty(0).Max();
            for (int index = 0; index < count; index++)
                values.Add(stack.Any(series => series.Normalise)
                    ? 100f
                    : stack.Sum(series => index < series.Values.Count ? series.Values[index] : 0f));
        }

        return values.Count == 0 ? (0f, 1f) : (values.Min(), values.Max());
    }
}
