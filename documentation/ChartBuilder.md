ChartBuilder
============

Purpose
-------
The preferred `IContainer.Chart(...)` API produces core and advanced vector charts without exposing `ChartElement` or `System.Drawing`. Core series are line, area, grouped/stacked/100%-stacked bar, pie, donut, and scatter. Typed advanced descriptors cover bubble, waterfall, radar, funnel, Gantt, candlestick, bullet/KPI, Pareto, range area, error bars, histogram, box plot, and heatmap.

```csharp
column.Item().Chart(chart =>
{
    chart.Size(480, 220);
    chart.Categories("Q1", "Q2", "Q3", "Q4");
    chart.Pareto("Defects", new[] { 42f, 24f, 11f, 5f })
        .CumulativeStyle(PdfColor.Parse("#C53030"), 1.5f);
});
```

All data is materialised and checked for finite values during composition. Invalid OHLC ranges, negative error extents, overlapping bullet ranges, and other series-specific errors fail before output. Core and advanced series cannot currently be mixed in the same chart; use adjacent chart containers. Theme palettes and `PdfColor` remain the public colour path.

Legacy builder
--------------
`ChartBuilder` remains available as a compatibility adapter for applications using raw `ChartElement` types. Obtain it from `ColumnBuilder.Chart(x, y, width, height)` or equivalent legacy helpers.

Common Configuration
--------------------
- Position & size: `X`, `Y`, `Width`, `Height`, `Padding(top,right,bottom,left)`.
- Title: `Title(string)`, `TitleFont(string family, float size)`.
- Axes: `CategoryX(labels...)`, `NumericX(min?, max?, ticks, formatter)`, `NumericY(...)`, `SecondaryNumericY(...)`, `UseSecondaryYForLast()`, `XLabelRotation`.
- Look & legend: `GridX`, `GridY`, `Legend(bool)`, `LegendPosition`, `Axis(color,width)`, `Grid(color)`, `LabelsFont`, `Palette`.

Series Creation
---------------
- Bars & lines: `AddBars`, `AddLine`, plus modifiers such as `BarCornerRadius`, `AlternateBarColors`, `StackBars`, `HorizontalBars`, `NormalizeBarsTo100`, `BarValueLabels`, `Smooth`, `StepLine`, `LineMarkers`, `FillUnder`, `LineValueLabels`, `StackArea`.
- Pie & donut: `AddPie`, `PieSlice`, `Donut`, `PieStartAngle`, `PieClockwise`, `PieLabels`, `PieAppendPercentages`, `PieLabelStyle`, `PieSliceStyle`.
- Scatter & bubble: `AddScatter`, `AddScatterCategories`, `ScatterMarkers`, `AddBubble`, `BubbleCategories`, `BubbleSizeRange`, `BubbleSizeDomain`, `BubblePoint`, `BubbleLabels`, `BubbleShadow`, `BubbleLegendPerPoint`.
- Range/area: `AddRangeArea`, `RangePoint`, `RangeSmooth`, `RangeOutline`.
- Error bars: `AddErrorBars`, `ErrorPoint(...)`.
- Special series: `AddWaterfall`, `WaterStep`; `AddHistogram`, `HistogramBins`, `HistogramPreBinned`, `HistogramGap`, `HistogramValueLabels`; `AddBoxPlot`, `BoxGroup`, `BoxStats`; `AddHeatmap`, `HeatmapDomain`; `AddRadar`, `RadarScale`, `RadarMarkers`; `AddFunnel`, `FunnelStage`, `FunnelLabelStyle`; `AddCandles`, `Candle`; `AddBullet`, `BulletRanges`, `BulletLook`; `AddPareto`, `ParetoItem`, `ParetoUseRightAxis`; `AddGantt`, `GanttTask`.

Finishing
---------
After configuration, call `Add()` on the `ChartBuilder` (via `ColumnBuilder.AddChart`) to append the element. Charts respect HarfBuzz text shaping for labels and integrate with pagination.

Example
-------
```csharp
col.Chart(page.MarginLeft, col.GetCurrentY(), width: 480, height: 260)
   .Title("Monthly Revenue vs Target")
   .NumericX(min: 1, max: 12, ticks: 12, formatter: month => $"M{month:0}")
   .NumericY(min: 0, max: 1500, ticks: 6, formatter: v => $"${v:0}k")
   .GridY(true).LegendPosition(ChartElement.LegendPos.Below)
   .Palette(Color.FromArgb(0x3B82F6), Color.FromArgb(0x10B981))
   .AddBars("Actual", Color.FromArgb(0x3B82F6), Color.FromArgb(0x1E3A8A), 0.5f,
       820, 910, 980, 1040, 1110, 1180, 1230, 1290, 1360, 1405, 1440, 1485)
   .BarValueLabels(show: true, pos: BarValueLabelPos.OutsideEnd, formatter: v => $"${v:0}k")
   .AddLine("Target", Color.FromArgb(0x10B981), 1.6f,
       800, 850, 900, 950, 1000, 1050, 1100, 1150, 1200, 1250, 1300, 1350)
   .LineMarkers(show: true, size: 4f, fill: Color.White)
   .LineValueLabels(show: true, onlyLast: true, formatter: pt => $"${pt.Y:0}k");
```

Expected Outcome
----------------
- A combined column/line chart with 12 month labels along the X axis and a 0–1500k Y axis.
- Blue bars display monthly actual revenue with value labels above each column.
- A green line overlays targets with circular markers and a single label on the final point.
- Legend appears below the plot distinguishing "Actual" and "Target".
- Grid lines align with Y ticks, aiding at-a-glance comparison.
