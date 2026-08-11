using System.Text;
using PdfBuilder.Document;
using PdfBuilder.Encoder;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Writer.Charts;

internal static class ChartLabelRenderer
{
    public static float Draw(StringBuilder content, PdfRenderContext context, string text, string fontFamily, float size, PdfColor color, float x, float y, bool centered = false)
    {
        ShapedRun? run = Shape(text, fontFamily, size);
        if (run == null || run.Glyphs.Count == 0) return 0f;
        var encoded = GlyphRunEncoder.Encode(run, context);
        float textX = centered ? x - run.Width / 2f : x;
        content.Append($"BT {encoded.FontResourceName} {ChartDrawing.Number(run.FontSize)} Tf {ChartDrawing.Fill(color)} {ChartDrawing.Number(textX)} {ChartDrawing.Number(y)} Td {encoded.TjCommand} ET\n");
        return run.Width;
    }

    private static ShapedRun? Shape(string text, string fontFamily, float size)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var request = new TextShapingRequest(text, fontFamily, size, 1f, float.PositiveInfinity, false, false, false, false, null, FlowDirection.LeftToRight);
        return TextShaper.Shared.ShapeParagraph(request).Lines.FirstOrDefault()?.Runs.FirstOrDefault();
    }
}
