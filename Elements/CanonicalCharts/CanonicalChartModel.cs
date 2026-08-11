using PdfBuilder.Document;
using PdfBuilder.Models;

namespace PdfBuilder.Elements.CanonicalCharts;

internal sealed class CanonicalChartModel
{
    internal static readonly PdfColor[] DefaultPalette =
    [
        PdfColor.Parse("#4E79A7"), PdfColor.Parse("#F2A63B"), PdfColor.Parse("#76B7B2"), PdfColor.Parse("#59A15D"),
        PdfColor.Parse("#ED6A5A"), PdfColor.Parse("#AF7AD6"), PdfColor.Parse("#8DB4EA"), PdfColor.Parse("#E18888")
    ];

    public List<string> Categories { get; } = [];
    public CanonicalChartAxis XAxis { get; } = new();
    public CanonicalChartAxis YAxis { get; } = new();
    public CanonicalChartAxis? SecondaryYAxis { get; set; }
    public ChartLegendPosition Legend { get; set; } = ChartLegendPosition.Hidden;
    public List<PdfColor> Palette { get; } = [.. DefaultPalette];
    public List<CanonicalChartSeries> Series { get; } = [];
    public string FontFamily { get; set; } = "Helvetica";
    public float FontSize { get; set; } = 9f;
    public PdfColor TextColor { get; set; } = PdfColor.Rgb(0, 0, 0);
}

internal sealed class CanonicalChartAxis
{
    public float? Minimum { get; set; }
    public float? Maximum { get; set; }
    public int DesiredTicks { get; set; } = 5;
    public Func<float, string> Formatter { get; set; } = value => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

internal abstract class CanonicalChartSeries(string name)
{
    public string Name { get; } = name;
    public PdfColor? Color { get; set; }
    public bool SecondaryAxis { get; set; }
}

internal sealed class CanonicalLineSeries(string name, IReadOnlyList<float> values, bool area) : CanonicalChartSeries(name)
{
    public IReadOnlyList<float> Values { get; } = values;
    public bool Area { get; } = area;
    public PdfColor? Fill { get; set; }
    public float StrokeWidth { get; set; } = 1.5f;
    public ChartMarkerShape Marker { get; set; }
    public float MarkerSize { get; set; } = 4f;
    public PdfColor? MarkerFill { get; set; }
    public bool Smooth { get; set; }
    public float SmoothTension { get; set; } = 0.5f;
    public bool ShowLabels { get; set; }
    public Func<float, string> LabelFormatter { get; set; } = value => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed class CanonicalBarSeries(string name, IReadOnlyList<float> values, string? stack, bool normalise) : CanonicalChartSeries(name)
{
    public IReadOnlyList<float> Values { get; } = values;
    public string? Stack { get; } = stack;
    public bool Normalise { get; } = normalise;
    public float GapRatio { get; set; } = 0.15f;
    public bool ShowLabels { get; set; }
    public Func<float, string> LabelFormatter { get; set; } = value => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed class CanonicalPieSeries(string name, IReadOnlyList<ChartValue> values, float innerRatio) : CanonicalChartSeries(name)
{
    public IReadOnlyList<ChartValue> Values { get; } = values;
    public float InnerRatio { get; } = innerRatio;
    public List<PdfColor> SliceColors { get; } = [];
    public bool ShowLabels { get; set; } = true;
    public bool LabelsOutside { get; set; } = true;
    public float StartAngle { get; set; } = -90f;
    public Func<ChartValue, string> LabelFormatter { get; set; } = value => value.Category;
}

internal sealed class CanonicalScatterSeries(string name, IReadOnlyList<ChartPoint> points) : CanonicalChartSeries(name)
{
    public IReadOnlyList<ChartPoint> Points { get; } = points;
    public ChartMarkerShape Marker { get; set; } = ChartMarkerShape.Circle;
    public float MarkerSize { get; set; } = 5f;
    public PdfColor? MarkerFill { get; set; }
    public bool ShowLabels { get; set; }
    public Func<ChartPoint, string> LabelFormatter { get; set; } = point => $"{point.Y:0.##}";
}
