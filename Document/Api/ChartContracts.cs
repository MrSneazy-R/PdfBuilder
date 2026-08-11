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

    /// <summary>Compatibility overload for the original canonical line API.</summary>
    void Line(string name, IEnumerable<float> values, PdfColor color, float strokeWidth = 1f);
    /// <summary>Compatibility overload for the original canonical bar API.</summary>
    void Bars(string name, IEnumerable<float> values, PdfColor color);
}
