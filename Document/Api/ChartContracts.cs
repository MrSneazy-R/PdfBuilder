using PdfBuilder.Models;

namespace PdfBuilder.Document;

/// <summary>Placement of a chart legend.</summary>
public enum ChartLegendPosition
{
    Hidden,
    TopLeft,
    TopRight,
    Below
}

/// <summary>Marker shapes supported by canonical line and scatter series.</summary>
public enum ChartMarkerShape
{
    None,
    Circle,
    Square,
    Triangle,
    Diamond,
    Cross,
    Plus
}

/// <summary>An immutable numeric chart point.</summary>
public readonly record struct ChartPoint(float X, float Y);

/// <summary>An immutable labelled value used by pie and donut charts.</summary>
public readonly record struct ChartValue(string Category, float Value);

public readonly record struct ChartBubblePoint(float X, float Y, float Size, string? Label = null, PdfColor? Fill = null);
public readonly record struct ChartWaterfallValue(float Delta, bool IsTotal = false);
public readonly record struct ChartFunnelValue(string Stage, float Value, PdfColor? Fill = null);
public readonly record struct ChartCandlestickValue(int CategoryIndex, float Open, float High, float Low, float Close);
public readonly record struct ChartBulletRange(float Start, float End, PdfColor Fill);
public readonly record struct ChartRangeValue(int CategoryIndex, float Low, float High);
public readonly record struct ChartErrorValue(int CategoryIndex, float Value, float ErrorMinus, float ErrorPlus);
public readonly record struct ChartBoxGroup(int CategoryIndex, IReadOnlyList<float> Values);
public readonly record struct ChartGanttTask(int CategoryIndex, float Start, float End, string Label, PdfColor? Fill = null);

/// <summary>Common configuration for advanced canonical chart series.</summary>
public interface IAdvancedChartSeriesDescriptor
{
    void Stroke(PdfColor color, float width = 0.5f);
    void SecondaryAxis(bool enabled = true);
}

public interface IBubbleChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Radius(float minimum, float maximum); }
public interface IWaterfallChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Colors(PdfColor positive, PdfColor negative, PdfColor total); }
public interface IRadarChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Fill(PdfColor color); void Range(float? minimum = null, float? maximum = null); }
public interface IFunnelChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Tapered(bool enabled = true); void Gap(float points); }
public interface IGanttChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Geometry(float rowGap, float barHeightRatio); }
public interface ICandlestickChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Colors(PdfColor up, PdfColor down, PdfColor wick); }
public interface IBulletChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void ValueColor(PdfColor color); void TargetStyle(PdfColor color, float width = 1f); }
public interface IParetoChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void BarColor(PdfColor color); void CumulativeStyle(PdfColor color, float width = 1f); }
public interface IRangeAreaChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Fill(PdfColor color); void Smooth(bool enabled = true, float tension = 0.45f); }
public interface IErrorBarChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void CapWidth(float points); }
public interface IHistogramChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Fill(PdfColor color); void Bins(int count); void Gap(float ratio); }
public interface IBoxPlotChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Fill(PdfColor color); void BoxWidth(float ratio); }
public interface IHeatmapChartSeriesDescriptor : IAdvancedChartSeriesDescriptor { void Range(float? minimum = null, float? maximum = null); void ColorScale(Func<float, PdfColor> scale); }

/// <summary>Configures a numeric chart axis.</summary>
public interface IChartAxisDescriptor
{
    void Range(float? minimum = null, float? maximum = null);
    void Ticks(int desiredCount);
    void Format(Func<float, string> formatter);
}

/// <summary>Common configuration shared by cartesian chart series.</summary>
public interface IChartSeriesDescriptor
{
    void Color(PdfColor color);
    void Color(string themeColor);
    void SecondaryAxis(bool enabled = true);
    void Labels(Func<float, string>? formatter = null);
}

/// <summary>Configures a line chart series.</summary>
public interface ILineChartSeriesDescriptor : IChartSeriesDescriptor
{
    void StrokeWidth(float width);
    void Markers(ChartMarkerShape shape = ChartMarkerShape.Circle, float size = 4f, PdfColor? fill = null);
    void Smooth(bool enabled = true, float tension = 0.5f);
}

