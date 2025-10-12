using PdfBuilder.Elements;
using System;
using System.Drawing;
using static PdfBuilder.Elements.ChartElement;

namespace PdfBuilder.Document
{
    public sealed class ChartBuilder
    {
        private readonly ColumnBuilder _col;
        private readonly ChartElement _chart;

        public ChartBuilder(ColumnBuilder col, float x, float y, float width, float height)
        {
            _col = col ?? throw new ArgumentNullException(nameof(col));
            _chart = new ChartElement(x, y)
            {
                Width = width,
                Height = height
            };
        }

        // ========== Position / Size ==========
        public ChartBuilder X(float x) { _chart.X = x; return this; }
        public ChartBuilder Y(float y) { _chart.Y = y; return this; }
        public ChartBuilder Width(float w) { _chart.Width = w; return this; }
        public ChartBuilder Height(float h) { _chart.Height = h; return this; }

        // ========== Padding ==========
        public ChartBuilder Padding(float top, float right, float bottom, float left)
        { _chart.PaddingTop = top; _chart.PaddingRight = right; _chart.PaddingBottom = bottom; _chart.PaddingLeft = left; return this; }

        // ========== Title ==========
        public ChartBuilder Title(string text) { _chart.Title = text ?? ""; return this; }
        public ChartBuilder TitleFont(string family, float size)
        { _chart.TitleFont = family; _chart.TitleSize = size; return this; }

        // ========== Axes ==========
        public ChartBuilder CategoryX(params string[] labels)
        {
            _chart.XAxis = PdfBuilder.Elements.Axis.Category();
            if (labels != null) _chart.XAxis.Categories.AddRange(labels);
            return this;
        }
        public ChartBuilder NumericX(float? min = null, float? max = null, int ticks = 5, Func<float, string> formatter = null)
        {
            _chart.XAxis = PdfBuilder.Elements.Axis.Numeric();
            _chart.XAxis.Min = min; _chart.XAxis.Max = max;
            _chart.XAxis.TicksDesired = Math.Max(2, ticks);
            if (formatter != null) _chart.XAxis.Format = formatter;
            return this;
        }
        public ChartBuilder XLabelRotation(float deg)
        { _chart.XAxis.LabelRotationDeg = deg; return this; }

