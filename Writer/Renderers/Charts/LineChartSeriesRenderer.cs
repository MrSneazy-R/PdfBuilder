using System.Text;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal static class LineChartSeriesRenderer
{
    public static void Append(StringBuilder content, CanonicalChartModel model, ChartLayout layout, ChartScales scales, PdfRenderContext context)
    {
        int seriesIndex = 0;
        foreach (CanonicalLineSeries series in model.Series.OfType<CanonicalLineSeries>())
        {
            if (series.Values.Count == 0) { seriesIndex++; continue; }
            var points = series.Values.Select((value, index) => (
                X: scales.CategoryCenter(index, layout.Plot),
                Y: (series.SecondaryAxis ? scales.SecondaryY ?? scales.PrimaryY : scales.PrimaryY).Map(value),
                Value: value)).ToArray();
            PdfBuilder.Models.PdfColor color = series.Color ?? model.Palette[model.Series.IndexOf(series) % model.Palette.Count];

            if (series.Area && points.Length > 1)
            {
                PdfBuilder.Models.PdfColor fill = ChartDrawing.WithAlphaBlendedOnWhite(series.Fill ?? new PdfBuilder.Models.PdfColor(color.Red, color.Green, color.Blue, 90));
                float baseline = (series.SecondaryAxis ? scales.SecondaryY ?? scales.PrimaryY : scales.PrimaryY).Map(0f);
                content.Append($"{ChartDrawing.Fill(fill)} {ChartDrawing.Number(points[0].X)} {ChartDrawing.Number(baseline)} m ");
                content.Append($"{ChartDrawing.Number(points[0].X)} {ChartDrawing.Number(points[0].Y)} l ");
                AppendPath(content, points, series.Smooth, series.SmoothTension);
                content.Append($"{ChartDrawing.Number(points[^1].X)} {ChartDrawing.Number(baseline)} l h f\n");
            }

            if (points.Length > 1)
            {
                content.Append($"{ChartDrawing.Stroke(color)} {ChartDrawing.Number(series.StrokeWidth)} w {ChartDrawing.Number(points[0].X)} {ChartDrawing.Number(points[0].Y)} m ");
                AppendPath(content, points, series.Smooth, series.SmoothTension);
                content.Append("S\n");
            }

            foreach (var point in points)
            {
                if (series.Marker != Document.ChartMarkerShape.None)
                    ChartDrawing.Marker(content, series.Marker, point.X, point.Y, series.MarkerSize, series.MarkerFill ?? color, color);
                if (series.ShowLabels)
                    ChartLabelRenderer.Draw(content, context, series.LabelFormatter(point.Value), model.FontFamily, model.FontSize, model.TextColor, point.X, point.Y + 5f, centered: true);
            }
            seriesIndex++;
        }
    }

    private static void AppendPath(StringBuilder content, IReadOnlyList<(float X, float Y, float Value)> points, bool smooth, float tension)
    {
        if (!smooth)
        {
            for (int index = 1; index < points.Count; index++)
                content.Append($"{ChartDrawing.Number(points[index].X)} {ChartDrawing.Number(points[index].Y)} l ");
            return;
        }

        float amount = Math.Clamp(tension, 0f, 1f) / 6f;
        for (int index = 0; index < points.Count - 1; index++)
        {
            var p0 = index == 0 ? points[0] : points[index - 1];
            var p1 = points[index];
            var p2 = points[index + 1];
            var p3 = index + 2 < points.Count ? points[index + 2] : points[index + 1];
            float c1x = p1.X + (p2.X - p0.X) * amount;
            float c1y = p1.Y + (p2.Y - p0.Y) * amount;
            float c2x = p2.X - (p3.X - p1.X) * amount;
            float c2y = p2.Y - (p3.Y - p1.Y) * amount;
            content.Append($"{ChartDrawing.Number(c1x)} {ChartDrawing.Number(c1y)} {ChartDrawing.Number(c2x)} {ChartDrawing.Number(c2y)} {ChartDrawing.Number(p2.X)} {ChartDrawing.Number(p2.Y)} c ");
        }
    }
}
