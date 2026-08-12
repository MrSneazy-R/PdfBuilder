// PdfBuilder/Elements/ChartElement.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Elements.CanonicalCharts;
using static PdfBuilder.Elements.ChartElement;

namespace PdfBuilder.Elements
{
    public partial class ChartElement : PdfElement
    {
        public float Width { get; set; } = 500;
        public float Height { get; set; } = 220;

        // Outer padding around plot area (for cartesian charts)
        public float PaddingTop { get; set; } = 8;
        public float PaddingRight { get; set; } = 12;
        public float PaddingBottom { get; set; } = 28;
        public float PaddingLeft { get; set; } = 40;

        // Title
        public string Title { get; set; } = "";
        public string TitleFont { get; set; } = "Helvetica";
        public float TitleSize { get; set; } = 11;

        // Axes (used for cartesian series)
        public Axis XAxis { get; set; } = Axis.Category();
        public Axis YAxis { get; set; } = Axis.Numeric();
        public Axis? YAxis2 { get; set; } = null; // optional secondary Y

        // Look
        public Color GridColor { get; set; } = Color.FromArgb(200, 200, 200);
        public Color AxisColor { get; set; } = Color.Black;
        public float AxisWidth { get; set; } = 0.5f;
        public bool ShowGridY { get; set; } = true;
        public bool ShowGridX { get; set; } = false;

        // Legend
        public bool ShowLegend { get; set; } = false;
        public enum LegendPos { None, InsideTopRight, InsideTopLeft, Below }
        public LegendPos LegendPosition { get; set; } = LegendPos.InsideTopRight;
        public Color LegendTextColor { get; set; } = Color.Black;
        public string LegendFont { get; set; } = "Helvetica";
        public float LegendFontSize { get; set; } = 9f;

        public enum BarValueLabelPos { InsideEnd, InsideBase, Center, OutsideEnd, OutsideBase }
        public enum LineValueLabelPos { Above, Below, Left, Right, Center }

        // Series
        public List<IChartSeries> Series { get; } = new();

        internal CanonicalChartModel? CanonicalModel { get; set; }

        // Fonts for axes labels
        public string Font { get; set; } = "Helvetica";
        public float FontSize { get; set; } = 9;

        // Palette (used for pies/bars/etc. when per-slice/per-bar color not supplied)
        public List<Color> Palette { get; } = new()
        {
            Color.FromArgb(0x4E,0x79,0xA7), // blue
            Color.FromArgb(0xF2,0xA6,0x3B), // orange
            Color.FromArgb(0x76,0xB7,0xB2), // teal
            Color.FromArgb(0x59,0xA1,0x5D), // green
            Color.FromArgb(0xED,0x6A,0x5A), // red
            Color.FromArgb(0xAF,0x7A,0xD6), // purple
            Color.FromArgb(0x8D,0xB4,0xEA), // light blue
            Color.FromArgb(0xE1,0x88,0x88), // pinkish
        };

        public ChartElement() : base(0, 0) { }
        public ChartElement(float x, float y) : base(x, y) { }
    }

    public sealed class Axis
    {
        public bool IsCategory { get; private set; }
        public List<string> Categories { get; } = new();
        public float? Min { get; set; }
        public float? Max { get; set; }
        public int TicksDesired { get; set; } = 5;
        public float LabelRotationDeg { get; set; } = 0;
        public Func<float, string> Format { get; set; } = v => v.ToString("0.##");

        public static Axis Category() => new Axis { IsCategory = true };
        public static Axis Numeric() => new Axis { IsCategory = false };
    }

    public interface IChartSeries
    {
        string Name { get; }
        Color Stroke { get; }
    }

    /// <summary>For cartesian series that can bind to Y axis 0 or 1.</summary>
    public interface ICartesianSeries : IChartSeries
    {
        int YAxisIndex { get; set; } // 0 -> YAxis, 1 -> YAxis2
    }

    // -----------------------------
    // Existing Cartesian Series
    // -----------------------------
    public sealed class LineSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 1f;
        public List<System.Drawing.PointF> Points { get; set; } = new();
        public bool UsesCategoryX { get; set; } = true;