        public ChartBuilder NumericY(float? min = null, float? max = null, int ticks = 5, Func<float, string> formatter = null)
        {
            _chart.YAxis = PdfBuilder.Elements.Axis.Numeric();
            _chart.YAxis.Min = min; _chart.YAxis.Max = max;
            _chart.YAxis.TicksDesired = Math.Max(2, ticks);
            if (formatter != null) _chart.YAxis.Format = formatter;
            return this;
        }
        public ChartBuilder SecondaryNumericY(float? min = null, float? max = null, int ticks = 5, Func<float, string> formatter = null)
        {
            _chart.YAxis2 = PdfBuilder.Elements.Axis.Numeric();
            _chart.YAxis2.Min = min; _chart.YAxis2.Max = max;
            _chart.YAxis2.TicksDesired = Math.Max(2, ticks);
            if (formatter != null) _chart.YAxis2.Format = formatter;
            return this;
        }
        public ChartBuilder UseSecondaryYForLast(bool on = true)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is ICartesianSeries cs)
                cs.YAxisIndex = on ? 1 : 0;
            return this;
        }

        // ========== Look / Legend ==========
        public ChartBuilder GridY(bool show = true) { _chart.ShowGridY = show; return this; }
        public ChartBuilder GridX(bool show = true) { _chart.ShowGridX = show; return this; }
        public ChartBuilder Legend(bool show = true) { _chart.ShowLegend = show; return this; }
        public ChartBuilder Axis(Color color, float width = 0.5f) { _chart.AxisColor = color; _chart.AxisWidth = width; return this; }
        public ChartBuilder Grid(Color color) { _chart.GridColor = color; return this; }
        public ChartBuilder LabelsFont(string family, float size) { _chart.Font = family; _chart.FontSize = size; return this; }
        public ChartBuilder LegendPosition(ChartElement.LegendPos pos)
        { _chart.LegendPosition = pos; _chart.ShowLegend = pos != ChartElement.LegendPos.None; return this; }
        public ChartBuilder Palette(params Color[] colors)
        {
            if (colors != null && colors.Length > 0)
            {
                _chart.Palette.Clear();
                _chart.Palette.AddRange(colors);
            }
            return this;
        }

        // ========== Bar options (last BarSeries) ==========
        public ChartBuilder BarCornerRadius(float r)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BarSeries b) b.CornerRadius = Math.Max(0, r);
            return this;
        }
        public ChartBuilder AlternateBarColors(params Color[] colors)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BarSeries b && colors != null && colors.Length > 0)
            { b.BarFills.Clear(); b.BarFills.AddRange(colors); }
            return this;
        }
        public ChartBuilder AlternateEverySecond(Color alt)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BarSeries b) b.AlternateFill = alt;
            return this;
        }
        public ChartBuilder StackBars(string key)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BarSeries b) b.StackKey = key;
            return this;
        }
        public ChartBuilder HorizontalBars(bool on = true)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BarSeries b) b.Horizontal = on;
            return this;
        }
        public ChartBuilder NormalizeBarsTo100(bool on = true)
        {
            if (_chart.Series.Count > 0 && (_chart.Series[^1] is BarSeries b)) b.NormalizeTo100 = on;
            return this;
        }
        public ChartBuilder BarValueLabels(
        bool show = true,
        BarValueLabelPos pos = BarValueLabelPos.OutsideEnd,
        string font = null,
        float size = 8f,
        Color? color = null,
        Func<float, string> formatter = null,
        float padding = 2f)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BarSeries b)
            {
                b.ShowValueLabels = show;
                b.ValueLabelPosition = pos;
                if (!string.IsNullOrWhiteSpace(font)) b.ValueLabelFont = font;
                b.ValueLabelSize = size;
                if (color.HasValue) b.ValueLabelColor = color.Value;
                if (formatter != null) b.ValueFormatter = formatter;
                b.LabelPadding = padding;
            }
            return this;
        }

        // ========== Line options (last LineSeries) ==========
        public ChartBuilder Smooth(bool on = true, float tension = 0.5f)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is LineSeries l) { l.Smooth = on; l.SmoothTension = tension; }
            return this;
        }
        public ChartBuilder StepLine(bool on = true)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is LineSeries l) l.Step = on;
            return this;
        }
        public ChartBuilder StackArea(string key)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is LineSeries l) l.StackKey = key;
            return this;
        }
        public ChartBuilder LineMarkers(bool show = true, float size = 3f, Color? fill = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is LineSeries l)
            { l.ShowMarkers = show; l.MarkerSize = size; l.MarkerFill = fill; }
            return this;
        }
        public ChartBuilder FillUnder(Color fill, float? baseline = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is LineSeries l)
            { l.Area = true; l.AreaFill = fill; if (baseline.HasValue) l.AreaBaseline = baseline.Value; }
            return this;
        }
        public ChartBuilder LineValueLabels(
        bool show = true,
        LineValueLabelPos pos = LineValueLabelPos.Above,
        string font = null,
        float size = 8f,
        Color? color = null,
        Func<System.Drawing.PointF, string> formatter = null,
        float offset = 3f,
        bool onlyLast = false)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is LineSeries l)
            {
                l.ShowValueLabels = show;
                l.ValueLabelPosition = pos;
                if (!string.IsNullOrWhiteSpace(font)) l.ValueLabelFont = font;
                l.ValueLabelSize = size;
                if (color.HasValue) l.ValueLabelColor = color.Value;
                if (formatter != null) l.PointLabelFormatter = formatter;
                l.ValueLabelOffset = offset;
                l.LabelOnlyLast = onlyLast;
            }
            return this;
        }

        // ========== Series helpers (Bars & Lines – existing) ==========
        public ChartBuilder AddBars(string name, Color fill, Color stroke, float strokeW, params float[] values)
        {
            var s = new BarSeries { Name = name, Fill = fill, Stroke = stroke, StrokeWidth = strokeW };
            if (values != null)
                for (int i = 0; i < values.Length; i++)
                    s.Bars.Add((i, values[i]));
            _chart.Series.Add(s);
            return this;
        }
        public ChartBuilder AddLine(string name, Color stroke, float strokeW, params float[] yValues)
        {
            var s = new LineSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, UsesCategoryX = true };
            if (yValues != null)
                for (int i = 0; i < yValues.Length; i++)
                    s.Points.Add(new System.Drawing.PointF(i, yValues[i]));
            _chart.Series.Add(s);
            return this;
        }

        // ========== PIE / DONUT ==========
        public ChartBuilder AddPie(string name, params (string label, float value)[] slices)
        {
            var s = new PieSeries { Name = name };
            if (slices != null)
            {
                for (int i = 0; i < slices.Length; i++)
                    s.Slices.Add(new PieSlice { Label = slices[i].label, Value = slices[i].value });
            }
            _chart.Series.Add(s);
            return this;
        }
        public ChartBuilder PieSlice(string label, float value, Color? fill = null, float explodeRatio = 0f, string customLabel = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p)
                p.Slices.Add(new PieSlice { Label = label ?? "", Value = value, Fill = fill, ExplodeRatio = Math.Max(0, explodeRatio), CustomLabel = customLabel });
            return this;
        }
        public ChartBuilder Donut(float innerRatio)
        { if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p) p.DonutInnerRatio = Math.Max(0, Math.Min(0.95f, innerRatio)); return this; }
        public ChartBuilder PieStartAngle(float deg)
        { if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p) p.StartAngleDeg = deg; return this; }
        public ChartBuilder PieClockwise(bool cw = true)
        { if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p) p.Clockwise = cw; return this; }
        public ChartBuilder PieLabels(bool show = true, bool outside = true, bool leaders = false, string font = "Helvetica", float size = 9f, Color? color = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p)
            {
                p.ShowLabels = show; p.LabelOutside = outside; p.LabelLeaderLines = leaders;
                p.LabelFont = font; p.LabelFontSize = size; if (color.HasValue) p.LabelColor = color.Value;
            }
            return this;
        }
        public ChartBuilder PieAppendPercentages(bool on = true)
        { if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p) p.AppendPercentages = on; return this; }

        public ChartBuilder PieLabelStyle(
            string font = null, float? size = null, Color? color = null,
            float? offset = null, float? padding = null,
            Color? leaderColor = null, float? leaderWidth = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p)
            {
                if (!string.IsNullOrWhiteSpace(font)) p.LabelFont = font;
                if (size.HasValue) p.LabelFontSize = size.Value;
                if (color.HasValue) p.LabelColor = color.Value;
                if (offset.HasValue) p.LabelOffset = offset.Value;
                if (padding.HasValue) p.LabelPadding = padding.Value;
                if (leaderColor.HasValue) p.LeaderLineColor = leaderColor.Value;
                if (leaderWidth.HasValue) p.LeaderLineWidth = leaderWidth.Value;
            }
            return this;
        }
        public ChartBuilder PieSliceStyle(
        string font = null, float? size = null, Color? color = null,
        float? offset = null, float? padding = null, bool? leaders = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is PieSeries p && p.Slices.Count > 0)
            {
                var s = p.Slices[^1];
                if (!string.IsNullOrWhiteSpace(font)) s.LabelFontOverride = font;
                if (size.HasValue) s.LabelSizeOverride = size;
                if (color.HasValue) s.LabelColorOverride = color.Value;
                if (offset.HasValue) s.LabelOffsetOverride = offset;
                if (padding.HasValue) s.LabelPaddingOverride = padding;
                if (leaders.HasValue) s.LabelLeaderLinesOverride = leaders;
            }
            return this;
        }


        // ========== SCATTER ==========
        public ChartBuilder AddScatter(string name, Color stroke, float strokeW, params (float x, float y)[] points)
        {
            var s = new ScatterSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, UsesCategoryX = false };
            if (points != null) foreach (var p in points) s.Points.Add(new System.Drawing.PointF(p.x, p.y));
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder AddScatterCategories(string name, Color stroke, float strokeW, params (int categoryIndex, float y)[] points)
        {
            var s = new ScatterSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, UsesCategoryX = true };
            if (points != null) foreach (var p in points) s.Points.Add(new System.Drawing.PointF(p.categoryIndex, p.y));
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder ScatterMarkers(MarkerShape shape, float size = 4f, Color? fill = null, bool outline = true)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is ScatterSeries s)
            { s.Marker = shape; s.MarkerSize = size; if (fill.HasValue) s.Fill = fill; s.Outline = outline; }
            return this;
        }

        // ========== BUBBLE ==========
        public ChartBuilder AddBubble(string name, Color stroke, float strokeW, params (float x, float y, float size)[] points)
        {
            var s = new BubbleSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, UsesCategoryX = false };
            if (points != null) foreach (var p in points) s.Points.Add(new BubblePoint { X = p.x, Y = p.y, Size = p.size });
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder BubbleCategories(string name, Color stroke, float strokeW, params (int categoryIndex, float y, float size)[] points)
        {
            var s = new BubbleSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, UsesCategoryX = true };
            if (points != null) foreach (var p in points) s.Points.Add(new BubblePoint { X = p.categoryIndex, Y = p.y, Size = p.size });
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder BubbleSizeRange(float minRadius, float maxRadius)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BubbleSeries b)
            { b.MinRadius = Math.Max(0, minRadius); b.MaxRadius = Math.Max(b.MinRadius, maxRadius); }
            return this;
        }
        public ChartBuilder BubbleSizeDomain(float? min, float? max)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BubbleSeries b) { b.SizeDomainMin = min; b.SizeDomainMax = max; }
            return this;
        }

        // Add a single bubble to the last BubbleSeries (with optional color + legend label)
        public ChartBuilder BubblePoint(float x, float y, float size, Color? fill = null, string category = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BubbleSeries b)
                b.Points.Add(new BubblePoint { X = x, Y = y, Size = size, Fill = fill, Category = category });
            return this;
        }

        public ChartBuilder BubbleLabels(bool show = true, string font = "Helvetica", float size = 9f,
                                         Color? color = null, float offset = 6f,
                                         Func<BubblePoint, string> formatter = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BubbleSeries b)
            {
                b.ShowLabels = show;
                b.LabelFont = font;
                b.LabelSize = size;
                if (color.HasValue) b.LabelColor = color.Value;
                b.LabelOffset = offset;
                if (formatter != null) b.LabelFormatter = formatter;
            }
            return this;
        }

        public ChartBuilder BubbleShadow(bool on = true, float dx = 1.4f, float dy = -1.1f, float scale = 1.05f, Color? color = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BubbleSeries b)
            {
                b.ShowShadow = on; b.ShadowDx = dx; b.ShadowDy = dy; b.ShadowScale = scale;
                if (color.HasValue) b.ShadowColor = color.Value;
            }
            return this;
        }

        public ChartBuilder BubbleLegendPerPoint(bool on = true)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BubbleSeries b) b.LegendPerPoint = on;
            return this;
        }

        // ========== RANGE / BAND AREA ==========
        public ChartBuilder AddRangeArea(string name, Color fill, bool usesCategoryX = true)
        {
            var s = new RangeAreaSeries { Name = name, Fill = fill, UsesCategoryX = usesCategoryX };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder RangePoint(int categoryIndex, float low, float high)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is RangeAreaSeries r && r.UsesCategoryX)
                r.Points.Add(new RangePoint { CategoryIndex = categoryIndex, Low = low, High = high });
            return this;
        }
        public ChartBuilder RangePoint(float x, float low, float high)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is RangeAreaSeries r && !r.UsesCategoryX)
                r.Points.Add(new RangePoint { X = x, Low = low, High = high });
            return this;
        }
        public ChartBuilder RangeSmooth(bool on = true, float tension = 0.45f)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is RangeAreaSeries r)
            { r.Smooth = on; r.SmoothTension = tension; }
            return this;
        }

        // NEW: optional crisp outline along the band edges
        public ChartBuilder RangeOutline(Color stroke, float width = 0.6f)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is RangeAreaSeries r)
            { r.Stroke = stroke; r.StrokeWidth = Math.Max(0.25f, width); }
            return this;
        }
        // ========== ERROR BARS ==========
        public ChartBuilder AddErrorBars(string name, Color stroke, float strokeW, bool usesCategoryX = true, bool symmetric = true, float capWidth = 6f)
        {
            var s = new ErrorBarSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, UsesCategoryX = usesCategoryX, Symmetric = symmetric, CapWidth = capWidth };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder ErrorPoint(int categoryIndex, float y, float error)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is ErrorBarSeries e && e.UsesCategoryX && e.Symmetric)
                e.Points.Add(new ErrorBarPoint { CategoryIndex = categoryIndex, Y = y, Error = error });
            return this;
        }
        public ChartBuilder ErrorPoint(float x, float y, float errorMinus, float errorPlus)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is ErrorBarSeries e && !e.UsesCategoryX && !e.Symmetric)
                e.Points.Add(new ErrorBarPoint { X = x, Y = y, ErrorMinus = errorMinus, ErrorPlus = errorPlus });
            return this;
        }

        // ========== WATERFALL ==========
        public ChartBuilder AddWaterfall(string name, Color? positiveFill = null, Color? negativeFill = null, Color? totalFill = null, float gapRatio = 0.15f, float cornerRadius = 0f)
        {
            var s = new WaterfallSeries { Name = name, GapRatio = gapRatio, CornerRadius = cornerRadius };
            if (positiveFill.HasValue) s.PositiveFill = positiveFill.Value;
            if (negativeFill.HasValue) s.NegativeFill = negativeFill.Value;
            if (totalFill.HasValue) s.TotalFill = totalFill.Value;
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder WaterStep(int categoryIndex, float delta, bool isTotal = false)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is WaterfallSeries w)
                w.Steps.Add((categoryIndex, delta, isTotal));
            return this;
        }

        // ========== HISTOGRAM ==========
        public ChartBuilder AddHistogram(string name, Color fill, Color stroke, float strokeW, params float[] samples)
        {
            var s = new HistogramSeries { Name = name, Fill = fill, Stroke = stroke, StrokeWidth = strokeW };
            if (samples != null && samples.Length > 0) s.Samples.AddRange(samples);
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder HistogramBins(int? binCount = null, float? binWidth = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is HistogramSeries h)
            { h.BinCount = binCount; h.BinWidth = binWidth; }
            return this;
        }
        public ChartBuilder HistogramPreBinned(params (float binStart, float binEnd, int count)[] bins)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is HistogramSeries h && bins != null)
                foreach (var b in bins) h.Bins.Add((b.binStart, b.binEnd, b.count));
            return this;
        }
        public ChartBuilder HistogramGap(float gapRatio)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is HistogramSeries h)
                h.BarGapRatio = Math.Max(0f, Math.Min(0.9f, gapRatio));
            return this;
        }

        public ChartBuilder HistogramValueLabels(
            bool show = true,
            string font = null,
            float size = 8f,
            Color? color = null,
            Func<int, string> formatter = null,
            float offset = 3f)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is HistogramSeries h)
            {
                h.ShowLabels = show;
                if (!string.IsNullOrWhiteSpace(font)) h.LabelFont = font;
                h.LabelSize = size;
                if (color.HasValue) h.LabelColor = color.Value;
                if (formatter != null) h.LabelFormatter = formatter;
                h.LabelOffset = offset;
            }
            return this;
        }

        // ========== BOX & WHISKER ==========
        public ChartBuilder AddBoxPlot(string name, Color fill, Color stroke, float strokeW, float boxWidthRatio = 0.7f)
        {
            var s = new BoxPlotSeries { Name = name, Fill = fill, Stroke = stroke, StrokeWidth = strokeW, BoxWidthRatio = boxWidthRatio };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder BoxGroup(int categoryIndex, params float[] values)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BoxPlotSeries b && values != null)
                b.Groups.Add((categoryIndex, values));
            return this;
        }
        public ChartBuilder BoxStats(int categoryIndex, float q1, float median, float q3, float whiskerLow, float whiskerHigh, params float[] outliers)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BoxPlotSeries b)
                b.Stats.Add((categoryIndex, q1, median, q3, whiskerLow, whiskerHigh, outliers != null ? new System.Collections.Generic.List<float>(outliers) : new System.Collections.Generic.List<float>()));
            return this;
        }

        // ========== HEATMAP ==========
        public ChartBuilder AddHeatmap(string name, float[,] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var s = new HeatmapSeries { Name = name, Rows = values.GetLength(0), Cols = values.GetLength(1), Values = values };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder HeatmapDomain(float? min = null, float? max = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is HeatmapSeries h) { h.Min = min; h.Max = max; }
            return this;
        }

        // ========== RADAR ==========
        public ChartBuilder AddRadar(string name, Color stroke, float strokeW, Color? fill = null, params float[] values)
        {
            var s = new RadarSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, Fill = fill };
            if (values != null) for (int i = 0; i < values.Length; i++) s.Points.Add((i, values[i]));
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder RadarScale(float? min = null, float? max = null)
        { if (_chart.Series.Count > 0 && _chart.Series[^1] is RadarSeries r) { r.Min = min; r.Max = max; } return this; }
        public ChartBuilder RadarMarkers(bool show = true, float size = 3f, Color? fill = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is RadarSeries r)
            { r.ShowMarkers = show; r.MarkerSize = size; r.MarkerFill = fill; }
            return this;
        }

        // ========== FUNNEL ==========
        public ChartBuilder AddFunnel(string name, bool tapered = true, float gap = 4f)
        {
            var s = new FunnelSeries { Name = name, Tapered = tapered, Gap = gap };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder FunnelStage(string stage, float value, Color? fill = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is FunnelSeries f)
                f.Stages.Add(new FunnelStage { Stage = stage ?? "", Value = value, Fill = fill });
            return this;
        }
        public ChartBuilder FunnelLabelStyle(string font = "Helvetica", float size = 9f, Color? color = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is FunnelSeries f)
            { f.LabelFont = font; f.LabelFontSize = size; if (color.HasValue) f.LabelColor = color.Value; }
            return this;
        }

        // ========== CANDLESTICK ==========
        public ChartBuilder AddCandles(string name, Color upFill, Color downFill, Color wickStroke, float strokeW = 0.7f, float candleWidthRatio = 0.7f, bool usesCategoryX = true)
        {
            var s = new CandleSeries
            {
                Name = name,
                UpFill = upFill,
                DownFill = downFill,
                WickStroke = wickStroke,
                StrokeWidth = strokeW,
                CandleWidthRatio = candleWidthRatio,
                UsesCategoryX = usesCategoryX
            };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder Candle(int xIndex, float open, float high, float low, float close)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is CandleSeries cs)
                cs.Candles.Add((xIndex, open, high, low, close));
            return this;
        }

        // ========== BULLET ==========
        public ChartBuilder AddBullet(string name, float value, float target)
        {
            var s = new BulletSeries { Name = name, Value = value, Target = target };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder BulletRanges(params (float start, float end, Color fill)[] ranges)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BulletSeries b && ranges != null)
                foreach (var r in ranges) b.QualitativeRanges.Add(r);
            return this;
        }
        public ChartBuilder BulletLook(bool horizontal = true, Color? valueFill = null, Color? targetStroke = null, float? targetStrokeWidth = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is BulletSeries b)
            {
                b.Horizontal = horizontal;
                if (valueFill.HasValue) b.ValueFill = valueFill.Value;
                if (targetStroke.HasValue) b.TargetStroke = targetStroke.Value;
                if (targetStrokeWidth.HasValue) b.TargetStrokeWidth = targetStrokeWidth.Value;
            }
            return this;
        }

        // ========== PARETO ==========
        public ChartBuilder AddPareto(string name, Color barFill, Color cumulativeStroke, float cumulativeStrokeW, bool sortDescending = true)
        {
            var s = new ParetoSeries { Name = name, BarFill = barFill, CumulativeStroke = cumulativeStroke, CumulativeStrokeWidth = cumulativeStrokeW, SortDescending = sortDescending };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder ParetoItem(int categoryIndex, float value)
        { if (_chart.Series.Count > 0 && _chart.Series[^1] is ParetoSeries p) p.Items.Add((categoryIndex, value)); return this; }
        public ChartBuilder ParetoUseRightAxis(bool on = true, Func<float, string> percentFormatter = null)
        {
            // Ensure a secondary axis 0..100 with percent labels
            if (on)
            {
                if (_chart.YAxis2 == null)
                {
                    _chart.YAxis2 = PdfBuilder.Elements.Axis.Numeric();
                    _chart.YAxis2.Min = 0; _chart.YAxis2.Max = 100; _chart.YAxis2.TicksDesired = 5;
                    _chart.YAxis2.Format = percentFormatter ?? (v => $"{v:0}%");
                }
            }
            return this;
        }

        // ========== GANTT ==========
        public ChartBuilder AddGantt(string name, Color stroke, float strokeW, float rowGap = 2f, float barHeightRatio = 0.6f)
        {
            var s = new GanttSeries { Name = name, Stroke = stroke, StrokeWidth = strokeW, RowGap = rowGap, BarHeightRatio = barHeightRatio };
            _chart.Series.Add(s); return this;
        }
        public ChartBuilder GanttTask(int categoryIndex, float startX, float endX, string label = null, Color? fill = null, Color? stroke = null)
        {
            if (_chart.Series.Count > 0 && _chart.Series[^1] is GanttSeries g)
                g.Tasks.Add(new GanttTask { CategoryIndex = categoryIndex, StartX = startX, EndX = endX, Label = label ?? "", Fill = fill, Stroke = stroke });
            return this;
        }

        // ========== Advanced: add a fully-built series ==========
        public ChartBuilder AddSeries(IChartSeries series) { if (series != null) _chart.Series.Add(series); return this; }

        // ========== Commit ==========
        public float Add()
        {
            return _col.AddChart(_chart);
        }
    }
}