/// <summary>Configures an area chart series.</summary>
public interface IAreaChartSeriesDescriptor : ILineChartSeriesDescriptor
{
    void Fill(PdfColor color);
    void Fill(string themeColor);
}

/// <summary>Configures a grouped or stacked bar chart series.</summary>
public interface IBarChartSeriesDescriptor : IChartSeriesDescriptor
{
    void Gap(float ratio);
}

/// <summary>Configures pie and donut labels and colours.</summary>
public interface IPieChartSeriesDescriptor
{
    void Colors(params PdfColor[] colors);
    void Colors(params string[] themeColors);
    void Labels(Func<ChartValue, string>? formatter = null, bool outside = true);
    void StartAngle(float degrees);
}

/// <summary>Configures a scatter chart series.</summary>
public interface IScatterChartSeriesDescriptor
{
    void Color(PdfColor color);
    void Color(string themeColor);
    void SecondaryAxis(bool enabled = true);
    void Markers(ChartMarkerShape shape = ChartMarkerShape.Circle, float size = 5f, PdfColor? fill = null);
    void Labels(Func<ChartPoint, string>? formatter = null);
}

/// <summary>Configures a canonical vector chart.</summary>
public interface IChartDescriptor
{
    void Size(float width, float height);
    void Title(string value);
    void LabelStyle(Action<ITextStyleDescriptor> configure);
    void Categories(params string[] values);
    void XAxis(Action<IChartAxisDescriptor> configure);
    void YAxis(Action<IChartAxisDescriptor> configure);
    void SecondaryYAxis(Action<IChartAxisDescriptor> configure);
    void Legend(ChartLegendPosition position = ChartLegendPosition.TopRight);
    void Palette(params PdfColor[] colors);
    void Palette(params string[] themeColors);

    ILineChartSeriesDescriptor Line(string name, IEnumerable<float> values);
    IAreaChartSeriesDescriptor Area(string name, IEnumerable<float> values);
    IBarChartSeriesDescriptor GroupedBars(string name, IEnumerable<float> values);
    IBarChartSeriesDescriptor StackedBars(string name, IEnumerable<float> values, string stack = "default");
    IBarChartSeriesDescriptor Stacked100Bars(string name, IEnumerable<float> values, string stack = "default");
    IPieChartSeriesDescriptor Pie(string name, IEnumerable<ChartValue> values);
    IPieChartSeriesDescriptor Donut(string name, IEnumerable<ChartValue> values, float innerRatio = 0.6f);
    IScatterChartSeriesDescriptor Scatter(string name, IEnumerable<ChartPoint> points);
    IBubbleChartSeriesDescriptor Bubble(string name, IEnumerable<ChartBubblePoint> points);
    IWaterfallChartSeriesDescriptor Waterfall(string name, IEnumerable<ChartWaterfallValue> values);
    IRadarChartSeriesDescriptor Radar(string name, IEnumerable<float> values);
    IFunnelChartSeriesDescriptor Funnel(string name, IEnumerable<ChartFunnelValue> stages);
    IGanttChartSeriesDescriptor Gantt(string name, IEnumerable<ChartGanttTask> tasks);
    ICandlestickChartSeriesDescriptor Candlestick(string name, IEnumerable<ChartCandlestickValue> values);
    IBulletChartSeriesDescriptor Bullet(string name, float value, float target, IEnumerable<ChartBulletRange> ranges);
    IParetoChartSeriesDescriptor Pareto(string name, IEnumerable<float> values);
    IRangeAreaChartSeriesDescriptor RangeArea(string name, IEnumerable<ChartRangeValue> values);
    IErrorBarChartSeriesDescriptor ErrorBars(string name, IEnumerable<ChartErrorValue> values);
    IHistogramChartSeriesDescriptor Histogram(string name, IEnumerable<float> samples);
    IBoxPlotChartSeriesDescriptor BoxPlot(string name, IEnumerable<ChartBoxGroup> groups);
    IHeatmapChartSeriesDescriptor Heatmap(string name, float[,] values);

    /// <summary>Compatibility overload for the original canonical line API.</summary>
    void Line(string name, IEnumerable<float> values, PdfColor color, float strokeWidth = 1f);
    /// <summary>Compatibility overload for the original canonical bar API.</summary>
    void Bars(string name, IEnumerable<float> values, PdfColor color);
}
