using System.Text;
using PdfBuilder.Elements.CanonicalCharts;

namespace PdfBuilder.Writer.Charts;

internal static class PieChartSeriesRenderer
{
    public static void Append(StringBuilder content, CanonicalChartModel model, ChartLayout layout, PdfRenderContext context)
    {
        CanonicalPieSeries[] pies = model.Series.OfType<CanonicalPieSeries>().ToArray();
        if (pies.Length == 0) return;
        float plotWidth = layout.Plot.Width / pies.Length;
        for (int pieIndex = 0; pieIndex < pies.Length; pieIndex++)
        {
            CanonicalPieSeries series = pies[pieIndex];
            float total = series.Values.Sum(value => value.Value);
            if (total <= 0f) continue;
            float centerX = layout.Plot.Left + plotWidth * (pieIndex + 0.5f);
            float centerY = layout.Plot.Bottom + layout.Plot.Height / 2f;
            float radius = Math.Max(1f, Math.Min(plotWidth, layout.Plot.Height) / 2f - 10f);
            float angle = series.StartAngle;
            for (int index = 0; index < series.Values.Count; index++)
            {
                Document.ChartValue value = series.Values[index];
                float sweep = value.Value / total * 360f;
                PdfBuilder.Models.PdfColor color = index < series.SliceColors.Count ? series.SliceColors[index] : model.Palette[index % model.Palette.Count];
                AppendSector(content, centerX, centerY, radius, radius * series.InnerRatio, angle, angle + sweep, color);
                if (series.ShowLabels && sweep > 0.5f)
                {
                    float middle = (angle + sweep / 2f) * MathF.PI / 180f;
                    float labelRadius = radius * (series.LabelsOutside ? 1.1f : (1f + series.InnerRatio) / 2f);
                    float x = centerX + MathF.Cos(middle) * labelRadius;
                    float y = centerY + MathF.Sin(middle) * labelRadius;
                    ChartLabelRenderer.Draw(content, context, series.LabelFormatter(value), model.FontFamily, model.FontSize, model.TextColor, x, y, centered: true);
                }
                angle += sweep;
            }
        }
    }

    private static void AppendSector(StringBuilder content, float cx, float cy, float outer, float inner, float startDegrees, float endDegrees, PdfBuilder.Models.PdfColor color)
    {
        float start = startDegrees * MathF.PI / 180f;
        float end = endDegrees * MathF.PI / 180f;
        float sweep = end - start;
        int segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (MathF.PI / 2f)));
        float step = sweep / segments;
        float startX = cx + MathF.Cos(start) * outer;
        float startY = cy + MathF.Sin(start) * outer;
        content.Append($"{ChartDrawing.Fill(color)} {ChartDrawing.Number(startX)} {ChartDrawing.Number(startY)} m ");
        AppendArc(content, cx, cy, outer, start, step, segments);
        if (inner > 0f)
        {
            float innerEndX = cx + MathF.Cos(end) * inner;
            float innerEndY = cy + MathF.Sin(end) * inner;
            content.Append($"{ChartDrawing.Number(innerEndX)} {ChartDrawing.Number(innerEndY)} l ");
            AppendArc(content, cx, cy, inner, end, -step, segments);
        }
        else content.Append($"{ChartDrawing.Number(cx)} {ChartDrawing.Number(cy)} l ");
        content.Append("h f\n");
    }

    private static void AppendArc(StringBuilder content, float cx, float cy, float radius, float angle, float step, int segments)
    {
        for (int index = 0; index < segments; index++)
        {
            float next = angle + step;
            float k = 4f / 3f * MathF.Tan(step / 4f);
            float x0 = cx + MathF.Cos(angle) * radius;
            float y0 = cy + MathF.Sin(angle) * radius;
            float x3 = cx + MathF.Cos(next) * radius;
            float y3 = cy + MathF.Sin(next) * radius;
            float c1x = x0 - MathF.Sin(angle) * radius * k;
            float c1y = y0 + MathF.Cos(angle) * radius * k;
            float c2x = x3 + MathF.Sin(next) * radius * k;
            float c2y = y3 - MathF.Cos(next) * radius * k;
            content.Append($"{ChartDrawing.Number(c1x)} {ChartDrawing.Number(c1y)} {ChartDrawing.Number(c2x)} {ChartDrawing.Number(c2y)} {ChartDrawing.Number(x3)} {ChartDrawing.Number(y3)} c ");
            angle = next;
        }
    }
}
