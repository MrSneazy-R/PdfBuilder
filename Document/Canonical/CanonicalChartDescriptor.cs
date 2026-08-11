using PdfBuilder.Elements;
using PdfBuilder.Elements.CanonicalCharts;
using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalChartDescriptor : IChartDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly CanonicalChartModel _model = new();

        public ChartElement Chart { get; } = new();

        public CanonicalChartDescriptor(DocumentTheme theme)
        {
            _theme = theme;
            Chart.CanonicalModel = _model;
            if (theme.ChartPalette.Count > 0)
                SetPalette(theme.ChartPalette);
        }

        public void Size(float width, float height)
        {
            Chart.Width = Positive(width, nameof(width));
            Chart.Height = Positive(height, nameof(height));
        }

        public void Title(string value) => Chart.Title = value ?? string.Empty;

        public void LabelStyle(Action<ITextStyleDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var style = new CanonicalTextStyle();
            configure(style);
            style.Apply(Chart, _theme);
            _model.FontFamily = Chart.Font;
            _model.FontSize = Chart.FontSize;
            _model.TextColor = ToPdfColor(Chart.AxisColor);
        }

        public void Categories(params string[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            _model.Categories.Clear();
            _model.Categories.AddRange(values.Select(value => value ?? string.Empty));
        }

        public void XAxis(Action<IChartAxisDescriptor> configure) => ConfigureAxis(_model.XAxis, configure);
        public void YAxis(Action<IChartAxisDescriptor> configure) => ConfigureAxis(_model.YAxis, configure);
        public void SecondaryYAxis(Action<IChartAxisDescriptor> configure)
        {
            _model.SecondaryYAxis ??= new CanonicalChartAxis();
            ConfigureAxis(_model.SecondaryYAxis, configure);
        }

        public void Legend(ChartLegendPosition position = ChartLegendPosition.TopRight) => _model.Legend = position;

        public void Palette(params PdfColor[] colors)
        {
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            if (colors.Length == 0) throw new ArgumentException("A chart palette requires at least one colour.", nameof(colors));
            _model.Palette.Clear();
            _model.Palette.AddRange(colors);
        }

        public void Palette(params string[] themeColors) => SetPalette(themeColors ?? throw new ArgumentNullException(nameof(themeColors)));

        public ILineChartSeriesDescriptor Line(string name, IEnumerable<float> values)
        {
            var series = new CanonicalLineSeries(RequiredName(name), Materialize(values), area: false);
            _model.Series.Add(series);
            return new LineDescriptor(series, _theme);
        }

        public IAreaChartSeriesDescriptor Area(string name, IEnumerable<float> values)
        {
            var series = new CanonicalLineSeries(RequiredName(name), Materialize(values), area: true);
            _model.Series.Add(series);
            return new AreaDescriptor(series, _theme);
        }

        public IBarChartSeriesDescriptor GroupedBars(string name, IEnumerable<float> values)
            => AddBars(name, values, null, normalise: false);

        public IBarChartSeriesDescriptor StackedBars(string name, IEnumerable<float> values, string stack = "default")
            => AddBars(name, values, RequiredName(stack), normalise: false);

        public IBarChartSeriesDescriptor Stacked100Bars(string name, IEnumerable<float> values, string stack = "default")
            => AddBars(name, values, RequiredName(stack), normalise: true);

        public IPieChartSeriesDescriptor Pie(string name, IEnumerable<ChartValue> values)
            => AddPie(name, values, 0f);

        public IPieChartSeriesDescriptor Donut(string name, IEnumerable<ChartValue> values, float innerRatio = 0.6f)
        {
            if (!float.IsFinite(innerRatio) || innerRatio <= 0f || innerRatio >= 1f)
                throw new ArgumentOutOfRangeException(nameof(innerRatio), "A donut inner ratio must be greater than zero and less than one.");
            return AddPie(name, values, innerRatio);
        }

        public IScatterChartSeriesDescriptor Scatter(string name, IEnumerable<ChartPoint> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            ChartPoint[] materialized = points.ToArray();
            if (materialized.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y)))
                throw new ArgumentException("Scatter points must contain finite coordinates.", nameof(points));
            var series = new CanonicalScatterSeries(RequiredName(name), materialized);
            _model.Series.Add(series);
            return new ScatterDescriptor(series, _theme);
        }

        public void Line(string name, IEnumerable<float> values, PdfColor color, float strokeWidth = 1f)
        {
            var descriptor = Line(name, values);
            descriptor.Color(color);
            descriptor.StrokeWidth(strokeWidth);
        }

        public void Bars(string name, IEnumerable<float> values, PdfColor color)
        {
            var descriptor = GroupedBars(name, values);
            descriptor.Color(color);
        }

        private IBarChartSeriesDescriptor AddBars(string name, IEnumerable<float> values, string? stack, bool normalise)
        {
            var series = new CanonicalBarSeries(RequiredName(name), Materialize(values), stack, normalise);
            _model.Series.Add(series);
            return new BarDescriptor(series, _theme);
        }

        private IPieChartSeriesDescriptor AddPie(string name, IEnumerable<ChartValue> values, float innerRatio)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            ChartValue[] materialized = values.ToArray();
            if (materialized.Any(value => !float.IsFinite(value.Value) || value.Value < 0f))
                throw new ArgumentException("Pie and donut values must be finite and non-negative.", nameof(values));
            var series = new CanonicalPieSeries(RequiredName(name), materialized, innerRatio);
            _model.Series.Add(series);
            return new PieDescriptor(series, _theme);
        }

        private void SetPalette(IEnumerable<string> values)
        {
            string[] materialized = values.ToArray();
            if (materialized.Length == 0) throw new ArgumentException("A chart palette requires at least one colour.", nameof(values));
            _model.Palette.Clear();
            foreach (string value in materialized)
                _model.Palette.Add(ResolveColor(value, _theme));
        }

        private static void ConfigureAxis(CanonicalChartAxis axis, Action<IChartAxisDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(new AxisDescriptor(axis));
        }

        private static float[] Materialize(IEnumerable<float> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            float[] result = values.ToArray();
            if (result.Any(value => !float.IsFinite(value))) throw new ArgumentException("Chart values must be finite.", nameof(values));
            return result;
        }

        private static string RequiredName(string value)
            => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A chart series name is required.", nameof(value)) : value;

        private static float Positive(float value, string parameterName)
            => !float.IsFinite(value) || value <= 0f ? throw new ArgumentOutOfRangeException(parameterName) : value;

        private static PdfColor ResolveColor(string value, DocumentTheme theme)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A colour or theme token is required.", nameof(value));
            string resolved = theme.ResolveColor(value);
            try { return PdfColor.Parse(resolved); }
            catch (FormatException exception) { throw new FormatException($"Chart colour '{value}' resolved to '{resolved}', which is not #RRGGBB or #AARRGGBB.", exception); }
        }

        private static PdfColor ToPdfColor(System.Drawing.Color value) => new(value.R, value.G, value.B, value.A);

        private sealed class AxisDescriptor(CanonicalChartAxis axis) : IChartAxisDescriptor
        {
            public void Range(float? minimum = null, float? maximum = null)
            {
                if (minimum.HasValue && !float.IsFinite(minimum.Value)) throw new ArgumentOutOfRangeException(nameof(minimum));
                if (maximum.HasValue && !float.IsFinite(maximum.Value)) throw new ArgumentOutOfRangeException(nameof(maximum));
                if (minimum.HasValue && maximum.HasValue && minimum.Value >= maximum.Value) throw new ArgumentException("The axis minimum must be less than its maximum.");
                axis.Minimum = minimum;
                axis.Maximum = maximum;
            }
            public void Ticks(int desiredCount) => axis.DesiredTicks = desiredCount < 2 ? throw new ArgumentOutOfRangeException(nameof(desiredCount)) : desiredCount;
            public void Format(Func<float, string> formatter) => axis.Formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        private abstract class SeriesDescriptor(CanonicalChartSeries series, DocumentTheme theme) : IChartSeriesDescriptor
        {
            protected DocumentTheme Theme { get; } = theme;
            public void Color(PdfColor color) => series.Color = color;
            public void Color(string themeColor) => series.Color = ResolveColor(themeColor, Theme);
            public void SecondaryAxis(bool enabled = true) => series.SecondaryAxis = enabled;
            public abstract void Labels(Func<float, string>? formatter = null);
        }

        private class LineDescriptor(CanonicalLineSeries series, DocumentTheme theme) : SeriesDescriptor(series, theme), ILineChartSeriesDescriptor
        {
            protected CanonicalLineSeries Series { get; } = series;
            public override void Labels(Func<float, string>? formatter = null) { Series.ShowLabels = true; if (formatter != null) Series.LabelFormatter = formatter; }
            public void StrokeWidth(float width) => Series.StrokeWidth = Positive(width, nameof(width));
            public void Markers(ChartMarkerShape shape = ChartMarkerShape.Circle, float size = 4f, PdfColor? fill = null) { Series.Marker = shape; Series.MarkerSize = Positive(size, nameof(size)); Series.MarkerFill = fill; }
            public void Smooth(bool enabled = true, float tension = 0.5f) { if (!float.IsFinite(tension) || tension < 0f || tension > 1f) throw new ArgumentOutOfRangeException(nameof(tension)); Series.Smooth = enabled; Series.SmoothTension = tension; }
        }

        private sealed class AreaDescriptor(CanonicalLineSeries series, DocumentTheme theme) : LineDescriptor(series, theme), IAreaChartSeriesDescriptor
        {
            public void Fill(PdfColor color) => Series.Fill = color;
            public void Fill(string themeColor) => Series.Fill = ResolveColor(themeColor, Theme);
        }

        private sealed class BarDescriptor(CanonicalBarSeries series, DocumentTheme theme) : SeriesDescriptor(series, theme), IBarChartSeriesDescriptor
        {
            public override void Labels(Func<float, string>? formatter = null) { series.ShowLabels = true; if (formatter != null) series.LabelFormatter = formatter; }
            public void Gap(float ratio) => series.GapRatio = !float.IsFinite(ratio) || ratio < 0f || ratio >= 1f ? throw new ArgumentOutOfRangeException(nameof(ratio)) : ratio;
        }

        private sealed class PieDescriptor(CanonicalPieSeries series, DocumentTheme theme) : IPieChartSeriesDescriptor
        {
            public void Colors(params PdfColor[] colors) { if (colors == null) throw new ArgumentNullException(nameof(colors)); series.SliceColors.Clear(); series.SliceColors.AddRange(colors); }
            public void Colors(params string[] themeColors) { if (themeColors == null) throw new ArgumentNullException(nameof(themeColors)); series.SliceColors.Clear(); series.SliceColors.AddRange(themeColors.Select(value => ResolveColor(value, theme))); }
            public void Labels(Func<ChartValue, string>? formatter = null, bool outside = true) { series.ShowLabels = true; series.LabelsOutside = outside; if (formatter != null) series.LabelFormatter = formatter; }
            public void StartAngle(float degrees) => series.StartAngle = !float.IsFinite(degrees) ? throw new ArgumentOutOfRangeException(nameof(degrees)) : degrees;
        }

        private sealed class ScatterDescriptor(CanonicalScatterSeries series, DocumentTheme theme) : IScatterChartSeriesDescriptor
        {
            public void Color(PdfColor color) => series.Color = color;
            public void Color(string themeColor) => series.Color = ResolveColor(themeColor, theme);
            public void SecondaryAxis(bool enabled = true) => series.SecondaryAxis = enabled;
            public void Markers(ChartMarkerShape shape = ChartMarkerShape.Circle, float size = 5f, PdfColor? fill = null) { series.Marker = shape; series.MarkerSize = Positive(size, nameof(size)); series.MarkerFill = fill; }
            public void Labels(Func<ChartPoint, string>? formatter = null) { series.ShowLabels = true; if (formatter != null) series.LabelFormatter = formatter; }
        }
    }
}
