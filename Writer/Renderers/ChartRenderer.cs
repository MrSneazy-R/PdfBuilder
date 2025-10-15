// PdfBuilder/Writer/ChartRenderer.cs
using PdfBuilder.Elements;
using PdfBuilder.Encoder;
using PdfBuilder.TextShaping;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using static PdfBuilder.Elements.ChartElement;

namespace PdfBuilder.Writer
{
    internal static class ChartRenderer
    {
        private static readonly IFormatProvider Inv = System.Globalization.CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);
        private static float AlignHalf(float v) => (float)Math.Round(v * 2f) / 2f;

        public static void Append(StringBuilder sb, ChartElement c, PdfRenderContext context)
        {
            if (c == null) return;
            var pieLeaderLines = new List<(Color c, float w, float x0, float y0, float x1, float y1, float x2, float y2)>();
            var pieTexts = new List<(string font, float size, Color color, float x, float y, string text)>();

            // Partition series by type
            var allLines = c.Series.OfType<LineSeries>().ToList();
            var allBars = c.Series.OfType<BarSeries>().ToList();
            var allScatter = c.Series.OfType<ScatterSeries>().ToList();
            var allBubble = c.Series.OfType<BubbleSeries>().ToList();
            var allRangeArea = c.Series.OfType<RangeAreaSeries>().ToList();
            var allErr = c.Series.OfType<ErrorBarSeries>().ToList();
            var allWaterfall = c.Series.OfType<WaterfallSeries>().ToList();
            var allHist = c.Series.OfType<HistogramSeries>().ToList();
            var allBox = c.Series.OfType<BoxPlotSeries>().ToList();
            var allHeat = c.Series.OfType<HeatmapSeries>().ToList();
            var allRadar = c.Series.OfType<RadarSeries>().ToList();
            var allFunnel = c.Series.OfType<FunnelSeries>().ToList();
            var allCandle = c.Series.OfType<CandleSeries>().ToList();
            var allBullet = c.Series.OfType<BulletSeries>().ToList();
            var allPareto = c.Series.OfType<ParetoSeries>().ToList();
            var allGantt = c.Series.OfType<GanttSeries>().ToList();
            var allPie = c.Series.OfType<PieSeries>().ToList();

            bool anyCartesian =
                allLines.Any() || allBars.Any() || allScatter.Any() || allBubble.Any()
                || allRangeArea.Any() || allErr.Any() || allWaterfall.Any()
                || allHist.Any() || allBox.Any() || allHeat.Any() || allCandle.Any()
                || allPareto.Any() || allGantt.Any();

            bool anyRadial = allPie.Any() || allRadar.Any() || allFunnel.Any() || allBullet.Any();

            sb.Append("q\n");

           


            sb.Append("0 J 0 j\n");

            // Title
            float yTop = c.Y;
            if (!string.IsNullOrWhiteSpace(c.Title))
            {
                string titleFont = string.IsNullOrWhiteSpace(c.TitleFont) ? c.Font : c.TitleFont;
                DrawText(sb, context, titleFont, c.TitleSize, Color.Black, c.X, yTop, c.Title);
                yTop -= c.TitleSize * 1.2f;
            }

            // Plot rect
            float plotX = c.X + c.PaddingLeft;
            float plotYTop = yTop - c.PaddingTop;
            float plotW = Math.Max(1, c.Width - (c.PaddingLeft + c.PaddingRight));
            float plotH = Math.Max(1, c.Height - (c.PaddingTop + c.PaddingBottom));
            float plotBottom = plotYTop - plotH;
            string axisFont = string.IsNullOrWhiteSpace(c.Font) ? "Helvetica" : c.Font;

            // Category handling (default: categories on X)
            bool categoriesOnY =
                allBars.Any(b => b.Horizontal) || allGantt.Any(); // basic switch; others keep default

            // Build category count (from X or Y depending on switch)
            int catCount = 0;
            if (c.XAxis.IsCategory)
            {
                if (!categoriesOnY)
                {
                    catCount = c.XAxis.Categories.Count;
                    if (catCount == 0)
                    {
                        catCount = Math.Max(
                            Math.Max(allBars.SelectMany(b => b.Bars).Select(t => t.categoryIndex + 1).DefaultIfEmpty(0).Max(),
                                     allLines.Where(l => l.UsesCategoryX).SelectMany(l => l.Points).Select(p => (int)p.X + 1).DefaultIfEmpty(0).Max()),
                            Math.Max(allScatter.Where(s => s.UsesCategoryX).SelectMany(s => s.Points).Select(p => (int)p.X + 1).DefaultIfEmpty(0).Max(),
                                     allBubble.Where(s => s.UsesCategoryX).SelectMany(s => s.Points).Select(p => (int)p.X + 1).DefaultIfEmpty(0).Max())
                        );
                    }
                }
                else
                {
                    // categories on Y (horizontal bars/gantt)
                    catCount = Math.Max(
                        Math.Max(allBars.SelectMany(b => b.Bars).Select(t => t.categoryIndex + 1).DefaultIfEmpty(0).Max(),
                                 allGantt.SelectMany(g => g.Tasks).Select(t => t.CategoryIndex + 1).DefaultIfEmpty(0).Max()),
                        c.XAxis.Categories.Count > 0 ? c.XAxis.Categories.Count : 0);
                }
            }

            // Numeric X domain (for scatter/bubble/line with numeric X, rangearea, gantt)
            float xMin = 0, xMax = 1;
            {
                var vals = new List<float>();
                // Lines numeric X
                foreach (var s in allLines.Where(l => !l.UsesCategoryX)) vals.AddRange(s.Points.Select(p => p.X));
                // Scatter/Bubble numeric X
                foreach (var s in allScatter.Where(p => !p.UsesCategoryX)) vals.AddRange(s.Points.Select(p => p.X));
                foreach (var s in allBubble.Where(p => !p.UsesCategoryX)) vals.AddRange(s.Points.Select(p => p.X));
                // Range/band numeric X
                foreach (var s in allRangeArea.Where(r => !r.UsesCategoryX)) vals.AddRange(s.Points.Select(p => p.X));
                // Gantt numeric X domain (time)
                foreach (var g in allGantt) { vals.AddRange(g.Tasks.Select(t => t.StartX)); vals.AddRange(g.Tasks.Select(t => t.EndX)); }
                // Histogram bins/samples are independent (draw without axis labels)
                if (vals.Count > 0)
                {
                    xMin = vals.Min();
                    xMax = vals.Max();
                    if (Math.Abs(xMax - xMin) < 1e-6f) { xMin -= 0.5f; xMax += 0.5f; }
                }
            }
            // --- Nice ticks for numeric X (scatter/bubble/line/etc.) ---
            float tickMinX = xMin, tickMaxX = xMax, tickStepX = 1f;
            List<float> xTicks = new();

            if (!c.XAxis.IsCategory)
            {
                float domMin = c.XAxis.Min ?? xMin;
                float domMax = c.XAxis.Max ?? xMax;
                if (Math.Abs(domMax - domMin) < 1e-6f) { domMin -= 0.5f; domMax += 0.5f; }

                var (nm, xM, st) = NiceTicks(domMin, domMax, Math.Max(2, c.XAxis.TicksDesired));
                tickMinX = nm; tickMaxX = xM; tickStepX = st;

                for (float t = tickMinX; t <= tickMaxX + 1e-6f; t += tickStepX) xTicks.Add(t);
            }

            // Y domains for primary & secondary axis (only for cartesian)
            float yMin1 = 0, yMax1 = 1, yMin2 = 0, yMax2 = 1;
            List<float> yCollect0 = new(), yCollect1 = new();

            if (anyCartesian)
            {
                void CollectY(int axisIndex, float v)
                {
                    if (axisIndex == 0) yCollect0.Add(v); else yCollect1.Add(v);
                }

                // Bars (vertical or horizontal)
                foreach (var b in allBars)
                {
                    foreach (var t in b.Bars)
                    {
                        if (b.Horizontal) CollectY(b.YAxisIndex, t.categoryIndex); // Y is categorical row index (just ensure range non-empty)
                        else CollectY(b.YAxisIndex, t.value);
                    }
                }
                // Lines
                foreach (var l in allLines)
                    foreach (var p in l.Points) CollectY(l.YAxisIndex, p.Y);
                // Scatter/Bubble
                foreach (var s in allScatter) foreach (var p in s.Points) CollectY(s.YAxisIndex, p.Y);
                foreach (var s in allBubble) foreach (var p in s.Points) CollectY(s.YAxisIndex, p.Y);
                // RangeArea
                foreach (var r in allRangeArea) foreach (var p in r.Points) { CollectY(r.YAxisIndex, p.Low); CollectY(r.YAxisIndex, p.High); }
                // Error bars
                foreach (var e in allErr) foreach (var p in e.Points) { CollectY(e.YAxisIndex, p.Y - (e.Symmetric ? p.Error : p.ErrorMinus)); CollectY(e.YAxisIndex, p.Y + (e.Symmetric ? p.Error : p.ErrorPlus)); }
                // Waterfall
                foreach (var w in allWaterfall) foreach (var s in w.Steps) CollectY(w.YAxisIndex, s.delta);
                // Histogram -> Y is counts
                foreach (var h in allHist)
                {
                    if (h.Bins.Count > 0)
                    {
                        foreach (var b in h.Bins) CollectY(h.YAxisIndex, b.count);
                    }
                    else if (h.Samples.Count > 0)
                    {
                        // compute bins just to get max counts for Y domain
                        float mn = h.Samples.Min(), mx = h.Samples.Max();
                        if (Math.Abs(mx - mn) < 1e-6f) { mn -= 0.5f; mx += 0.5f; }

                        int k = h.BinCount ?? 10;
                        float bw = h.BinWidth ?? ((mx - mn) / k);
                        if (bw <= 0) bw = (mx - mn) / Math.Max(1, k);

                        int nb = (int)Math.Ceiling((mx - mn) / bw);
                        var counts = new int[nb];

                        foreach (var v in h.Samples)
                        {
                            int bi = (int)Math.Floor((v - mn) / bw);
                            if (bi < 0) bi = 0; if (bi >= nb) bi = nb - 1;
                            counts[bi]++;
                        }

                        foreach (var cnt in counts) CollectY(h.YAxisIndex, cnt);
                    }
                }

                // BoxPlot
                foreach (var bx in allBox)
                {
                    if (bx.Stats.Count > 0) foreach (var s in bx.Stats) { CollectY(bx.YAxisIndex, s.whiskerLow); CollectY(bx.YAxisIndex, s.whiskerHigh); }
                    foreach (var g in bx.Groups) foreach (var v in g.values) CollectY(bx.YAxisIndex, v);
                }
                // Heatmap -> Values; we’ll scale inside renderer (no axis)
                // Candle -> use high/low
                foreach (var cs in allCandle) foreach (var k in cs.Candles) { CollectY(cs.YAxisIndex, k.low); CollectY(cs.YAxisIndex, k.high); }
                // Pareto -> bars’ values on primary; cumulative on secondary (0..100)
                foreach (var pr in allPareto) foreach (var it in pr.Items) CollectY(pr.YAxisIndex, it.value);
                // Gantt -> rows (categorical Y), numeric X independent

                // Finalize
                (float tMin, float tMax) GetDomain(List<float> arr, Axis axis)
                {
                    if (arr.Count == 0) return (axis.Min ?? 0, axis.Max ?? 1);
                    float mn = axis.Min ?? arr.Min();
                    float mx = axis.Max ?? arr.Max();
                    if (Math.Abs(mx - mn) < 1e-6f) { mn -= 0.5f; mx += 0.5f; }
                    return (mn, mx);
                }

                (yMin1, yMax1) = GetDomain(yCollect0, c.YAxis);
                if (c.YAxis2 != null) (yMin2, yMax2) = GetDomain(yCollect1, c.YAxis2);
            }

            // Nice ticks for Y1 (+ Y2 if present)
            var (tickMin1, tickMax1, tickStep1) = NiceTicks(yMin1, yMax1, Math.Max(2, c.YAxis.TicksDesired));
            var yTicks1 = new List<float>();
            for (float t = tickMin1; t <= tickMax1 + 1e-6; t += tickStep1) yTicks1.Add(t);

            List<float> yTicks2 = new();
            float tickMin2 = 0, tickMax2 = 1;
            if (c.YAxis2 != null)
            {
                var (mn2, mx2, st2) = NiceTicks(yMin2, yMax2, Math.Max(2, c.YAxis2.TicksDesired));
                tickMin2 = mn2; tickMax2 = mx2;
                for (float t = mn2; t <= mx2 + 1e-6; t += st2) yTicks2.Add(t);
            }

            // Mapping helpers
            float YtoPx(float v, int yAxisIndex)
            {
                if (yAxisIndex == 1 && c.YAxis2 != null)
                    return plotBottom + (v - tickMin2) / (tickMax2 - tickMin2) * plotH;
                return plotBottom + (v - tickMin1) / (tickMax1 - tickMin1) * plotH;
            }
            float XBandLeft(int i, int n) => n <= 0 ? plotX : plotX + i * (plotW / n);
            float XBandWidth(int n) => n <= 0 ? plotW : (plotW / n);
            float XtoPxNumeric(float x)
             => (tickMaxX - tickMinX) == 0 ? plotX : plotX + (x - tickMinX) / (tickMaxX - tickMinX) * plotW;


            // ===== Axes & grid (cartesian only) =====
            if (anyCartesian)
            {
                // Grid lines
                if (!categoriesOnY)
                {
                    if (c.ShowGridY)
                    {
                        foreach (var t in yTicks1)
                        {
                            float y = AlignHalf(YtoPx(t, 0));
                            sb.Append($"{ToRgbStroke(c.GridColor)} 0.5 w {N(plotX)} {N(y)} m {N(plotX + plotW)} {N(y)} l S\n");
                        }
                    }
                }
                else
                {
                    // categories on Y: draw horizontal grid per row
                    if (c.ShowGridY && catCount > 0)
                    {
                        for (int i = 0; i <= catCount; i++)
                        {
                            float y = AlignHalf(plotYTop - i * (plotH / catCount));
                            sb.Append($"{ToRgbStroke(c.GridColor)} 0.5 w {N(plotX)} {N(y)} m {N(plotX + plotW)} {N(y)} l S\n");
                        }
                    }
                }

                // Axes lines
                // bottom x-axis
                sb.Append($"{ToRgbStroke(c.AxisColor)} {N(c.AxisWidth)} w {N(plotX)} {N(AlignHalf(plotBottom))} m {N(plotX + plotW)} {N(AlignHalf(plotBottom))} l S\n");
                // left y-axis
                sb.Append($"{ToRgbStroke(c.AxisColor)} {N(c.AxisWidth)} w {N(AlignHalf(plotX))} {N(plotYTop)} m {N(AlignHalf(plotX))} {N(plotBottom)} l S\n");
                // right y2-axis (if present)
                if (c.YAxis2 != null)
                {
                    float rx = AlignHalf(plotX + plotW);
                    sb.Append($"{ToRgbStroke(c.AxisColor)} {N(c.AxisWidth)} w {N(rx)} {N(plotYTop)} m {N(rx)} {N(plotBottom)} l S\n");
                }

                // Y1 tick labels
                foreach (var t in yTicks1)
                {
                    string s = c.YAxis.Format(t);
                    float ty = YtoPx(t, 0);
                    DrawText(sb, context, axisFont, c.FontSize, c.AxisColor, c.X + 2, ty - c.FontSize * 0.35f, s);
                }

                // Y2 tick labels (right)
                if (c.YAxis2 != null)
                {
                    foreach (var t in yTicks2)
                    {
                        string s = c.YAxis2.Format(t);
                        float ty = YtoPx(t, 1);
                        float tx = plotX + plotW + 2;
                        DrawText(sb, context, axisFont, c.FontSize, c.AxisColor, tx, ty - c.FontSize * 0.35f, s);
                    }
                }

                // Category labels
                if (c.XAxis.IsCategory && catCount > 0)
                {
                    if (!categoriesOnY)
                    {
                        float band = XBandWidth(catCount);
                        for (int i = 0; i < catCount; i++)
                        {
                            string lab = i < c.XAxis.Categories.Count ? c.XAxis.Categories[i] : (i + 1).ToString();
                            float cx = XBandLeft(i, catCount) + band / 2f;
                            DrawTextRot(sb, context, axisFont, c.FontSize, c.AxisColor, cx, plotBottom - 12, lab, c.XAxis.LabelRotationDeg, hCenter: true);
                            if (c.ShowGridX)
                            {
                                float gx = AlignHalf(XBandLeft(i, catCount));
                                sb.Append($"{ToRgbStroke(c.GridColor)} 0.5 w {N(gx)} {N(plotYTop)} m {N(gx)} {N(plotBottom)} l S\n");
                            }
                        }
                    }
                    else
                    {
                        // categories on Y: draw labels on left side per row
                        float bandH = plotH / catCount;
                        for (int i = 0; i < catCount; i++)
                        {
                            string lab = i < c.XAxis.Categories.Count ? c.XAxis.Categories[i] : (i + 1).ToString();
                            float cy = plotYTop - (i + 0.5f) * bandH;
                            DrawText(sb, context, axisFont, c.FontSize, c.AxisColor, c.X + 2, cy - c.FontSize * 0.35f, lab);
                        }
                    }
                }
            }

            // ===== Clip to plot for data =====
            sb.Append("q ");
            sb.Append($"{N(plotX)} {N(plotBottom)} {N(plotW)} {N(plotH)} re W n\n");


            foreach (var ls in allLines)
            {
                var pts = ls.Points;
                if (!(ls.Area && ls.AreaFill.HasValue) || pts.Count < 2) continue;

                var mapped = MapLinePoints(
                    pts, ls.UsesCategoryX, catCount, plotW,
                    XBandLeft, XBandWidth, XtoPxNumeric,
                    v => YtoPx(v, ls.YAxisIndex));

                float baseVal = float.IsNaN(ls.AreaBaseline) ? tickMin1 : ls.AreaBaseline;
                float by = YtoPx(baseVal, ls.YAxisIndex);

                sb.Append($"{ToRgbFill(ls.AreaFill.Value)} ");
                if (ls.Smooth) AppendSmoothPath(sb, mapped, Math.Clamp(ls.SmoothTension, 0f, 1f));
                else
                {
                    sb.Append($"{N(mapped[0].x)} {N(mapped[0].y)} m ");
                    for (int i = 1; i < mapped.Count; i++) sb.Append($"{N(mapped[i].x)} {N(mapped[i].y)} l ");
                }
                sb.Append($"{N(mapped[^1].x)} {N(by)} l {N(mapped[0].x)} {N(by)} l h f\n");
            }

            // ===== RENDER: Non-cartesian first (use same plot rect) =====

            // PIE / DONUT
            foreach (var ps in allPie)
            {
                // center & radius
                float cx = plotX + plotW / 2f, cy = plotBottom + plotH / 2f;
                float R = 0.5f * Math.Min(plotW, plotH) * 0.92f;
                float rInner = ps.DonutInnerRatio <= 0 ? 0 : Math.Clamp(ps.DonutInnerRatio, 0f, 0.95f) * R;

                float total = ps.Slices.Sum(s => Math.Max(0, s.Value));
                if (total <= 0) continue;

                float ang = ps.StartAngleDeg * (float)(Math.PI / 180.0);
                float dir = ps.Clockwise ? 1f : -1f;

                for (int i = 0; i < ps.Slices.Count; i++)
                {
                    var s = ps.Slices[i];
                    if (s.Value <= 0) continue;

                    float frac = s.Value / total;
                    float sweep = frac * 2f * (float)Math.PI * dir;
                    float a0 = ang;
                    float a1 = ang + sweep;

                    // explode offset
                    float mid = (a0 + a1) / 2f;
                    float explode = Math.Max(0, s.ExplodeRatio) * R;
                    float offX = (float)Math.Cos(mid) * explode;
                    float offY = (float)Math.Sin(mid) * explode;

                    var fill = s.Fill ?? c.Palette[i % c.Palette.Count];

                    // draw wedge (annular sector if rInner>0)
                    FillAnnularSector(sb, cx + offX, cy + offY, rInner, R, a0, a1, fill);
                    if (ps.StrokeWidth > 0.01f) StrokeAnnularSectorOutline(sb, cx + offX, cy + offY, rInner, R, a0, a1, ps.Stroke, ps.StrokeWidth);

                    // label
                    if (ps.ShowLabels)
                    {
                        float midd = (a0 + a1) / 2f;
                        bool right = MathF.Cos(midd) >= 0f;

                        // --- per-slice overrides (fallback to series defaults) ---
                        string fontName = string.IsNullOrWhiteSpace(s.LabelFontOverride) ? ps.LabelFont : s.LabelFontOverride;
                        float fontSize = s.LabelSizeOverride ?? ps.LabelFontSize;
                        Color fontColor = s.LabelColorOverride ?? ps.LabelColor;
                        float labOffset = s.LabelOffsetOverride ?? ps.LabelOffset;
                        float labPad = s.LabelPaddingOverride ?? ps.LabelPadding;
                        bool leaders = s.LabelLeaderLinesOverride ?? ps.LabelLeaderLines;

                        // anchor radius
                        float labelR = rInner > 0 && ps.LabelOutside
                            ? R + labOffset
                            : (rInner > 0 ? (rInner + R) / 2f : R * 0.65f);

                        float ax = cx + offX + MathF.Cos(midd) * labelR;
                        float ay = cy + offY + MathF.Sin(midd) * labelR;

                        // text
                        string txt = s.CustomLabel ?? ps.LabelFormatter(s);
                        if (ps.AppendPercentages)
                        {
                            float pct = (s.Value / total) * 100f;
                            txt = string.IsNullOrWhiteSpace(txt) ? $"{pct:0.#}%" : $"{txt} ({pct:0.#}%)";
                        }

                        float effectiveFontSize = fontSize > 0 ? fontSize : ps.LabelFontSize;
                        string resolvedFont = string.IsNullOrWhiteSpace(fontName) ? axisFont : fontName;
                        float tw = MeasureTextWidth(txt, resolvedFont, effectiveFontSize);
                        float tx = right ? ax + labPad : ax - tw - labPad;
                        float ty = ay - effectiveFontSize * 0.35f;

                        // optional leader line to the text edge
                        if (ps.LabelOutside && leaders)
                        {
                            float innerR = R * 0.95f;
                            float x0 = cx + offX + MathF.Cos(midd) * innerR;
                            float y0 = cy + offY + MathF.Sin(midd) * innerR;
                            float x1 = ax, y1 = ay;
                            float x2 = right ? tx : tx + tw, y2 = ay;

                            pieLeaderLines.Add((ps.LeaderLineColor, Math.Max(0.25f, ps.LeaderLineWidth),
                                                x0, y0, x1, y1, x2, y2));
                        }

                        // cache draw
                        pieTexts.Add((resolvedFont, effectiveFontSize, fontColor, tx, ty, txt));
                    }



                    ang = a1;
                }
            }

            // RADAR
            foreach (var rs in allRadar)
            {
                // use categories count if provided, else use series points count
                int n = Math.Max(c.XAxis.Categories.Count, rs.Points.Count);
                if (n < 3) continue;

                float cx = plotX + plotW / 2f, cy = plotBottom + plotH / 2f;
                float R = 0.5f * Math.Min(plotW, plotH) * 0.9f;
                float vmin = rs.Min ?? 0f;
                float vmax = rs.Max ?? Math.Max(1f, rs.Points.Select(p => p.value).DefaultIfEmpty(1f).Max());

                // polygon
                var mapped = new List<(float x, float y)>(n);
                for (int i = 0; i < n; i++)
                {
                    float angle = -MathF.PI / 2f + i * (2f * MathF.PI / n);
                    float val = rs.Points.FirstOrDefault(p => p.categoryIndex == i).value;
                    float r = vmax <= vmin ? 0 : (val - vmin) / (vmax - vmin) * R;
                    mapped.Add((cx + MathF.Cos(angle) * r, cy + MathF.Sin(angle) * r));
                }

                // Fill
                if (rs.Fill.HasValue)
                {
                    sb.Append($"{ToRgbFill(rs.Fill.Value)} ");
                    sb.Append($"{N(mapped[0].x)} {N(mapped[0].y)} m ");
                    for (int i = 1; i < mapped.Count; i++) sb.Append($"{N(mapped[i].x)} {N(mapped[i].y)} l ");
                    sb.Append("h f\n");
                }
                // Stroke
                sb.Append($"{ToRgbStroke(rs.Stroke)} {N(Math.Max(0.25f, rs.StrokeWidth))} w ");
                sb.Append($"{N(mapped[0].x)} {N(mapped[0].y)} m ");
                for (int i = 1; i < mapped.Count; i++) sb.Append($"{N(mapped[i].x)} {N(mapped[i].y)} l ");
                if (rs.CloseShape) sb.Append("h ");
                sb.Append("S\n");

                // Markers
                if (rs.ShowMarkers)
                {
                    var fill = rs.MarkerFill ?? Color.White;
                    foreach (var p in mapped)
                    {
                        FillCircle(sb, p.x, p.y, rs.MarkerSize, fill);
                        StrokeCircle(sb, p.x, p.y, rs.MarkerSize, rs.Stroke, 0.5f);
                    }
                }
            }

            // FUNNEL
            foreach (var fs in allFunnel)
            {
                if (fs.Stages.Count == 0) continue;
                float topY = plotYTop - 2;
                float botY = plotBottom + 2;
                float stageH = (topY - botY) / fs.Stages.Count;
                float maxV = Math.Max(1e-6f, fs.Stages.Max(s => s.Value));

                for (int i = 0; i < fs.Stages.Count; i++)
                {
                    var st = fs.Stages[i];
                    float frac = st.Value / maxV;
                    float wTop = frac * plotW;
                    float wBot = i + 1 < fs.Stages.Count && fs.Tapered
                        ? (fs.Stages[i + 1].Value / maxV) * plotW
                        : wTop;

                    float y1 = topY - i * stageH;
                    float y0 = y1 - stageH + fs.Gap;
                    float x1 = plotX + (plotW - wTop) / 2f;
                    float x2 = plotX + (plotW + wTop) / 2f;
                    float x3 = plotX + (plotW + wBot) / 2f;
                    float x4 = plotX + (plotW - wBot) / 2f;

                    var fill = st.Fill ?? c.Palette[i % c.Palette.Count];
                    // trapezoid
                    sb.Append($"{ToRgbFill(fill)} ");
                    sb.Append($"{N(x1)} {N(y1)} m {N(x2)} {N(y1)} l {N(x3)} {N(y0)} l {N(x4)} {N(y0)} l h f\n");
                    sb.Append($"{ToRgbStroke(fs.Stroke)} {N(Math.Max(0.25f, fs.StrokeWidth))} w ");
                    sb.Append($"{N(x1)} {N(y1)} m {N(x2)} {N(y1)} l {N(x3)} {N(y0)} l {N(x4)} {N(y0)} l h S\n");

                    // label (center)
                    string lbl = $"{st.Stage}  {st.Value:0.##}";
                    float cx = plotX + plotW / 2f;
                    float cy = (y1 + y0) / 2f - fs.LabelFontSize * 0.35f;
                    string funnelFont = string.IsNullOrWhiteSpace(fs.LabelFont) ? axisFont : fs.LabelFont;
                    DrawTextCentered(sb, context, funnelFont, fs.LabelFontSize, fs.LabelColor, cx, cy, lbl);
                }
            }

            // BULLET
            foreach (var bs in allBullet)
            {
                // Horizontal bullet only (common case)
                float barH = Math.Min(18f, plotH * 0.25f);
                float cy = plotBottom + plotH / 2f;
                float y0 = cy - barH / 2f, y1 = cy + barH / 2f;

                // Qualitative ranges (full width scale from 0..maxTarget/value)
                float xmax = Math.Max(Math.Max(bs.Value, bs.Target), bs.QualitativeRanges.Select(r => r.end).DefaultIfEmpty(0f).Max());
                if (xmax <= 0) xmax = 1;

                foreach (var r in bs.QualitativeRanges)
                {
                    float x0 = plotX + (r.start / xmax) * plotW;
                    float x1 = plotX + (r.end / xmax) * plotW;
                    FillRect(sb, x0, y0, x1 - x0, y1 - y0, r.fill);
                }
                // Value bar
                float xv1 = plotX + (bs.Value / xmax) * plotW;
                FillRect(sb, plotX, y0 + barH * 0.2f, xv1 - plotX, barH * 0.6f, bs.ValueFill);
                // Target line
                float xt = plotX + (bs.Target / xmax) * plotW;
                sb.Append($"{ToRgbStroke(bs.TargetStroke)} {N(Math.Max(0.5f, bs.TargetStrokeWidth))} w {N(xt)} {N(y0)} m {N(xt)} {N(y1)} l S\n");
            }

            // ===== RENDER: Cartesian series =====

            // Range/Band Area (draw first, beneath lines)
            foreach (var rs in allRangeArea)
            {
                if (rs.Points.Count < 1) continue;

                var upper = new List<(float x, float y)>();
                var lower = new List<(float x, float y)>();
                foreach (var p in rs.Points.OrderBy(p => rs.UsesCategoryX ? p.CategoryIndex : p.X))
                {
                    float px = rs.UsesCategoryX
                        ? (XBandLeft(p.CategoryIndex, catCount) + XBandWidth(catCount) / 2f)
                        : XtoPxNumeric(p.X);
                    upper.Add((px, YtoPx(p.High, rs.YAxisIndex)));
                    lower.Add((px, YtoPx(p.Low, rs.YAxisIndex)));
                }

                var lowerRev = new List<(float x, float y)>(lower);
                lowerRev.Reverse();

                sb.Append($"{ToRgbFill(rs.Fill)} ");

                if (!rs.Smooth || upper.Count < 3 || lower.Count < 3)
                {
                    // polygon: upper L→R, lower R→L
                    sb.Append($"{N(upper[0].x)} {N(upper[0].y)} m ");
                    for (int i = 1; i < upper.Count; i++) sb.Append($"{N(upper[i].x)} {N(upper[i].y)} l ");
                    for (int i = lower.Count - 1; i >= 0; i--) sb.Append($"{N(lower[i].x)} {N(lower[i].y)} l ");
                    sb.Append("h f\n");
                }
                else
                {
                    float t = Math.Clamp(rs.SmoothTension, 0f, 1f);

                    // upper curve L→R
                    sb.Append($"{N(upper[0].x)} {N(upper[0].y)} m ");
                    AppendSmoothCubicSegments(sb, upper, t);

                    // BRIDGE from upper end to start of reversed lower
                    sb.Append($"{N(lowerRev[0].x)} {N(lowerRev[0].y)} l ");

                    // lower curve R→L
                    AppendSmoothCubicSegments(sb, lowerRev, t);

                    sb.Append("h f\n");

                    // optional outlines along both edges
                    if (rs.StrokeWidth > 0.01f)
                    {
                        Color sc = rs.Stroke.IsEmpty ? rs.Fill : rs.Stroke;

                        // upper outline
                        sb.Append($"{ToRgbStroke(sc)} {N(Math.Max(0.25f, rs.StrokeWidth))} w ");
                        sb.Append($"{N(upper[0].x)} {N(upper[0].y)} m ");
                        AppendSmoothCubicSegments(sb, upper, t);
                        sb.Append("S\n");

                        // lower outline (left→right for nicer label alignment)
                        sb.Append($"{ToRgbStroke(sc)} {N(Math.Max(0.25f, rs.StrokeWidth))} w ");
                        sb.Append($"{N(lower[0].x)} {N(lower[0].y)} m ");
                        AppendSmoothCubicSegments(sb, lower, t);
                        sb.Append("S\n");
                    }
                }
            }


            // Histogram (numeric X)
            foreach (var hs in allHist)
            {
                var bins = new List<(float start, float end, int count)>();
                if (hs.Bins.Count > 0) bins.AddRange(hs.Bins.Select(b => (b.binStart, b.binEnd, b.count)));
                else if (hs.Samples.Count > 0)
                {
                    float mn = hs.Samples.Min(), mx = hs.Samples.Max();
                    if (Math.Abs(mx - mn) < 1e-6) { mn -= 0.5f; mx += 0.5f; }
                    int k = hs.BinCount ?? 10;
                    float bw = hs.BinWidth ?? ((mx - mn) / k);
                    if (bw <= 0) bw = (mx - mn) / Math.Max(1, k);
                    int nb = (int)Math.Ceiling((mx - mn) / bw);
                    var counts = new int[nb];
                    foreach (var v in hs.Samples)
                    {
                        int bi = (int)Math.Floor((v - mn) / bw);
                        if (bi < 0) bi = 0; if (bi >= nb) bi = nb - 1;
                        counts[bi]++;
                    }
                    for (int i = 0; i < nb; i++) bins.Add((mn + i * bw, mn + (i + 1) * bw, counts[i]));
                    // update xMin/xMax for mapping
                    xMin = mn; xMax = mn + nb * bw;
                }
                if (bins.Count == 0) continue;

                int yIdx = hs.YAxisIndex;
                string histLabelFont = string.IsNullOrWhiteSpace(hs.LabelFont) ? axisFont : hs.LabelFont; // for value labels

                foreach (var (start, end, count) in bins)
                {
                    float X0 = XtoPxNumeric(start);
                    float X1 = XtoPxNumeric(end);
                    float Y0 = YtoPx(0, yIdx);
                    float Y1 = YtoPx(count, yIdx);

                    float rx = Math.Min(X0, X1);
                    float rw = Math.Abs(X1 - X0);

                    // gap: shrink bar inside its bin, centered
                    float gap = Math.Clamp(hs.BarGapRatio, 0f, 0.9f);
                    float bw = rw * (1f - gap);
                    float bx = rx + (rw - bw) / 2f;

                    float ry = Math.Min(Y0, Y1);
                    float rh = Math.Abs(Y1 - Y0);

                    FillRect(sb, bx, ry, bw, rh, hs.Fill);
                    StrokeRect(sb, bx, ry, bw, rh, hs.Stroke, hs.StrokeWidth);

                    // optional count labels
                    if (hs.ShowLabels && count > 0)
                    {
                        string txt = hs.LabelFormatter != null ? hs.LabelFormatter(count) : count.ToString();
                        float tw = MeasureTextWidth(txt, histLabelFont, hs.LabelSize);
                        float tx = bx + bw / 2f - tw / 2f;
                        float ty = Math.Max(Y0, Y1) + hs.LabelOffset;
                        DrawText(sb, context, histLabelFont, hs.LabelSize, hs.LabelColor, tx, ty, txt);
                    }
                }

            }

            // Waterfall
            foreach (var ws in allWaterfall)
            {
                float cum = 0f;
                float band = c.XAxis.IsCategory ? XBandWidth(catCount) : (plotW / Math.Max(1, ws.Steps.Count));
                float gap = band * ws.GapRatio;
                float usable = band - gap;

                for (int i = 0; i < ws.Steps.Count; i++)
                {
                    var sstep = ws.Steps[i];
                    int catIndex = sstep.categoryIndex;
                    float delta = sstep.delta;
                    float baseVal = sstep.isTotal ? 0f : cum;
                    float topVal = sstep.isTotal ? cum : cum + delta;

                    float x0 = (c.XAxis.IsCategory ? XBandLeft(catIndex, catCount) : plotX + i * band) + gap / 2f;
                    float x1 = x0 + usable;

                    float y0 = YtoPx(baseVal, ws.YAxisIndex);
                    float y1 = YtoPx(topVal, ws.YAxisIndex);

                    Color fill = sstep.isTotal ? ws.TotalFill : (topVal >= baseVal ? ws.PositiveFill : ws.NegativeFill);

                    if (ws.CornerRadius > 0f)
                    {
                        FillRoundRect(sb, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), ws.CornerRadius, fill);
                        StrokeRoundRect(sb, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), ws.CornerRadius, ws.Stroke, ws.StrokeWidth);
                    }
                    else
                    {
                        FillRect(sb, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), fill);
                        StrokeRect(sb, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), ws.Stroke, ws.StrokeWidth);
                    }

                    if (!sstep.isTotal) cum += delta;
                }
            }

            // Box & Whisker
            foreach (var bx in allBox)
            {
                var stats = new Dictionary<int, (float q1, float median, float q3, float wl, float wh, List<float> outs)>();
                if (bx.Stats.Count > 0)
                {
                    foreach (var s in bx.Stats) stats[s.categoryIndex] = (s.q1, s.median, s.q3, s.whiskerLow, s.whiskerHigh, s.outliers ?? new List<float>());
                }
                else
                {
                    // compute from values
                    foreach (var g in bx.Groups)
                    {
                        var vals = g.values?.ToList() ?? new List<float>();
                        vals.Sort();
                        if (vals.Count == 0) continue;
                        float Q(float p)
                        {
                            if (vals.Count == 1) return vals[0];
                            double idx = (vals.Count - 1) * p;
                            int i0 = (int)Math.Floor(idx);
                            int i1 = Math.Min(vals.Count - 1, i0 + 1);
                            double frac = idx - i0;
                            return (float)(vals[i0] * (1 - frac) + vals[i1] * frac);
                        }
                        float q1 = Q(0.25f), med = Q(0.5f), q3 = Q(0.75f);
                        float iqr = q3 - q1;
                        float lo = vals.Where(v => v >= q1 - 1.5f * iqr).DefaultIfEmpty(vals.First()).Min();
                        float hi = vals.Where(v => v <= q3 + 1.5f * iqr).DefaultIfEmpty(vals.Last()).Max();
                        var outliers = vals.Where(v => v < lo || v > hi).ToList();
                        stats[g.categoryIndex] = (q1, med, q3, lo, hi, outliers);
                    }
                }

                foreach (var kv in stats)
                {
                    int i = kv.Key;
                    var s = kv.Value;
                    float band = XBandWidth(catCount);
                    float cx = XBandLeft(i, catCount) + band / 2f;
                    float bw = band * bx.BoxWidthRatio;

                    float yQ1 = YtoPx(s.q1, bx.YAxisIndex);
                    float yMed = YtoPx(s.median, bx.YAxisIndex);
                    float yQ3 = YtoPx(s.q3, bx.YAxisIndex);
                    float yLo = YtoPx(s.wl, bx.YAxisIndex);
                    float yHi = YtoPx(s.wh, bx.YAxisIndex);

                    // box
                    FillRect(sb, cx - bw / 2f, Math.Min(yQ1, yQ3), bw, Math.Abs(yQ3 - yQ1), bx.Fill);
                    StrokeRect(sb, cx - bw / 2f, Math.Min(yQ1, yQ3), bw, Math.Abs(yQ3 - yQ1), bx.Stroke, bx.StrokeWidth);
                    // median line
                    sb.Append($"{ToRgbStroke(bx.Stroke)} {N(Math.Max(0.5f, bx.StrokeWidth))} w {N(cx - bw / 2f)} {N(yMed)} m {N(cx + bw / 2f)} {N(yMed)} l S\n");
                    // whiskers
                    sb.Append($"{ToRgbStroke(bx.Stroke)} {N(Math.Max(0.5f, bx.StrokeWidth))} w {N(cx)} {N(yQ3)} m {N(cx)} {N(yHi)} l S\n");
                    sb.Append($"{ToRgbStroke(bx.Stroke)} {N(Math.Max(0.5f, bx.StrokeWidth))} w {N(cx)} {N(yQ1)} m {N(cx)} {N(yLo)} l S\n");
                    // whisker caps
                    sb.Append($"{ToRgbStroke(bx.Stroke)} {N(Math.Max(0.5f, bx.StrokeWidth))} w {N(cx - bw / 3f)} {N(yHi)} m {N(cx + bw / 3f)} {N(yHi)} l S\n");
                    sb.Append($"{ToRgbStroke(bx.Stroke)} {N(Math.Max(0.5f, bx.StrokeWidth))} w {N(cx - bw / 3f)} {N(yLo)} m {N(cx + bw / 3f)} {N(yLo)} l S\n");
                    // outliers
                    foreach (var ov in s.outs)
                    {
                        float oy = YtoPx(ov, bx.YAxisIndex);
                        FillCircle(sb, cx, oy, 1.8f, bx.Stroke);
                    }
                }
            }

            // Heatmap
            foreach (var hm in allHeat)
            {
                if (hm.Values == null || hm.Rows <= 0 || hm.Cols <= 0) continue;
                float cellW = plotW / hm.Cols;
                float cellH = plotH / hm.Rows;

                float vmin = hm.Min ?? hm.Values.Cast<float>().Min();
                float vmax = hm.Max ?? hm.Values.Cast<float>().Max();
                if (Math.Abs(vmax - vmin) < 1e-6f) { vmax += 1; vmin -= 1; }

                for (int r = 0; r < hm.Rows; r++)
                {
                    for (int col = 0; col < hm.Cols; col++)
                    {
                        float v = hm.Values[r, col];
                        Color fc = hm.ColorScale != null ? hm.ColorScale(v) : DefaultHeatColor((v - vmin) / (vmax - vmin));
                        float x = plotX + col * cellW;
                        float y = plotYTop - (r + 1) * cellH;
                        FillRect(sb, x, y, cellW, cellH, fc);
                    }
                }
            }

            // Candlestick
            foreach (var cs in allCandle)
            {
                if (cs.Candles.Count == 0) continue;
                int n = c.XAxis.IsCategory ? catCount : cs.Candles.Count;
                float band = XBandWidth(n);
                float bw = band * cs.CandleWidthRatio;

                foreach (var k in cs.Candles)
                {
                    float cx = XBandLeft(k.xIndex, n) + band / 2f;
                    float yO = YtoPx(k.open, cs.YAxisIndex);
                    float yC = YtoPx(k.close, cs.YAxisIndex);
                    float yH = YtoPx(k.high, cs.YAxisIndex);
                    float yL = YtoPx(k.low, cs.YAxisIndex);

                    // wick
                    sb.Append($"{ToRgbStroke(cs.WickStroke)} {N(Math.Max(0.5f, cs.StrokeWidth))} w {N(cx)} {N(yH)} m {N(cx)} {N(yL)} l S\n");

                    float yTopBody = Math.Min(yO, yC);
                    float hBody = Math.Abs(yC - yO);
                    var fill = k.close >= k.open ? cs.UpFill : cs.DownFill;
                    FillRect(sb, cx - bw / 2f, yTopBody, bw, Math.Max(0.5f, hBody), fill);
                    StrokeRect(sb, cx - bw / 2f, yTopBody, bw, Math.Max(0.5f, hBody), cs.Stroke, cs.StrokeWidth);
                }
            }

            // Bars (grouped / stacked / horizontal / normalized 100%)
            if (allBars.Count > 0)
            {
                bool anyStack = allBars.Any(b => !string.IsNullOrEmpty(b.StackKey));
                bool horiz = allBars.Any(b => b.Horizontal);
                if (!horiz)
                {
                    float band = XBandWidth(catCount);
                    float gap = band * allBars.First().GapRatio;
                    if (!anyStack)
                    {
                        // grouped
                        int k = Math.Max(1, allBars.Count);
                        float usable = band - gap;
                        float each = usable / k;

                        for (int i = 0; i < catCount; i++)
                        {
                            for (int sIdx = 0; sIdx < allBars.Count; sIdx++)
                            {
                                var bs = allBars[sIdx];
                                var found = bs.Bars.FirstOrDefault(t => t.categoryIndex == i);
                                float v = found.value;
                                Color fill = ResolveBarFill(c, bs, i);

                                float x0 = XBandLeft(i, catCount) + gap / 2f + sIdx * each;
                                float x1 = x0 + each * 0.9f;
                                float y0 = YtoPx(0, bs.YAxisIndex);
                                float y1 = YtoPx(v, bs.YAxisIndex);
                                DrawBarRect(sb, x0, x1, y0, y1, bs, fill);

                                if (bs.ShowValueLabels)
                                {
                                    string txt = bs.ValueFormatter(v);
                                    string barLabelFont = string.IsNullOrWhiteSpace(bs.ValueLabelFont) ? axisFont : bs.ValueLabelFont;
                                    float tw = MeasureTextWidth(txt, barLabelFont, bs.ValueLabelSize);
                                    float cx = (x0 + x1) * 0.5f;
                                    float top = Math.Max(y0, y1), bot = Math.Min(y0, y1);
                                    float ly;

                                    switch (bs.ValueLabelPosition)
                                    {
                                        case BarValueLabelPos.Center:
                                            ly = (top + bot) * 0.5f - bs.ValueLabelSize * 0.5f;
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.InsideEnd:
                                            ly = top - bs.LabelPadding - bs.ValueLabelSize;
                                            if (y1 < y0) ly = bot + bs.LabelPadding; // negative bar
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.OutsideEnd:
                                            ly = top + bs.LabelPadding;
                                            if (y1 < y0) ly = bot - bs.LabelPadding - bs.ValueLabelSize; // negative
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.InsideBase:
                                            ly = bot + bs.LabelPadding;
                                            if (y1 < y0) ly = top - bs.LabelPadding - bs.ValueLabelSize; // negative
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.OutsideBase:
                                            ly = bot - bs.LabelPadding - bs.ValueLabelSize;
                                            if (y1 < y0) ly = top + bs.LabelPadding; // negative
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // stacked by StackKey (optionally NormalizeTo100 per stack group)
                        var groups = allBars.GroupBy(b => b.StackKey);
                        foreach (var grp in groups)
                        {
                            // sum per category
                            var series = grp.ToList();
                            bool norm100 = series.Any(b => b.NormalizeTo100);
                            for (int i = 0; i < catCount; i++)
                            {
                                float sum = norm100 ? Math.Max(1e-6f, series.Sum(s => s.Bars.FirstOrDefault(t => t.categoryIndex == i).value)) : 1f;
                                float x0 = XBandLeft(i, catCount) + gap / 2f;
                                float usable = band - gap;
                                float x1 = x0 + usable * 0.9f;

                                float yBase = 0f;
                                foreach (var bs in series)
                                {
                                    var found = bs.Bars.FirstOrDefault(t => t.categoryIndex == i);
                                    float v = norm100 ? (found.value / sum) * (tickMax1 - tickMin1) : found.value;
                                    Color fill = ResolveBarFill(c, bs, i);
                                    float y0 = YtoPx(yBase, bs.YAxisIndex);
                                    float y1 = YtoPx(yBase + v, bs.YAxisIndex);
                                    DrawBarRect(sb, x0, x1, y0, y1, bs, fill);

                                    if (bs.ShowValueLabels)
                                    {
                                        string txt = bs.ValueFormatter(v);
                                        string barLabelFont = string.IsNullOrWhiteSpace(bs.ValueLabelFont) ? axisFont : bs.ValueLabelFont;
                                        float tw = MeasureTextWidth(txt, barLabelFont, bs.ValueLabelSize);
                                        float cx = (x0 + x1) * 0.5f;
                                        float top = Math.Max(y0, y1), bot = Math.Min(y0, y1);
                                        float ly;

                                        switch (bs.ValueLabelPosition)
                                        {
                                            case BarValueLabelPos.Center:
                                                ly = (top + bot) * 0.5f - bs.ValueLabelSize * 0.5f;
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.InsideEnd:
                                                ly = top - bs.LabelPadding - bs.ValueLabelSize;
                                                if (y1 < y0) ly = bot + bs.LabelPadding; // negative bar
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.OutsideEnd:
                                                ly = top + bs.LabelPadding;
                                                if (y1 < y0) ly = bot - bs.LabelPadding - bs.ValueLabelSize; // negative
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.InsideBase:
                                                ly = bot + bs.LabelPadding;
                                                if (y1 < y0) ly = top - bs.LabelPadding - bs.ValueLabelSize; // negative
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.OutsideBase:
                                                ly = bot - bs.LabelPadding - bs.ValueLabelSize;
                                                if (y1 < y0) ly = top + bs.LabelPadding; // negative
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;
                                        }
                                    }
                                    yBase += v;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // horizontal bars: categories on Y
                    float bandH = plotH / catCount;
                    float gap = bandH * allBars.First().GapRatio;

                    bool anyStackH = allBars.Where(b => b.Horizontal).Any(b => !string.IsNullOrEmpty(b.StackKey));
                    if (!anyStackH)
                    {
                        // grouped horizontal per category
                        var horizBars = allBars.Where(b => b.Horizontal).ToList();
                        int k = Math.Max(1, horizBars.Count);
                        float usable = bandH - gap;
                        float each = usable / k;

                        for (int i = 0; i < catCount; i++)
                        {
                            for (int sIdx = 0; sIdx < horizBars.Count; sIdx++)
                            {
                                var bs = horizBars[sIdx];
                                var found = bs.Bars.FirstOrDefault(t => t.categoryIndex == i);
                                float v = found.value;
                                Color fill = ResolveBarFill(c, bs, i);

                                float y0 = plotYTop - (i + 1) * bandH + gap / 2f + sIdx * each;
                                float y1 = y0 + each * 0.9f;
                                float x0 = XtoPxNumeric(0);
                                float x1 = XtoPxNumeric(v);
                                DrawBarRect(sb, Math.Min(x0, x1), Math.Max(x0, x1), y0, y1, bs, fill, horizontal: true);

                                if (bs.ShowValueLabels)
                                {
                                    string txt = bs.ValueFormatter(v);
                                    string barLabelFont = string.IsNullOrWhiteSpace(bs.ValueLabelFont) ? axisFont : bs.ValueLabelFont;
                                    float tw = MeasureTextWidth(txt, barLabelFont, bs.ValueLabelSize);
                                    float cx = (x0 + x1) * 0.5f;
                                    float top = Math.Max(y0, y1), bot = Math.Min(y0, y1);
                                    float ly;

                                    switch (bs.ValueLabelPosition)
                                    {
                                        case BarValueLabelPos.Center:
                                            ly = (top + bot) * 0.5f - bs.ValueLabelSize * 0.5f;
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.InsideEnd:
                                            ly = top - bs.LabelPadding - bs.ValueLabelSize;
                                            if (y1 < y0) ly = bot + bs.LabelPadding; // negative bar
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.OutsideEnd:
                                            ly = top + bs.LabelPadding;
                                            if (y1 < y0) ly = bot - bs.LabelPadding - bs.ValueLabelSize; // negative
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.InsideBase:
                                            ly = bot + bs.LabelPadding;
                                            if (y1 < y0) ly = top - bs.LabelPadding - bs.ValueLabelSize; // negative
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;

                                        case BarValueLabelPos.OutsideBase:
                                            ly = bot - bs.LabelPadding - bs.ValueLabelSize;
                                            if (y1 < y0) ly = top + bs.LabelPadding; // negative
                                            DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // stacked horizontal by StackKey (optional NormalizeTo100)
                        var groups = allBars.Where(b => b.Horizontal).GroupBy(b => b.StackKey);
                        foreach (var grp in groups)
                        {
                            var series = grp.ToList();
                            bool norm100 = series.Any(b => b.NormalizeTo100);
                            for (int i = 0; i < catCount; i++)
                            {
                                float sum = norm100 ? Math.Max(1e-6f, series.Sum(s => s.Bars.FirstOrDefault(t => t.categoryIndex == i).value)) : 1f;
                                float y0 = plotYTop - (i + 1) * bandH + gap / 2f;
                                float usable = bandH - gap;
                                float y1 = y0 + usable * 0.9f;

                                float xBase = 0f;
                                foreach (var bs in series)
                                {
                                    var found = bs.Bars.FirstOrDefault(t => t.categoryIndex == i);
                                    float v = norm100 ? (found.value / sum) * (xMax - xMin) : found.value;
                                    Color fill = ResolveBarFill(c, bs, i);
                                    float x0 = XtoPxNumeric(xBase);
                                    float x1 = XtoPxNumeric(xBase + v);
                                    DrawBarRect(sb, Math.Min(x0, x1), Math.Max(x0, x1), y0, y1, bs, fill, horizontal: true);

                                    if (bs.ShowValueLabels)
                                    {
                                        string txt = bs.ValueFormatter(v);
                                        string barLabelFont = string.IsNullOrWhiteSpace(bs.ValueLabelFont) ? axisFont : bs.ValueLabelFont;
                                        float tw = MeasureTextWidth(txt, barLabelFont, bs.ValueLabelSize);
                                        float cx = (x0 + x1) * 0.5f;
                                        float top = Math.Max(y0, y1), bot = Math.Min(y0, y1);
                                        float ly;

                                        switch (bs.ValueLabelPosition)
                                        {
                                            case BarValueLabelPos.Center:
                                                ly = (top + bot) * 0.5f - bs.ValueLabelSize * 0.5f;
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.InsideEnd:
                                                ly = top - bs.LabelPadding - bs.ValueLabelSize;
                                                if (y1 < y0) ly = bot + bs.LabelPadding; // negative bar
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.OutsideEnd:
                                                ly = top + bs.LabelPadding;
                                                if (y1 < y0) ly = bot - bs.LabelPadding - bs.ValueLabelSize; // negative
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.InsideBase:
                                                ly = bot + bs.LabelPadding;
                                                if (y1 < y0) ly = top - bs.LabelPadding - bs.ValueLabelSize; // negative
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;

                                            case BarValueLabelPos.OutsideBase:
                                                ly = bot - bs.LabelPadding - bs.ValueLabelSize;
                                                if (y1 < y0) ly = top + bs.LabelPadding; // negative
                                                DrawText(sb, context, barLabelFont, bs.ValueLabelSize, bs.ValueLabelColor, cx - tw * 0.5f, ly, txt);
                                                break;
                                        }
                                    }
                                    xBase += v;
                                }
                            }
                        }
                    }
                }
            }

            // Lines (including Step + Smooth + Area already filled earlier, but we redo area using per-axis)
            foreach (var ls in allLines)
            {
                var pts = ls.Points;
                if (pts.Count == 0) continue;

               

                // Stroke
                var m2 = MapLinePoints(pts, ls.UsesCategoryX, catCount, plotW, XBandLeft, XBandWidth, XtoPxNumeric, v => YtoPx(v, ls.YAxisIndex));
                sb.Append($"{ToRgbStroke(ls.Stroke)} {N(ls.StrokeWidth)} w ");
                if (ls.Smooth)
                {
                    AppendSmoothPath(sb, m2, Math.Clamp(ls.SmoothTension, 0f, 1f));
                    sb.Append("S\n");
                }
                else if (ls.Step)
                {
                    sb.Append($"{N(m2[0].x)} {N(m2[0].y)} m ");
                    for (int i = 1; i < m2.Count; i++)
                    {
                        sb.Append($"{N(m2[i].x)} {N(m2[i - 1].y)} l {N(m2[i].x)} {N(m2[i].y)} l ");
                    }
                    sb.Append("S\n");
                }
                else
                {
                    sb.Append($"{N(m2[0].x)} {N(m2[0].y)} m ");
                    for (int i = 1; i < m2.Count; i++) sb.Append($"{N(m2[i].x)} {N(m2[i].y)} l ");
                    sb.Append("S\n");
                }

                // Markers
                if (ls.ShowMarkers)
                {
                    var mf = ls.MarkerFill ?? Color.White;
                    foreach (var p in m2)
                    {
                        FillCircle(sb, p.x, p.y, ls.MarkerSize, mf);
                        StrokeCircle(sb, p.x, p.y, ls.MarkerSize, ls.Stroke, 0.5f);
                    }
                }
                if (ls.ShowValueLabels)
                {
                    var mapped = MapLinePoints(pts, ls.UsesCategoryX, catCount, plotW, XBandLeft, XBandWidth, XtoPxNumeric, v => YtoPx(v, ls.YAxisIndex));
                    string lineLabelFont = string.IsNullOrWhiteSpace(ls.ValueLabelFont) ? axisFont : ls.ValueLabelFont;

                    IEnumerable<(System.Drawing.PointF p, float x, float y)> toLabel =
                        ls.LabelOnlyLast ? new[] { (pts[^1], mapped[^1].x, mapped[^1].y) }
                                         : pts.Zip(mapped, (p, m) => (p, m.x, m.y));

                    foreach (var (p, x, y) in toLabel)
                    {
                        string txt = ls.PointLabelFormatter(p);
                        float tw = MeasureTextWidth(txt, lineLabelFont, ls.ValueLabelSize);
                        float ox = 0, oy = 0;
                        switch (ls.ValueLabelPosition)
                        {
                            case LineValueLabelPos.Above: oy = ls.ValueLabelOffset; DrawText(sb, context, lineLabelFont, ls.ValueLabelSize, ls.ValueLabelColor, x - tw * 0.5f, y + oy, txt); break;
                            case LineValueLabelPos.Below: oy = ls.ValueLabelOffset; DrawText(sb, context, lineLabelFont, ls.ValueLabelSize, ls.ValueLabelColor, x - tw * 0.5f, y - oy - ls.ValueLabelSize, txt); break;
                            case LineValueLabelPos.Right: ox = ls.ValueLabelOffset; DrawText(sb, context, lineLabelFont, ls.ValueLabelSize, ls.ValueLabelColor, x + ox, y - ls.ValueLabelSize * 0.5f, txt); break;
                            case LineValueLabelPos.Left: ox = ls.ValueLabelOffset; DrawText(sb, context, lineLabelFont, ls.ValueLabelSize, ls.ValueLabelColor, x - tw - ox, y - ls.ValueLabelSize * 0.5f, txt); break;
                            case LineValueLabelPos.Center: DrawText(sb, context, lineLabelFont, ls.ValueLabelSize, ls.ValueLabelColor, x - tw * 0.5f, y - ls.ValueLabelSize * 0.5f, txt); break;
                        }
                    }
                }

            }

            // Scatter
            foreach (var ss in allScatter)
            {
                foreach (var p in ss.Points)
                {
                    float px = ss.UsesCategoryX ? (XBandLeft((int)p.X, catCount) + XBandWidth(catCount) / 2f) : XtoPxNumeric(p.X);
                    float py = YtoPx(p.Y, ss.YAxisIndex);
                    DrawMarker(sb, ss.Marker, px, py, ss.MarkerSize, ss.Fill ?? Color.White, ss.Outline ? ss.Stroke : (Color?)null, Math.Max(0.25f, ss.StrokeWidth));
                }
            }

            // Bubble
            // Bubble
            foreach (var bs in allBubble)
            {
                if (bs.Points.Count == 0) continue;

                float sMin = bs.SizeDomainMin ?? bs.Points.Min(p => p.Size);
                float sMax = bs.SizeDomainMax ?? bs.Points.Max(p => p.Size);
                if (Math.Abs(sMax - sMin) < 1e-6f) { sMin -= 0.5f; sMax += 0.5f; }

                for (int i = 0; i < bs.Points.Count; i++)
                {
                    var p = bs.Points[i];

                    float px = bs.UsesCategoryX
                        ? (XBandLeft((int)p.X, catCount) + XBandWidth(catCount) / 2f)
                        : XtoPxNumeric(p.X);
                    float py = YtoPx(p.Y, bs.YAxisIndex);

                    float k = (p.Size - sMin) / (sMax - sMin);
                    k = Math.Clamp(k, 0f, 1f);
                    float r = MathF.Sqrt(bs.MinRadius * bs.MinRadius +
                                         k * (bs.MaxRadius * bs.MaxRadius - bs.MinRadius * bs.MinRadius));

                    var fill = p.Fill ?? c.Palette[i % c.Palette.Count];

                    // shadow first
                    if (bs.ShowShadow)
                        FillCircle(sb, px + bs.ShadowDx, py + bs.ShadowDy, r * bs.ShadowScale, bs.ShadowColor);

                    // bubble + outline
                    FillCircle(sb, px, py, r, fill);
                    StrokeCircle(sb, px, py, r, bs.Stroke, Math.Max(0.4f, bs.StrokeWidth));

                    // label
                    if (bs.ShowLabels)
                    {
                        string txt = bs.LabelFormatter != null
                            ? bs.LabelFormatter(p)
                            : (string.IsNullOrWhiteSpace(p.Category) ? $"{p.Y:0}, {p.X:0}" : $"{p.Category}  ${p.Y:0}, {p.X:0}");

                        string bubbleLabelFont = string.IsNullOrWhiteSpace(bs.LabelFont) ? axisFont : bs.LabelFont;
                        float tw = MeasureTextWidth(txt, bubbleLabelFont, bs.LabelSize);

                        // try ABOVE first
                        float tx = px - tw * 0.5f;
                        float ty = py + r + bs.LabelOffset;

                        // flip BELOW if we would clip out of the plot at the top
                        if (ty + bs.LabelSize > plotYTop - 2)
                            ty = py - r - bs.LabelOffset - bs.LabelSize;

                        // keep text inside the plot horizontally
                        tx = Math.Max(plotX + 1, Math.Min(tx, plotX + plotW - tw - 1));

                        // subtle white backplate
                        float pad = 2f;
                        FillRect(sb, tx - pad, ty - pad, tw + 2 * pad, bs.LabelSize + 2 * pad, Color.White);

                        DrawText(sb, context, bubbleLabelFont, bs.LabelSize, bs.LabelColor, tx, ty, txt);
                    }

                }
            }


            // Error bars
            foreach (var es in allErr)
            {
                foreach (var p in es.Points)
                {
                    float px = es.UsesCategoryX ? (XBandLeft(p.CategoryIndex, catCount) + XBandWidth(catCount) / 2f) : XtoPxNumeric(p.X);
                    float py = YtoPx(p.Y, es.YAxisIndex);
                    float em = es.Symmetric ? p.Error : p.ErrorMinus;
                    float ep = es.Symmetric ? p.Error : p.ErrorPlus;
                    float y0 = YtoPx(p.Y - em, es.YAxisIndex);
                    float y1 = YtoPx(p.Y + ep, es.YAxisIndex);

                    sb.Append($"{ToRgbStroke(es.Stroke)} {N(Math.Max(0.25f, es.StrokeWidth))} w {N(px)} {N(y0)} m {N(px)} {N(y1)} l S\n");
                    // caps
                    float cap = es.CapWidth / 2f;
                    sb.Append($"{ToRgbStroke(es.Stroke)} {N(Math.Max(0.25f, es.StrokeWidth))} w {N(px - cap)} {N(y0)} m {N(px + cap)} {N(y0)} l S\n");
                    sb.Append($"{ToRgbStroke(es.Stroke)} {N(Math.Max(0.25f, es.StrokeWidth))} w {N(px - cap)} {N(y1)} m {N(px + cap)} {N(y1)} l S\n");
                }
            }

            // Pareto (bars + cumulative line to 100%)
            foreach (var pr in allPareto)
            {
                var items = pr.Items.ToList();
                if (pr.SortDescending) items = items.OrderByDescending(x => x.value).ToList();

                // bars on primary axis
                float total = Math.Max(1e-6f, items.Sum(i => i.value));
                float band = XBandWidth(items.Count);
                float gap = band * pr.BarGapRatio;
                float usable = band - gap;

                for (int i = 0; i < items.Count; i++)
                {
                    float v = items[i].value;
                    float x0 = XBandLeft(i, items.Count) + gap / 2f;
                    float x1 = x0 + usable * 0.9f;
                    float y0 = YtoPx(0, pr.YAxisIndex);
                    float y1 = YtoPx(v, pr.YAxisIndex);
                    FillRect(sb, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), pr.BarFill);
                    StrokeRect(sb, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), Color.Black, 0.5f);
                }

                // cumulative line 0..100% mapped to right side or internally
                float acc = 0f;
                var linePts = new List<(float x, float y)>();
                for (int i = 0; i < items.Count; i++)
                {
                    acc += items[i].value;
                    float pct = (acc / total) * 100f;
                    float cx = XBandLeft(i, items.Count) + band / 2f;
                    float cy;
                    if (c.YAxis2 != null)
                        cy = YtoPx(pct, 1);
                    else
                        cy = plotBottom + pct / 100f * plotH;

                    linePts.Add((cx, cy));
                }
                sb.Append($"{ToRgbStroke(pr.CumulativeStroke)} {N(Math.Max(0.75f, pr.CumulativeStrokeWidth))} w ");
                sb.Append($"{N(linePts[0].x)} {N(linePts[0].y)} m ");
                for (int i = 1; i < linePts.Count; i++) sb.Append($"{N(linePts[i].x)} {N(linePts[i].y)} l ");
                sb.Append("S\n");
            }

            // Gantt
            foreach (var gs in allGantt)
            {
                if (gs.Tasks.Count == 0) continue;
                int rows = Math.Max(catCount, gs.Tasks.Max(t => t.CategoryIndex + 1));
                float rowH = plotH / Math.Max(1, rows);
                foreach (var t in gs.Tasks)
                {
                    float y0 = plotYTop - (t.CategoryIndex + 1) * rowH + (1 - gs.BarHeightRatio) * rowH / 2f;
                    float h = rowH * gs.BarHeightRatio;
                    float x0 = XtoPxNumeric(t.StartX);
                    float x1 = XtoPxNumeric(t.EndX);
                    Color fc = t.Fill ?? c.Palette[t.CategoryIndex % c.Palette.Count];
                    Color sc = t.Stroke ?? gs.Stroke;

                    FillRoundRect(sb, Math.Min(x0, x1), y0, Math.Abs(x1 - x0), h, 2f, fc);
                    StrokeRoundRect(sb, Math.Min(x0, x1), y0, Math.Abs(x1 - x0), h, 2f, sc, gs.StrokeWidth);

                    // label mid
                    if (!string.IsNullOrWhiteSpace(t.Label))
                    {
                        DrawTextCentered(sb, context, axisFont, Math.Min(9f, rowH * 0.4f), Color.Black, (x0 + x1) / 2f, y0 + h / 2f - 4f, t.Label);
                    }
                }
            }

            sb.Append("Q\n"); // end plot clip
                              // --- PIE LABELS & LEADERS (drawn outside the plot clip) ---
            foreach (var L in pieLeaderLines)
            {
                sb.Append($"{ToRgbStroke(L.c)} {N(Math.Max(0.25f, L.w))} w " +
                          $"{N(L.x0)} {N(L.y0)} m {N(L.x1)} {N(L.y1)} l {N(L.x2)} {N(L.y2)} l S\n");
            }
            foreach (var T in pieTexts)
            {
                DrawText(sb, context, T.font, T.size, T.color, T.x, T.y, T.text);
            }
         
            // ===== Legend (simple swatches per series) =====
            if (c.Series.Count > 0 && c.LegendPosition != LegendPos.None && c.ShowLegend)
            {
                string legendFont = string.IsNullOrWhiteSpace(c.LegendFont) ? axisFont : c.LegendFont;
                float lx, ly;

                switch (c.LegendPosition)
                {
                    case LegendPos.InsideTopLeft:
                        lx = plotX + 6; ly = plotYTop - 14; break;
                    case LegendPos.Below:
                        lx = plotX; ly = plotBottom - 14; break;
                    default: // InsideTopRight
                        lx = plotX + plotW - 140; ly = plotYTop - 14; break;
                }

                foreach (var s in c.Series)
                {
                    if (s is PieSeries pc)
                    {
                        float total = Math.Max(1e-6f, pc.Slices.Sum(x => Math.Max(0, x.Value)));

                        for (int i = 0; i < pc.Slices.Count; i++)
                        {
                            var sl = pc.Slices[i];
                            string name = sl.Label ?? "";
                            if (pc.AppendPercentages && total > 0) name += $" ({(sl.Value / total * 100f):0.#}%)";

                            // FIX: use per-slice fill or fallback to palette[i]
                            Color sw = sl.Fill ?? c.Palette[i % c.Palette.Count];

                            FillRect(sb, lx, ly - 8, 14, 6, sw);
                            StrokeRect(sb, lx, ly - 8, 14, 6, Color.Black, 0.5f);
                            DrawText(sb, context, legendFont, c.LegendFontSize, c.LegendTextColor, lx + 18, ly - 10, name);
                            ly -= 14;
                        }
                        continue;
                    }
                    if (s is BubbleSeries bbs && bbs.LegendPerPoint)
                    {
                        for (int i = 0; i < bbs.Points.Count; i++)
                        {
                            var bp = bbs.Points[i];
                            string name = string.IsNullOrWhiteSpace(bp.Category) ? $"{bbs.Name} {i + 1}" : bp.Category;
                            Color sw = bp.Fill ?? c.Palette[i % c.Palette.Count];
                            FillRect(sb, lx, ly - 8, 14, 6, sw);
                            StrokeRect(sb, lx, ly - 8, 14, 6, Color.Black, 0.5f);
                            DrawText(sb, context, legendFont, c.LegendFontSize, c.LegendTextColor, lx + 18, ly - 10, name);
                            ly -= 14;
                        }
                        continue;
                    }
                    Color swatch = s switch
                    {
                        BarSeries b => b.Fill,
                        PieSeries p => p.Slices.FirstOrDefault()?.Fill ?? c.Palette.First(),
                        BubbleSeries bb => bb.Points.FirstOrDefault()?.Fill ?? c.Palette.First(),
                        RangeAreaSeries ra => ra.Fill,
                        WaterfallSeries wf => wf.TotalFill,          // ← new: or wf.PositiveFill if you prefer
                        FunnelSeries => Color.FromArgb(180, 180, 180),
                        CandleSeries => Color.FromArgb(180, 180, 180),
                        _ => s.Stroke
                    };
                    FillRect(sb, lx, ly - 8, 14, 6, swatch);
                    StrokeRect(sb, lx, ly - 8, 14, 6, s.Stroke, 0.5f);
                    DrawText(sb, context, legendFont, c.LegendFontSize, c.LegendTextColor, lx + 18, ly - 10, s.Name ?? "");
                    ly -= 14;
                }
            }

            sb.Append("Q\n"); // outer
        }

        // ---- utilities ----
        private static (float niceMin, float niceMax, float niceStep) NiceTicks(float min, float max, int ticks)
        {
            if (max < min) (min, max) = (max, min);
            double range = NiceNum(max - min, false);
            double step = NiceNum(range / Math.Max(1, ticks - 1), true);
            double niceMin = Math.Floor(min / step) * step;
            double niceMax = Math.Ceiling(max / step) * step;
            return ((float)niceMin, (float)niceMax, (float)step);
        }
        private static double NiceNum(double x, bool round)
        {
            if (x <= 0) x = 1;
            double exp = Math.Floor(Math.Log10(x));
            double f = x / Math.Pow(10, exp);
            double nf = round
                ? f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10
                : f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
            return nf * Math.Pow(10, exp);
        }

        private static string ToRgbStroke(Color c) => $"{(c.R / 255.0).ToString("0.###", Inv)} {(c.G / 255.0).ToString("0.###", Inv)} {(c.B / 255.0).ToString("0.###", Inv)} RG";
        private static string ToRgbFill(Color c) => $"{(c.R / 255.0).ToString("0.###", Inv)} {(c.G / 255.0).ToString("0.###", Inv)} {(c.B / 255.0).ToString("0.###", Inv)} rg";

        private static void DrawText(StringBuilder sb, PdfRenderContext context, string fontFamily, float size, Color color, float x, float y, string s)
        {
            var run = ShapeText(s, fontFamily, size);
            if (run == null || run.Glyphs.Count == 0)
                return;

            var encoded = GlyphRunEncoder.Encode(run, context);
            sb.Append("BT ");
            sb.Append($"{encoded.FontResourceName} {N(run.FontSize)} Tf {ToRgbFill(color)} ");
            sb.Append($"{N(x)} {N(y)} Td ");
            sb.Append($"{encoded.TjCommand} ET\n");
        }

        private static void DrawTextCentered(StringBuilder sb, PdfRenderContext context, string fontFamily, float size, Color color, float cx, float cy, string s)
        {
            var run = ShapeText(s, fontFamily, size);
            if (run == null || run.Glyphs.Count == 0)
                return;

            float dx = -0.5f * run.Width;
            var encoded = GlyphRunEncoder.Encode(run, context);
            sb.Append("BT ");
            sb.Append($"{encoded.FontResourceName} {N(run.FontSize)} Tf {ToRgbFill(color)} ");
            sb.Append($"{N(cx + dx)} {N(cy)} Td ");
            sb.Append($"{encoded.TjCommand} ET\n");
        }

        private static void DrawTextRot(StringBuilder sb, PdfRenderContext context, string fontFamily, float size, Color color, float cx, float cy, string s, float deg, bool hCenter)
        {
            var run = ShapeText(s, fontFamily, size);
            if (run == null || run.Glyphs.Count == 0)
                return;

            float dx = hCenter ? -0.5f * run.Width : 0f;
            var encoded = GlyphRunEncoder.Encode(run, context);

            double radians = deg * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            sb.Append("BT ");
            sb.Append($"{encoded.FontResourceName} {N(run.FontSize)} Tf {ToRgbFill(color)} ");
            sb.Append($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} {N(cx + dx)} {N(cy)} Tm ");
            sb.Append($"{encoded.TjCommand} ET\n");
        }

        private static float MeasureTextWidth(string s, string fontFamily, float size)
        {
            var run = ShapeText(s, fontFamily, size);
            return run?.Width ?? 0f;
        }

        private static ShapedRun? ShapeText(string s, string fontFamily, float size)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            var request = new TextShapingRequest(
                s,
                fontFamily,
                size,
                lineHeight: 1f,
                maxWidth: float.PositiveInfinity,
                bold: false,
                italic: false,
                smallCaps: false,
                monospace: false,
                fallbackFonts: null);

            var paragraph = TextShaper.Shared.ShapeParagraph(request);
            var line = paragraph.Lines.FirstOrDefault();
            return line?.Runs.FirstOrDefault();
        }

        private static void FillRect(StringBuilder sb, float x, float y, float w, float h, Color color)
            => sb.Append($"{ToRgbFill(color)} {N(x)} {N(y)} {N(w)} {N(h)} re f\n");
        private static void StrokeRect(StringBuilder sb, float x, float y, float w, float h, Color color, float width)
        {
            sb.Append($"{ToRgbStroke(color)} {N(Math.Max(0.25f, width))} w ");
            sb.Append($"{N(AlignHalf(x))} {N(AlignHalf(y + h))} m {N(AlignHalf(x + w))} {N(AlignHalf(y + h))} l {N(AlignHalf(x + w))} {N(AlignHalf(y))} l {N(AlignHalf(x))} {N(AlignHalf(y))} l h S\n");
        }
        // Emits ONLY the cubic segments ("c ..."), no initial moveto.
        // Expects the caller to have already issued "m" at pts[0].
        private static void AppendSmoothCubicSegments(StringBuilder sb, List<(float x, float y)> P, float tension)
        {
            if (P.Count < 2) return;
            float t = tension / 6f;

            for (int i = 0; i < P.Count - 1; i++)
            {
                var p0 = i == 0 ? P[0] : P[i - 1];
                var p1 = P[i];
                var p2 = P[i + 1];
                var p3 = i + 2 < P.Count ? P[i + 2] : P[i + 1];

                float c1x = p1.x + (p2.x - p0.x) * t;
                float c1y = p1.y + (p2.y - p0.y) * t;
                float c2x = p2.x - (p3.x - p1.x) * t;
                float c2y = p2.y - (p3.y - p1.y) * t;

                sb.Append($"{N(c1x)} {N(c1y)} {N(c2x)} {N(c2y)} {N(p2.x)} {N(p2.y)} c ");
            }
        }

        private static void DrawBarRect(StringBuilder sb, float x0, float x1, float y0, float y1, BarSeries bs, Color fill, bool horizontal = false)
        {
            float rx = Math.Min(x0, x1), rw = Math.Abs(x1 - x0);
            float ry = Math.Min(y0, y1), rh = Math.Abs(y1 - y0);

            if (bs.CornerRadius > 0f)
            {
                FillRoundRect(sb, rx, ry, rw, rh, bs.CornerRadius, fill);
                StrokeRoundRect(sb, rx, ry, rw, rh, bs.CornerRadius, bs.Stroke, bs.StrokeWidth);
            }
            else
            {
                FillRect(sb, rx, ry, rw, rh, fill);
                StrokeRect(sb, rx, ry, rw, rh, bs.Stroke, bs.StrokeWidth);
            }
        }

        private static Color ResolveBarFill(ChartElement c, BarSeries bs, int catIdx)
        {
            if (bs.BarFills.Count > 0) return bs.BarFills[catIdx % bs.BarFills.Count];
            if (bs.AlternateFill.HasValue) return (catIdx % 2 == 0) ? bs.Fill : bs.AlternateFill.Value;
            return bs.Fill;
        }

        private static void FillRoundRect(StringBuilder sb, float x, float y, float w, float h, float r, Color fill)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2f));
            sb.Append($"{ToRgbFill(fill)} ");
            RoundRectPath(sb, x, y, w, h, r);
            sb.Append("f\n");
        }
        private static void StrokeRoundRect(StringBuilder sb, float x, float y, float w, float h, float r, Color stroke, float width)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2f));
            sb.Append($"{ToRgbStroke(stroke)} {N(Math.Max(0.25f, width))} w ");
            RoundRectPath(sb, x, y, w, h, r);
            sb.Append("S\n");
        }
        private static void RoundRectPath(StringBuilder sb, float x, float y, float w, float h, float r)
        {
            // PDF cubic Bezier approximation of quarter-circles
            const double K = 0.5522847498307936; // 4*(sqrt(2)-1)/3
            float x0 = x, y0 = y, x1 = x + w, y1 = y + h;
            float ox = (float)(r * K), oy = ox;

            sb.Append($"{N(x0 + r)} {N(y0)} m ");
            sb.Append($"{N(x1 - r)} {N(y0)} l ");
            sb.Append($"{N(x1 - r + ox)} {N(y0)} {N(x1)} {N(y0 + r - oy)} {N(x1)} {N(y0 + r)} c ");
            sb.Append($"{N(x1)} {N(y1 - r)} l ");
            sb.Append($"{N(x1)} {N(y1 - r + oy)} {N(x1 - r + ox)} {N(y1)} {N(x1 - r)} {N(y1)} c ");
            sb.Append($"{N(x0 + r)} {N(y1)} l ");
            sb.Append($"{N(x0 + r - ox)} {N(y1)} {N(x0)} {N(y1 - r + oy)} {N(x0)} {N(y1 - r)} c ");
            sb.Append($"{N(x0)} {N(y0 + r)} l ");
            sb.Append($"{N(x0)} {N(y0 + r - oy)} {N(x0 + r - ox)} {N(y0)} {N(x0 + r)} {N(y0)} c h ");
        }

        private static void FillCircle(StringBuilder sb, float cx, float cy, float r, Color fill)
        {
            const double K = 0.5522847498307936;
            float ox = (float)(r * K), oy = ox;
            sb.Append($"{ToRgbFill(fill)} ");
            sb.Append($"{N(cx + r)} {N(cy)} m ");
            sb.Append($"{N(cx + r)} {N(cy + oy)} {N(cx + ox)} {N(cy + r)} {N(cx)} {N(cy + r)} c ");
            sb.Append($"{N(cx - ox)} {N(cy + r)} {N(cx - r)} {N(cy + oy)} {N(cx - r)} {N(cy)} c ");
            sb.Append($"{N(cx - r)} {N(cy - oy)} {N(cx - ox)} {N(cy - r)} {N(cx)} {N(cy - r)} c ");
            sb.Append($"{N(cx + ox)} {N(cy - r)} {N(cx + r)} {N(cy - oy)} {N(cx + r)} {N(cy)} c f\n");
        }
        private static void StrokeCircle(StringBuilder sb, float cx, float cy, float r, Color stroke, float width)
        {
            const double K = 0.5522847498307936;
            float ox = (float)(r * K), oy = ox;
            sb.Append($"{ToRgbStroke(stroke)} {N(Math.Max(0.25f, width))} w ");
            sb.Append($"{N(cx + r)} {N(cy)} m ");
            sb.Append($"{N(cx + r)} {N(cy + oy)} {N(cx + ox)} {N(cy + r)} {N(cx)} {N(cy + r)} c ");
            sb.Append($"{N(cx - ox)} {N(cy + r)} {N(cx - r)} {N(cy + oy)} {N(cx - r)} {N(cy)} c ");
            sb.Append($"{N(cx - r)} {N(cy - oy)} {N(cx - ox)} {N(cy - r)} {N(cx)} {N(cy - r)} c ");
            sb.Append($"{N(cx + ox)} {N(cy - r)} {N(cx + r)} {N(cy - oy)} {N(cx + r)} {N(cy)} c S\n");
        }

        private static void AppendSmoothPath(StringBuilder sb, List<(float x, float y)> pts, float tension)
        {
            if (pts.Count < 2) return;
            var P = pts;
            sb.Append($"{N(P[0].x)} {N(P[0].y)} m ");
            for (int i = 0; i < P.Count - 1; i++)
            {
                var p0 = i == 0 ? P[0] : P[i - 1];
                var p1 = P[i];
                var p2 = P[i + 1];
                var p3 = i + 2 < P.Count ? P[i + 2] : P[i + 1];

                float t = tension;
                float c1x = p1.x + (p2.x - p0.x) * (t / 6f);
                float c1y = p1.y + (p2.y - p0.y) * (t / 6f);
                float c2x = p2.x - (p3.x - p1.x) * (t / 6f);
                float c2y = p2.y - (p3.y - p1.y) * (t / 6f);

                sb.Append($"{N(c1x)} {N(c1y)} {N(c2x)} {N(c2y)} {N(p2.x)} {N(p2.y)} c ");
            }
        }

        private static List<(float x, float y)> MapLinePoints(
            List<PointF> pts, bool usesCategoryX, int catCount, float plotW,
            Func<int, int, float> XBandLeft, Func<int, float> XBandWidth, Func<float, float> XtoPxNumeric,
            Func<float, float> Ymap)
        {
            var res = new List<(float x, float y)>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                float px = usesCategoryX ? XBandLeft((int)pts[i].X, catCount) + XBandWidth(catCount) / 2f : XtoPxNumeric(pts[i].X);
                float py = Ymap(pts[i].Y);
                res.Add((px, py));
            }
            return res;
        }

        // ---- Pie/Donut arc helpers ----
        private static void FillAnnularSector(
      StringBuilder sb, float cx, float cy, float r0, float r1,
      float a0, float a1, Color fill)
        {
            sb.Append($"{ToRgbFill(fill)} ");

            // 1) outer arc a0 -> a1
            ArcPath(sb, cx, cy, r1, a0, a1, moveToStart: true);

            if (r0 > 0.01f)
            {
                // 2) radial line: outer(a1) -> inner(a1)
                float xi1 = cx + r0 * MathF.Cos(a1);
                float yi1 = cy + r0 * MathF.Sin(a1);
                sb.Append($"{N(xi1)} {N(yi1)} l ");

                // 3) inner arc a1 -> a0 (continue path; no moveto)
                ArcPath(sb, cx, cy, r0, a1, a0, moveToStart: false);

                // 4) radial line: inner(a0) -> outer(a0)
                float xo0 = cx + r1 * MathF.Cos(a0);
                float yo0 = cy + r1 * MathF.Sin(a0);
                sb.Append($"{N(xo0)} {N(yo0)} l h f\n");
            }
            else
            {
                // plain pie slice
                sb.Append($"{N(cx)} {N(cy)} l h f\n");
            }
        }

        private static void StrokeAnnularSectorOutline(
            StringBuilder sb, float cx, float cy, float r0, float r1,
            float a0, float a1, Color stroke, float width)
        {
            sb.Append($"{ToRgbStroke(stroke)} {N(Math.Max(0.25f, width))} w ");

            // outer arc
            ArcPath(sb, cx, cy, r1, a0, a1, moveToStart: true);

            if (r0 > 0.01f)
            {
                // radial line to inner(a1)
                float xi1 = cx + r0 * MathF.Cos(a1);
                float yi1 = cy + r0 * MathF.Sin(a1);
                sb.Append($"{N(xi1)} {N(yi1)} l ");

                // inner arc back a1 -> a0
                ArcPath(sb, cx, cy, r0, a1, a0, moveToStart: false);

                // radial line back to outer(a0)
                float xo0 = cx + r1 * MathF.Cos(a0);
                float yo0 = cy + r1 * MathF.Sin(a0);
                sb.Append($"{N(xo0)} {N(yo0)} l S\n");
            }
            else
            {
                // stroke the wedge to center
                sb.Append($"{N(cx)} {N(cy)} l S\n");
            }
        }


        private static void ArcPath(StringBuilder sb, float cx, float cy, float r, float a0, float a1, bool moveToStart)
        {
            // Approximate circular arc with cubic beziers, split into <= 90° segments
            float sweep = a1 - a0;
            int segs = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (float)(Math.PI / 2)));
            float da = sweep / segs;
            float ang = a0;

            for (int i = 0; i < segs; i++)
            {
                float aStart = ang;
                float aEnd = ang + da;
                // control point factor
                float t = (float)Math.Tan((aEnd - aStart) / 4f) * 4f / 3f;
                float x0 = cx + r * (float)Math.Cos(aStart);
                float y0 = cy + r * (float)Math.Sin(aStart);
                float x3 = cx + r * (float)Math.Cos(aEnd);
                float y3 = cy + r * (float)Math.Sin(aEnd);
                float dx0 = -r * (float)Math.Sin(aStart);
                float dy0 = r * (float)Math.Cos(aStart);
                float dx1 = -r * (float)Math.Sin(aEnd);
                float dy1 = r * (float)Math.Cos(aEnd);
                float x1 = x0 + dx0 * t;
                float y1 = y0 + dy0 * t;
                float x2 = x3 - dx1 * t;
                float y2 = y3 - dy1 * t;

                if (i == 0 && moveToStart) sb.Append($"{N(x0)} {N(y0)} m ");
                sb.Append($"{N(x1)} {N(y1)} {N(x2)} {N(y2)} {N(x3)} {N(y3)} c ");
                ang = aEnd;
            }
        }

        // Simple blue→white→red gradient for heatmap
        private static Color DefaultHeatColor(float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            if (t < 0.5f)
            {
                float u = t / 0.5f;
                return Color.FromArgb(
                    (int)(0 + u * (255 - 0)),
                    (int)(0 + u * (255 - 0)),
                    255);
            }
            else
            {
                float u = (t - 0.5f) / 0.5f;
                return Color.FromArgb(255, (int)(255 - u * 255), (int)(255 - u * 255));
            }
        }

        private static void DrawMarker(StringBuilder sb, MarkerShape shape, float cx, float cy, float size, Color fill, Color? stroke, float strokeW)
        {
            float r = size / 2f;
            switch (shape)
            {
                case MarkerShape.Circle:
                    FillCircle(sb, cx, cy, r, fill);
                    if (stroke.HasValue) StrokeCircle(sb, cx, cy, r, stroke.Value, strokeW);
                    break;
                case MarkerShape.Square:
                    FillRect(sb, cx - r, cy - r, size, size, fill);
                    if (stroke.HasValue) StrokeRect(sb, cx - r, cy - r, size, size, stroke.Value, strokeW);
                    break;
                case MarkerShape.Triangle:
                {
                    var p1 = (cx, cy - r);
                    var p2 = (cx + r, cy + r);
                    var p3 = (cx - r, cy + r);
                    sb.Append($"{ToRgbFill(fill)} {N(p1.Item1)} {N(p1.Item2)} m {N(p2.Item1)} {N(p2.Item2)} l {N(p3.Item1)} {N(p3.Item2)} l h f\n");
                    if (stroke.HasValue)
                    {
                        sb.Append($"{ToRgbStroke(stroke.Value)} {N(strokeW)} w {N(p1.Item1)} {N(p1.Item2)} m {N(p2.Item1)} {N(p2.Item2)} l {N(p3.Item1)} {N(p3.Item2)} l h S\n");
                    }
                }
                break;
                case MarkerShape.Diamond:
                {
                    var p1 = (cx, cy - r);
                    var p2 = (cx + r, cy);
                    var p3 = (cx, cy + r);
                    var p4 = (cx - r, cy);
                    sb.Append($"{ToRgbFill(fill)} {N(p1.Item1)} {N(p1.Item2)} m {N(p2.Item1)} {N(p2.Item2)} l {N(p3.Item1)} {N(p3.Item2)} l {N(p4.Item1)} {N(p4.Item2)} l h f\n");
                    if (stroke.HasValue)
                    {
                        sb.Append($"{ToRgbStroke(stroke.Value)} {N(strokeW)} w {N(p1.Item1)} {N(p1.Item2)} m {N(p2.Item1)} {N(p2.Item2)} l {N(p3.Item1)} {N(p3.Item2)} l {N(p4.Item1)} {N(p4.Item2)} l h S\n");
                    }
                }
                break;
                case MarkerShape.Cross:
                {
                    float d = r;
                    if (stroke.HasValue)
                    {
                        sb.Append($"{ToRgbStroke(stroke.Value)} {N(strokeW)} w {N(cx - d)} {N(cy - d)} m {N(cx + d)} {N(cy + d)} l S\n");
                        sb.Append($"{ToRgbStroke(stroke.Value)} {N(strokeW)} w {N(cx - d)} {N(cy + d)} m {N(cx + d)} {N(cy - d)} l S\n");
                    }
                }
                break;
                case MarkerShape.Plus:
                {
                    float d = r;
                    if (stroke.HasValue)
                    {
                        sb.Append($"{ToRgbStroke(stroke.Value)} {N(strokeW)} w {N(cx - d)} {N(cy)} m {N(cx + d)} {N(cy)} l S\n");
                        sb.Append($"{ToRgbStroke(stroke.Value)} {N(strokeW)} w {N(cx)} {N(cy - d)} m {N(cx)} {N(cy + d)} l S\n");
                    }
                }
                break;
            }
        }
    }
}
