using System.Text;
using PdfBuilder.Elements;

namespace PdfBuilder.Writer;

internal static class ChartRenderer
{
    public static void Append(StringBuilder content, ChartElement chart, PdfRenderContext context)
    {
        if (chart.CanonicalModel != null)
            Charts.CanonicalChartRenderer.Append(content, chart, chart.CanonicalModel, context);
        else
            LegacyChartRenderer.Append(content, chart, context);
    }
}