        // NEW: customizations
        public bool Smooth { get; set; } = false;
        public float SmoothTension { get; set; } = 0.5f; // 0..1
        public bool ShowMarkers { get; set; } = false;
        public float MarkerSize { get; set; } = 3f;
        public Color? MarkerFill { get; set; }
        public bool Area { get; set; } = false;
        public Color? AreaFill { get; set; }
        public float AreaBaseline { get; set; } = float.NaN; // NaN => tickMin

        // NEW: step & stack
        public bool Step { get; set; } = false;
        public string? StackKey { get; set; } = null; // stacked area (null => not stacked)

        // Secondary axis support
        public int YAxisIndex { get; set; } = 0;

        // Point labels (useful for "target" values)
        public bool ShowValueLabels { get; set; } = false;
        public LineValueLabelPos ValueLabelPosition { get; set; } = LineValueLabelPos.Above;
        public string ValueLabelFont { get; set; } = "Helvetica";
        public float ValueLabelSize { get; set; } = 8f;
        public Color ValueLabelColor { get; set; } = Color.Black;
        public float ValueLabelOffset { get; set; } = 3f;
        public bool LabelOnlyLast { get; set; } = false; // nice for a constant Target line
        public Func<System.Drawing.PointF, string> PointLabelFormatter { get; set; } =
            p => p.Y.ToString("0.##");
    }

    public sealed class BarSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public Color Fill { get; set; } = Color.FromArgb(230, 230, 255);
        public float StrokeWidth { get; set; } = 0.5f;
        public List<(int categoryIndex, float value)> Bars { get; } = new();
        public float GapRatio { get; set; } = 0.15f; // 0..0.9
        public string? StackKey { get; set; } = null;

        // NEW: customizations
        public float CornerRadius { get; set; } = 0f;
        public List<Color> BarFills { get; } = new();
        public Color? AlternateFill { get; set; }

        // NEW: orientation & normalized 100%
        public bool Horizontal { get; set; } = false;
        public bool NormalizeTo100 { get; set; } = false;

        public int YAxisIndex { get; set; } = 0;

        // Data labels
        public bool ShowValueLabels { get; set; } = false;
        public BarValueLabelPos ValueLabelPosition { get; set; } = BarValueLabelPos.OutsideEnd;
        public string ValueLabelFont { get; set; } = "Helvetica";
        public float ValueLabelSize { get; set; } = 8f;
        public Color ValueLabelColor { get; set; } = Color.Black;
        public float LabelPadding { get; set; } = 2f;
        public Func<float, string> ValueFormatter { get; set; } = v => v.ToString("0.##");

    }

    // -----------------------------
    // NEW: Pie / Donut Series
    // -----------------------------
    public sealed class PieSeries : IChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.White;
        public float StrokeWidth { get; set; } = 0.5f;

        /// <summary>Collection of slices. If Fill is null, the chart's Palette is used by index.</summary>
        public List<PieSlice> Slices { get; } = new();

        /// <summary>Start angle in degrees. Default -90 places the first slice at 12 o'clock.</summary>
        public float StartAngleDeg { get; set; } = -90f;

        /// <summary>Clockwise drawing if true, counterclockwise if false.</summary>
        public bool Clockwise { get; set; } = true;

        /// <summary>0 => full pie. e.g. 0.6 => donut with inner radius at 60% of outer.</summary>
        public float DonutInnerRatio { get; set; } = 0f;

        // Labels
        public bool ShowLabels { get; set; } = true;
        public bool LabelOutside { get; set; } = true;
        public bool LabelLeaderLines { get; set; } = false;
        public float LabelOffset { get; set; } = 6f;             // radial gap from outer radius
        public float LabelPadding { get; set; } = 3f;            // horizontal gap from leader end to text
        public float LeaderLineWidth { get; set; } = 0.5f;
        public Color LeaderLineColor { get; set; } = Color.Black;
        public bool LabelSmartAlign { get; set; } = true;       // auto left/right placement
        public string LabelFont { get; set; } = "Helvetica";
        public float LabelFontSize { get; set; } = 9f;
        public Color LabelColor { get; set; } = Color.Black;

        /// <summary>Format a label for a slice. Default: slice.Label (percent appended if AppendPercentages).</summary>
        public Func<PieSlice, string> LabelFormatter { get; set; } = s => s.Label;

        /// <summary>If true, percentages are appended automatically (based on total).</summary>
        public bool AppendPercentages { get; set; } = true;
    }

    public sealed class PieSlice
    {
        public string Label { get; set; } = "";
        public float Value { get; set; } = 0f;
        public Color? Fill { get; set; } = null;

        /// <summary>Explode offset as a fraction of radius (e.g., 0.08 = 8% of radius).</summary>
        public float ExplodeRatio { get; set; } = 0f;

        /// <summary>Optional override for the data label text.</summary>
        public string? CustomLabel { get; set; }

        public string? LabelFontOverride { get; set; }          // e.g., "Helvetica-Bold"
        public float? LabelSizeOverride { get; set; }
        public Color? LabelColorOverride { get; set; }
        public float? LabelOffsetOverride { get; set; }
        public float? LabelPaddingOverride { get; set; }
        public bool? LabelLeaderLinesOverride { get; set; }
    }

    // -----------------------------
    // NEW: Scatter Series
    // -----------------------------
    public sealed class ScatterSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.5f;

        /// <summary>Cartesian points. If UsesCategoryX = true, X = category index.</summary>
        public List<System.Drawing.PointF> Points { get; } = new();

        public bool UsesCategoryX { get; set; } = false;

        public MarkerShape Marker { get; set; } = MarkerShape.Circle;
        public float MarkerSize { get; set; } = 4f; // diameter in points
        public Color? Fill { get; set; } = Color.FromArgb(220, 220, 220);
        public bool Outline { get; set; } = true;

        public int YAxisIndex { get; set; } = 0;
    }

    public enum MarkerShape
    {
        Circle,
        Square,
        Triangle,
        Diamond,
        Cross,
        Plus
    }

    // -----------------------------
    // NEW: Bubble Series (size-mapped scatter)
    // -----------------------------
    public sealed class BubbleSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.5f;

        public List<BubblePoint> Points { get; } = new();

        /// <summary>Maps data Size to a rendered radius in points.</summary>
        public float MinRadius { get; set; } = 3f;
        public float MaxRadius { get; set; } = 12f;

        /// <summary>If true, X is treated as category index.</summary>
        public bool UsesCategoryX { get; set; } = false;

        /// <summary>Optional: clamp size domain. If null, domain is inferred from data.</summary>
        public float? SizeDomainMin { get; set; }
        public float? SizeDomainMax { get; set; }

        public int YAxisIndex { get; set; } = 0;
        // Labels on bubbles
        public bool ShowLabels { get; set; } = false;
        public string LabelFont { get; set; } = "Helvetica";
        public float LabelSize { get; set; } = 9f;
        public Color LabelColor { get; set; } = Color.Black;
        public float LabelOffset { get; set; } = 6f;
        public Func<BubblePoint, string> LabelFormatter { get; set; }
            = p => string.IsNullOrWhiteSpace(p.Category) ? $"{p.Size:0}" : p.Category;

        // Soft shadow to make bubbles pop
        public bool ShowShadow { get; set; } = true;
        public float ShadowDx { get; set; } = 1.4f;
        public float ShadowDy { get; set; } = -1.1f;
        public float ShadowScale { get; set; } = 1.05f;
        public Color ShadowColor { get; set; } = Color.FromArgb(210, 210, 210);

        // Optional: legend lists each bubble instead of one series swatch
        public bool LegendPerPoint { get; set; } = false;
    }

    public sealed class BubblePoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        /// <summary>Data size (will be scaled to radius).</summary>
        public float Size { get; set; }

        public Color? Fill { get; set; } = null;
        public string? Category { get; set; } = null; // optional semantic label
    }

    // -----------------------------
    // NEW: Waterfall (Bridge) Series
    // -----------------------------
    public sealed class WaterfallSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.5f;

        public Color PositiveFill { get; set; } = Color.FromArgb(180, 220, 180);
        public Color NegativeFill { get; set; } = Color.FromArgb(230, 170, 170);
        public Color TotalFill { get; set; } = Color.FromArgb(180, 180, 220);

        /// <summary>Each step contributes delta to cumulative baseline. If isTotal, show as total bar.</summary>
        public List<(int categoryIndex, float delta, bool isTotal)> Steps { get; } = new();

        public float GapRatio { get; set; } = 0.15f;
        public float CornerRadius { get; set; } = 0f;

        public int YAxisIndex { get; set; } = 0;
    }

    // -----------------------------
    // NEW: Histogram Series
    // -----------------------------
    public sealed class HistogramSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.5f;
        public Color Fill { get; set; } = Color.FromArgb(220, 220, 250);

        /// <summary>Raw samples (if provided, renderer bins them using BinCount or BinWidth).</summary>
        public List<float> Samples { get; } = new();

        /// <summary>Or, pre-binned counts.</summary>
        public List<(float binStart, float binEnd, int count)> Bins { get; } = new();

        public int? BinCount { get; set; } = null;
        public float? BinWidth { get; set; } = null;

        public int YAxisIndex { get; set; } = 0;

        // gaps + labels
        public float BarGapRatio { get; set; } = 0.15f;          // 0..0.9 (fraction of each bin reserved as gap)
        public bool ShowLabels { get; set; } = false;
        public string LabelFont { get; set; } = "Helvetica";
        public float LabelSize { get; set; } = 8f;
        public Color LabelColor { get; set; } = Color.Black;
        public float LabelOffset { get; set; } = 2f;            // px above the bar
        public Func<int, string> LabelFormatter { get; set; } = (n => n.ToString());
    }

    // -----------------------------
    // NEW: Box & Whisker (BoxPlot) Series
    // -----------------------------
    public sealed class BoxPlotSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.7f;
        public Color Fill { get; set; } = Color.FromArgb(235, 235, 255);

        /// <summary>Raw values per category (renderer computes stats if Stats not supplied).</summary>
        public List<(int categoryIndex, float[] values)> Groups { get; } = new();

        /// <summary>Precomputed stats (if given, preferred over Groups).</summary>
        public List<(int categoryIndex, float q1, float median, float q3, float whiskerLow, float whiskerHigh, List<float> outliers)> Stats { get; } = new();

        public float BoxWidthRatio { get; set; } = 0.7f;

        public int YAxisIndex { get; set; } = 0;
    }

    // -----------------------------
    // NEW: Heatmap Series
    // -----------------------------
    public sealed class HeatmapSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Transparent;
        public float StrokeWidth { get; set; } = 0f;

        public int Rows { get; set; }
        public int Cols { get; set; }
        public float[,] Values { get; set; } = new float[0, 0];

        public float? Min { get; set; }
        public float? Max { get; set; }

        /// <summary>Map value→color; if null, renderer uses a default gradient.</summary>
        public Func<float, Color>? ColorScale { get; set; } = null;

        public int YAxisIndex { get; set; } = 0;
    }

    // -----------------------------
    // NEW: Radar / Spider Series
    // -----------------------------
    public sealed class RadarSeries : IChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.8f;
        public Color? Fill { get; set; } = null;

        /// <summary>Values by category index.</summary>
        public List<(int categoryIndex, float value)> Points { get; } = new();

        /// <summary>Optional scaling; if null, renderer infers.</summary>
        public float? Min { get; set; }
        public float? Max { get; set; }

        public bool CloseShape { get; set; } = true;
        public bool ShowMarkers { get; set; } = false;
        public float MarkerSize { get; set; } = 3f;
        public Color? MarkerFill { get; set; } = null;
    }

    // -----------------------------
    // NEW: Funnel Series
    // -----------------------------
    public sealed class FunnelSeries : IChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.5f;

        public List<FunnelStage> Stages { get; } = new();
        public bool Tapered { get; set; } = true;
        public float Gap { get; set; } = 4f; // gap between stages (pt)

        public string LabelFont { get; set; } = "Helvetica";
        public float LabelFontSize { get; set; } = 9f;
        public Color LabelColor { get; set; } = Color.Black;
    }

    public sealed class FunnelStage
    {
        public string Stage { get; set; } = "";
        public float Value { get; set; }
        public Color? Fill { get; set; } = null;
    }

    // -----------------------------
    // NEW: Candlestick / OHLC Series
    // -----------------------------
    public sealed class CandleSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.7f;

        public Color UpFill { get; set; } = Color.FromArgb(190, 235, 190);
        public Color DownFill { get; set; } = Color.FromArgb(240, 190, 190);
        public Color WickStroke { get; set; } = Color.Black;
        public float CandleWidthRatio { get; set; } = 0.7f;

        public bool UsesCategoryX { get; set; } = true; // or numeric time

        public List<(int xIndex, float open, float high, float low, float close)> Candles { get; } = new();

        public int YAxisIndex { get; set; } = 0;
    }

    // -----------------------------
    // NEW: Bullet (KPI) Series
    // -----------------------------
    public sealed class BulletSeries : IChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.5f;

        public float Value { get; set; }
        public float Target { get; set; }
        public List<(float start, float end, Color fill)> QualitativeRanges { get; } = new(); // e.g., poor/avg/good

        public bool Horizontal { get; set; } = true;
        public Color ValueFill { get; set; } = Color.FromArgb(90, 140, 220);
        public Color TargetStroke { get; set; } = Color.Black;
        public float TargetStrokeWidth { get; set; } = 1f;
    }

    // -----------------------------
    // NEW: Pareto Series (convenience: bars + cumulative line)
    // -----------------------------
    public sealed class ParetoSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black; // cumulative line
        public float StrokeWidth { get; set; } = 1f;

        public Color BarFill { get; set; } = Color.FromArgb(230, 230, 255);
        public float BarGapRatio { get; set; } = 0.15f;
        public bool SortDescending { get; set; } = true;

        public List<(int categoryIndex, float value)> Items { get; } = new();

        public Color CumulativeStroke { get; set; } = Color.FromArgb(200, 80, 80);
        public float CumulativeStrokeWidth { get; set; } = 1f;

        public int YAxisIndex { get; set; } = 0;
    }

    // -----------------------------
    // NEW: Range/Band Area Series (min-max ribbon)
    // -----------------------------
    public sealed class RangeAreaSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Empty;
        public float StrokeWidth { get; set; } = 0f;

        public bool UsesCategoryX { get; set; } = true;

        /// <summary>Each point defines low/high. Use CategoryIndex when UsesCategoryX, otherwise X.</summary>
        public List<RangePoint> Points { get; } = new();

        public Color Fill { get; set; } = Color.FromArgb(120, 160, 220, 120);

        public int YAxisIndex { get; set; } = 0;
        public bool Smooth { get; set; } = false;
        public float SmoothTension { get; set; } = 0.45f;

    }

    public sealed class RangePoint
    {
        public int CategoryIndex { get; set; } = -1; // when UsesCategoryX
        public float X { get; set; } = 0f;           // when numeric X
        public float Low { get; set; }
        public float High { get; set; }
    }

    // -----------------------------
    // NEW: Error Bars (overlay)
    // -----------------------------
    public sealed class ErrorBarSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.8f;

        public bool UsesCategoryX { get; set; } = true;

        /// <summary>Points with +/- errors. If Symmetric, Error is used for both sides.</summary>
        public List<ErrorBarPoint> Points { get; } = new();

        public bool Symmetric { get; set; } = true;
        public float CapWidth { get; set; } = 6f;

        public int YAxisIndex { get; set; } = 0;
    }

    public sealed class ErrorBarPoint
    {
        public int CategoryIndex { get; set; } = -1; // when UsesCategoryX
        public float X { get; set; } = 0f;           // when numeric X
        public float Y { get; set; }
        public float Error { get; set; } = 0f;       // used if Symmetric
        public float ErrorMinus { get; set; } = 0f;  // used if !Symmetric
        public float ErrorPlus { get; set; } = 0f;   // used if !Symmetric
    }

    // -----------------------------
    // NEW: Gantt Series (horizontal task bars)
    // -----------------------------
    public sealed class GanttSeries : IChartSeries, ICartesianSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Stroke { get; set; } = Color.Black;
        public float StrokeWidth { get; set; } = 0.5f;

        /// <summary>Tasks are drawn as horizontal bars from StartX to EndX on row CategoryIndex.</summary>
        public List<GanttTask> Tasks { get; } = new();

        public float RowGap { get; set; } = 2f;       // vertical spacing between rows (pt)
        public float BarHeightRatio { get; set; } = 0.6f; // fraction of row height

        public int YAxisIndex { get; set; } = 0;
    }

    public sealed class GanttTask
    {
        public int CategoryIndex { get; set; } // Y row index (use XAxis categories for time)
        public float StartX { get; set; }      // numeric or category index depending on X axis
        public float EndX { get; set; }
        public string Label { get; set; } = "";
        public Color? Fill { get; set; } = null;
        public Color? Stroke { get; set; } = null;
    }
}

