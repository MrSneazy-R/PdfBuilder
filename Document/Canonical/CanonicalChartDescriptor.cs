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
        private bool _advancedMode;

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
            Chart.XAxis.Categories.Clear();
            Chart.XAxis.Categories.AddRange(_model.Categories);
        }

        public void XAxis(Action<IChartAxisDescriptor> configure) { ConfigureAxis(_model.XAxis, configure); SyncAxis(_model.XAxis, Chart.XAxis); }
        public void YAxis(Action<IChartAxisDescriptor> configure) { ConfigureAxis(_model.YAxis, configure); SyncAxis(_model.YAxis, Chart.YAxis); }
        public void SecondaryYAxis(Action<IChartAxisDescriptor> configure)
        {
            _model.SecondaryYAxis ??= new CanonicalChartAxis();
            ConfigureAxis(_model.SecondaryYAxis, configure);
            Chart.YAxis2 ??= Axis.Numeric();
            SyncAxis(_model.SecondaryYAxis, Chart.YAxis2);
        }

        public void Legend(ChartLegendPosition position = ChartLegendPosition.TopRight)
        {
            _model.Legend = position;
            Chart.ShowLegend = position != ChartLegendPosition.Hidden;
            Chart.LegendPosition = position switch
            {
                ChartLegendPosition.TopLeft => ChartElement.LegendPos.InsideTopLeft,
                ChartLegendPosition.TopRight => ChartElement.LegendPos.InsideTopRight,
                ChartLegendPosition.Below => ChartElement.LegendPos.Below,
                _ => ChartElement.LegendPos.None
            };
        }

        public void Palette(params PdfColor[] colors)
        {
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            if (colors.Length == 0) throw new ArgumentException("A chart palette requires at least one colour.", nameof(colors));
            _model.Palette.Clear();
            _model.Palette.AddRange(colors);
            SyncLegacyPalette();
        }

        public void Palette(params string[] themeColors) => SetPalette(themeColors ?? throw new ArgumentNullException(nameof(themeColors)));

        public ILineChartSeriesDescriptor Line(string name, IEnumerable<float> values)
        {
            EnsureCoreMode();
            var series = new CanonicalLineSeries(RequiredName(name), Materialize(values), area: false);
            _model.Series.Add(series);
            return new LineDescriptor(series, _theme);
        }

        public IAreaChartSeriesDescriptor Area(string name, IEnumerable<float> values)
        {
            EnsureCoreMode();
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
            EnsureCoreMode();
            if (points == null) throw new ArgumentNullException(nameof(points));
            ChartPoint[] materialized = points.ToArray();
            if (materialized.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y)))
                throw new ArgumentException("Scatter points must contain finite coordinates.", nameof(points));
            var series = new CanonicalScatterSeries(RequiredName(name), materialized);
            _model.Series.Add(series);
            return new ScatterDescriptor(series, _theme);
        }

        public IBubbleChartSeriesDescriptor Bubble(string name, IEnumerable<ChartBubblePoint> points)
        {
            EnsureAdvancedMode();
            ChartBubblePoint[] materialized = RequiredFinite(points, point => point.X, point => point.Y, point => point.Size);
            if (materialized.Any(point => point.Size < 0f)) throw new ArgumentException("Bubble sizes must be non-negative.", nameof(points));
            var series = new BubbleSeries { Name = RequiredName(name) };
            foreach (var point in materialized)
                series.Points.Add(new BubblePoint { X = point.X, Y = point.Y, Size = point.Size, Category = point.Label, Fill = point.Fill.HasValue ? ToDrawingColor(point.Fill.Value) : null });
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, value => series.Stroke = value, value => series.StrokeWidth = value, value => series.YAxisIndex = value).AsBubble(series);
        }

        public IWaterfallChartSeriesDescriptor Waterfall(string name, IEnumerable<ChartWaterfallValue> values)
        {
            EnsureAdvancedMode();
            ChartWaterfallValue[] materialized = RequiredFinite(values, value => value.Delta);
            var series = new WaterfallSeries { Name = RequiredName(name) };
            for (int index = 0; index < materialized.Length; index++) series.Steps.Add((index, materialized[index].Delta, materialized[index].IsTotal));
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, value => series.Stroke = value, value => series.StrokeWidth = value, value => series.YAxisIndex = value).AsWaterfall(series);
        }

        public IRadarChartSeriesDescriptor Radar(string name, IEnumerable<float> values)
        {
            EnsureAdvancedMode();
            float[] materialized = Materialize(values);
            if (materialized.Length < 3) throw new ArgumentException("A radar series requires at least three values.", nameof(values));
            var series = new RadarSeries { Name = RequiredName(name) };
            for (int index = 0; index < materialized.Length; index++) series.Points.Add((index, materialized[index]));
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, value => series.Stroke = value, value => series.StrokeWidth = value).AsRadar(series);
        }

        public IFunnelChartSeriesDescriptor Funnel(string name, IEnumerable<ChartFunnelValue> stages)
        {
            EnsureAdvancedMode();
            ChartFunnelValue[] materialized = RequiredFinite(stages, stage => stage.Value);
            if (materialized.Any(stage => stage.Value < 0f)) throw new ArgumentException("Funnel values must be non-negative.", nameof(stages));
            var series = new FunnelSeries { Name = RequiredName(name) };
            foreach (var stage in materialized) series.Stages.Add(new FunnelStage { Stage = stage.Stage ?? string.Empty, Value = stage.Value, Fill = stage.Fill.HasValue ? ToDrawingColor(stage.Fill.Value) : null });
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, value => series.Stroke = value, value => series.StrokeWidth = value).AsFunnel(series);
        }

        public IGanttChartSeriesDescriptor Gantt(string name, IEnumerable<ChartGanttTask> tasks)
        {
            EnsureAdvancedMode();
            ChartGanttTask[] materialized = RequiredFinite(tasks, task => task.Start, task => task.End);
            if (materialized.Any(task => task.CategoryIndex < 0 || task.End < task.Start)) throw new ArgumentException("Gantt tasks require a non-negative category and an end at or after the start.", nameof(tasks));
            var series = new GanttSeries { Name = RequiredName(name) };
            foreach (var task in materialized) series.Tasks.Add(new GanttTask { CategoryIndex = task.CategoryIndex, StartX = task.Start, EndX = task.End, Label = task.Label ?? string.Empty, Fill = task.Fill.HasValue ? ToDrawingColor(task.Fill.Value) : null });
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, value => series.Stroke = value, value => series.StrokeWidth = value, value => series.YAxisIndex = value).AsGantt(series);
        }

        public ICandlestickChartSeriesDescriptor Candlestick(string name, IEnumerable<ChartCandlestickValue> values)
        {
            EnsureAdvancedMode();
            ChartCandlestickValue[] materialized = RequiredFinite(values, value => value.Open, value => value.High, value => value.Low, value => value.Close);
            if (materialized.Any(value => value.CategoryIndex < 0 || value.Low > Math.Min(value.Open, value.Close) || value.High < Math.Max(value.Open, value.Close)))
                throw new ArgumentException("Candlestick values require low <= open/close <= high and a non-negative category.", nameof(values));
            var series = new CandleSeries { Name = RequiredName(name) };
            foreach (var value in materialized) series.Candles.Add((value.CategoryIndex, value.Open, value.High, value.Low, value.Close));
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width, axis => series.YAxisIndex = axis).AsCandlestick(series);
        }

        public IBulletChartSeriesDescriptor Bullet(string name, float value, float target, IEnumerable<ChartBulletRange> ranges)
        {
            EnsureAdvancedMode();
            if (!float.IsFinite(value) || !float.IsFinite(target)) throw new ArgumentException("Bullet values must be finite.");
            ChartBulletRange[] materialized = (ranges ?? throw new ArgumentNullException(nameof(ranges))).ToArray();
            if (materialized.Any(range => !float.IsFinite(range.Start) || !float.IsFinite(range.End) || range.End <= range.Start)) throw new ArgumentException("Bullet ranges must be finite and increasing.", nameof(ranges));
            for (int index = 1; index < materialized.Length; index++)
                if (materialized[index].Start < materialized[index - 1].End)
                    throw new ArgumentException("Bullet ranges must be ordered and cannot overlap.", nameof(ranges));
            var series = new BulletSeries { Name = RequiredName(name), Value = value, Target = target };
            foreach (var range in materialized) series.QualitativeRanges.Add((range.Start, range.End, ToDrawingColor(range.Fill)));
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width).AsBullet(series);
        }

        public IParetoChartSeriesDescriptor Pareto(string name, IEnumerable<float> values)
        {
            EnsureAdvancedMode();
            float[] materialized = Materialize(values);
            if (materialized.Any(value => value < 0f)) throw new ArgumentException("Pareto values must be non-negative.", nameof(values));
            var series = new ParetoSeries { Name = RequiredName(name) };
            for (int index = 0; index < materialized.Length; index++) series.Items.Add((index, materialized[index]));
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width, axis => series.YAxisIndex = axis).AsPareto(series);
        }

        public IRangeAreaChartSeriesDescriptor RangeArea(string name, IEnumerable<ChartRangeValue> values)
        {
            EnsureAdvancedMode();
            ChartRangeValue[] materialized = RequiredFinite(values, value => value.Low, value => value.High);
            if (materialized.Any(value => value.CategoryIndex < 0 || value.High < value.Low)) throw new ArgumentException("Range-area values require a non-negative category and high >= low.", nameof(values));
            var series = new RangeAreaSeries { Name = RequiredName(name) };
            foreach (var value in materialized) series.Points.Add(new RangePoint { CategoryIndex = value.CategoryIndex, Low = value.Low, High = value.High });
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width, axis => series.YAxisIndex = axis).AsRangeArea(series);
        }

        public IErrorBarChartSeriesDescriptor ErrorBars(string name, IEnumerable<ChartErrorValue> values)
        {
            EnsureAdvancedMode();
            ChartErrorValue[] materialized = RequiredFinite(values, value => value.Value, value => value.ErrorMinus, value => value.ErrorPlus);
            if (materialized.Any(value => value.CategoryIndex < 0 || value.ErrorMinus < 0f || value.ErrorPlus < 0f)) throw new ArgumentException("Error bars require non-negative categories and errors.", nameof(values));
            var series = new ErrorBarSeries { Name = RequiredName(name), Symmetric = false };
            foreach (var value in materialized) series.Points.Add(new ErrorBarPoint { CategoryIndex = value.CategoryIndex, Y = value.Value, ErrorMinus = value.ErrorMinus, ErrorPlus = value.ErrorPlus });
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width, axis => series.YAxisIndex = axis).AsErrorBars(series);
        }

        public IHistogramChartSeriesDescriptor Histogram(string name, IEnumerable<float> samples)
        {
            EnsureAdvancedMode();
            var series = new HistogramSeries { Name = RequiredName(name) };
            series.Samples.AddRange(Materialize(samples));
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width, axis => series.YAxisIndex = axis).AsHistogram(series);
        }

        public IBoxPlotChartSeriesDescriptor BoxPlot(string name, IEnumerable<ChartBoxGroup> groups)
        {
            EnsureAdvancedMode();
            ChartBoxGroup[] materialized = (groups ?? throw new ArgumentNullException(nameof(groups))).ToArray();
            if (materialized.Any(group => group.CategoryIndex < 0 || group.Values == null || group.Values.Count == 0 || group.Values.Any(value => !float.IsFinite(value))))
                throw new ArgumentException("Box-plot groups require a non-negative category and finite values.", nameof(groups));
            var series = new BoxPlotSeries { Name = RequiredName(name) };
            foreach (var group in materialized) series.Groups.Add((group.CategoryIndex, group.Values.ToArray()));
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width, axis => series.YAxisIndex = axis).AsBoxPlot(series);
        }

        public IHeatmapChartSeriesDescriptor Heatmap(string name, float[,] values)
        {
            EnsureAdvancedMode();
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length == 0 || values.Cast<float>().Any(value => !float.IsFinite(value))) throw new ArgumentException("A heatmap requires finite values.", nameof(values));
            var series = new HeatmapSeries { Name = RequiredName(name), Rows = values.GetLength(0), Cols = values.GetLength(1), Values = (float[,])values.Clone() };
            Chart.Series.Add(series);
            return new AdvancedDescriptor(series, color => series.Stroke = color, width => series.StrokeWidth = width, axis => series.YAxisIndex = axis).AsHeatmap(series);
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
            EnsureCoreMode();
            var series = new CanonicalBarSeries(RequiredName(name), Materialize(values), stack, normalise);
            _model.Series.Add(series);
            return new BarDescriptor(series, _theme);
        }

        private IPieChartSeriesDescriptor AddPie(string name, IEnumerable<ChartValue> values, float innerRatio)
        {
            EnsureCoreMode();
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
            SyncLegacyPalette();
        }

        private void EnsureCoreMode()
        {
            if (_advancedMode) throw new InvalidOperationException("Core and advanced chart series cannot be mixed in one chart. Use separate chart containers.");
        }

        private void EnsureAdvancedMode()
        {
            if (_model.Series.Count > 0) throw new InvalidOperationException("Core and advanced chart series cannot be mixed in one chart. Use separate chart containers.");
            if (_advancedMode) return;
            _advancedMode = true;
            Chart.CanonicalModel = null;
            SyncLegacyPalette();
            SyncAxis(_model.XAxis, Chart.XAxis);
            SyncAxis(_model.YAxis, Chart.YAxis);
        }

        private void SyncLegacyPalette()
        {
            Chart.Palette.Clear();
            Chart.Palette.AddRange(_model.Palette.Select(ToDrawingColor));
        }

        private static void SyncAxis(CanonicalChartAxis source, Axis target)
        {
            target.Min = source.Minimum;
            target.Max = source.Maximum;
            target.TicksDesired = source.DesiredTicks;
            target.Format = source.Formatter;
        }

        private static T[] RequiredFinite<T>(IEnumerable<T> values, params Func<T, float>[] selectors)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            T[] result = values.ToArray();
            if (result.Any(value => selectors.Any(selector => !float.IsFinite(selector(value))))) throw new ArgumentException("Chart values must be finite.", nameof(values));
            return result;
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
        private static System.Drawing.Color ToDrawingColor(PdfColor value) => System.Drawing.Color.FromArgb(value.Alpha, value.Red, value.Green, value.Blue);

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

        private sealed class AdvancedDescriptor(
            object series,
            Action<System.Drawing.Color> setStroke,
            Action<float> setStrokeWidth,
            Action<int>? setAxis = null) :
            IBubbleChartSeriesDescriptor,
            IWaterfallChartSeriesDescriptor,
            IRadarChartSeriesDescriptor,
            IFunnelChartSeriesDescriptor,
            IGanttChartSeriesDescriptor,
            ICandlestickChartSeriesDescriptor,
            IBulletChartSeriesDescriptor,
            IParetoChartSeriesDescriptor,
            IRangeAreaChartSeriesDescriptor,
            IErrorBarChartSeriesDescriptor,
            IHistogramChartSeriesDescriptor,
            IBoxPlotChartSeriesDescriptor,
            IHeatmapChartSeriesDescriptor
        {
            public IBubbleChartSeriesDescriptor AsBubble(BubbleSeries _) => this;
            public IWaterfallChartSeriesDescriptor AsWaterfall(WaterfallSeries _) => this;
            public IRadarChartSeriesDescriptor AsRadar(RadarSeries _) => this;
            public IFunnelChartSeriesDescriptor AsFunnel(FunnelSeries _) => this;
            public IGanttChartSeriesDescriptor AsGantt(GanttSeries _) => this;
            public ICandlestickChartSeriesDescriptor AsCandlestick(CandleSeries _) => this;
            public IBulletChartSeriesDescriptor AsBullet(BulletSeries _) => this;
            public IParetoChartSeriesDescriptor AsPareto(ParetoSeries _) => this;
            public IRangeAreaChartSeriesDescriptor AsRangeArea(RangeAreaSeries _) => this;
            public IErrorBarChartSeriesDescriptor AsErrorBars(ErrorBarSeries _) => this;
            public IHistogramChartSeriesDescriptor AsHistogram(HistogramSeries _) => this;
            public IBoxPlotChartSeriesDescriptor AsBoxPlot(BoxPlotSeries _) => this;
            public IHeatmapChartSeriesDescriptor AsHeatmap(HeatmapSeries _) => this;

            public void Stroke(PdfColor color, float width = 0.5f)
            {
                setStroke(ToDrawingColor(color));
                setStrokeWidth(Positive(width, nameof(width)));
            }

            public void SecondaryAxis(bool enabled = true)
            {
                if (setAxis == null && enabled) throw new InvalidOperationException("This chart series does not support a secondary axis.");
                setAxis?.Invoke(enabled ? 1 : 0);
            }

            public void Radius(float minimum, float maximum)
            {
                if (series is not BubbleSeries bubble) throw Unsupported();
                bubble.MinRadius = Positive(minimum, nameof(minimum));
                bubble.MaxRadius = Positive(maximum, nameof(maximum));
                if (bubble.MinRadius > bubble.MaxRadius) throw new ArgumentException("The minimum bubble radius cannot exceed the maximum.");
            }

            public void Colors(PdfColor positive, PdfColor negative, PdfColor total)
            {
                switch (series)
                {
                    case WaterfallSeries waterfall:
                        waterfall.PositiveFill = ToDrawingColor(positive);
                        waterfall.NegativeFill = ToDrawingColor(negative);
                        waterfall.TotalFill = ToDrawingColor(total);
                        break;
                    case CandleSeries candle:
                        candle.UpFill = ToDrawingColor(positive);
                        candle.DownFill = ToDrawingColor(negative);
                        candle.WickStroke = ToDrawingColor(total);
                        break;
                    default: throw Unsupported();
                }
            }

            public void Fill(PdfColor color)
            {
                System.Drawing.Color drawing = ToDrawingColor(color);
                switch (series)
                {
                    case RadarSeries radar: radar.Fill = drawing; break;
                    case RangeAreaSeries range: range.Fill = drawing; break;
                    case HistogramSeries histogram: histogram.Fill = drawing; break;
                    case BoxPlotSeries box: box.Fill = drawing; break;
                    default: throw Unsupported();
                }
            }

            public void Range(float? minimum = null, float? maximum = null)
            {
                if (minimum.HasValue && !float.IsFinite(minimum.Value) || maximum.HasValue && !float.IsFinite(maximum.Value) || minimum.HasValue && maximum.HasValue && minimum.Value >= maximum.Value)
                    throw new ArgumentException("The range must be finite and its minimum must be less than its maximum.");
                switch (series)
                {
                    case RadarSeries radar: radar.Min = minimum; radar.Max = maximum; break;
                    case HeatmapSeries heatmap: heatmap.Min = minimum; heatmap.Max = maximum; break;
                    default: throw Unsupported();
                }
            }

            public void Tapered(bool enabled = true)
            {
                if (series is not FunnelSeries funnel) throw Unsupported();
                funnel.Tapered = enabled;
            }

            public void Gap(float value)
            {
                switch (series)
                {
                    case FunnelSeries funnel: funnel.Gap = value < 0f || !float.IsFinite(value) ? throw new ArgumentOutOfRangeException(nameof(value)) : value; break;
                    case HistogramSeries histogram: histogram.BarGapRatio = value < 0f || value >= 1f || !float.IsFinite(value) ? throw new ArgumentOutOfRangeException(nameof(value)) : value; break;
                    default: throw Unsupported();
                }
            }

            public void Geometry(float rowGap, float barHeightRatio)
            {
                if (series is not GanttSeries gantt) throw Unsupported();
                gantt.RowGap = rowGap < 0f || !float.IsFinite(rowGap) ? throw new ArgumentOutOfRangeException(nameof(rowGap)) : rowGap;
                gantt.BarHeightRatio = barHeightRatio <= 0f || barHeightRatio > 1f || !float.IsFinite(barHeightRatio) ? throw new ArgumentOutOfRangeException(nameof(barHeightRatio)) : barHeightRatio;
            }

            public void ValueColor(PdfColor color)
            {
                if (series is not BulletSeries bullet) throw Unsupported();
                bullet.ValueFill = ToDrawingColor(color);
            }

            public void TargetStyle(PdfColor color, float width = 1f)
            {
                if (series is not BulletSeries bullet) throw Unsupported();
                bullet.TargetStroke = ToDrawingColor(color);
                bullet.TargetStrokeWidth = Positive(width, nameof(width));
            }

            public void BarColor(PdfColor color)
            {
                if (series is not ParetoSeries pareto) throw Unsupported();
                pareto.BarFill = ToDrawingColor(color);
            }

            public void CumulativeStyle(PdfColor color, float width = 1f)
            {
                if (series is not ParetoSeries pareto) throw Unsupported();
                pareto.CumulativeStroke = ToDrawingColor(color);
                pareto.CumulativeStrokeWidth = Positive(width, nameof(width));
            }

            public void Smooth(bool enabled = true, float tension = 0.45f)
            {
                if (series is not RangeAreaSeries range) throw Unsupported();
                if (!float.IsFinite(tension) || tension < 0f || tension > 1f) throw new ArgumentOutOfRangeException(nameof(tension));
                range.Smooth = enabled;
                range.SmoothTension = tension;
            }

            public void CapWidth(float points)
            {
                if (series is not ErrorBarSeries error) throw Unsupported();
                error.CapWidth = Positive(points, nameof(points));
            }

            public void Bins(int count)
            {
                if (series is not HistogramSeries histogram) throw Unsupported();
                histogram.BinCount = count < 1 ? throw new ArgumentOutOfRangeException(nameof(count)) : count;
            }

            public void BoxWidth(float ratio)
            {
                if (series is not BoxPlotSeries box) throw Unsupported();
                box.BoxWidthRatio = ratio <= 0f || ratio > 1f || !float.IsFinite(ratio) ? throw new ArgumentOutOfRangeException(nameof(ratio)) : ratio;
            }

            public void ColorScale(Func<float, PdfColor> scale)
            {
                if (series is not HeatmapSeries heatmap) throw Unsupported();
                if (scale == null) throw new ArgumentNullException(nameof(scale));
                heatmap.ColorScale = value => ToDrawingColor(scale(value));
            }

            private static InvalidOperationException Unsupported() => new("The requested option is not supported by this advanced chart series type.");
        }
    }
}
