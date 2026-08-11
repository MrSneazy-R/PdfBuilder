using System.Globalization;
using System.Text;
using PdfBuilder.Document;
using PdfBuilder.Models;

namespace PdfBuilder.Writer.Charts;

internal static class ChartDrawing
{
    public static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    public static string Stroke(PdfColor color) => $"{Channel(color.Red)} {Channel(color.Green)} {Channel(color.Blue)} RG";
    public static string Fill(PdfColor color) => $"{Channel(color.Red)} {Channel(color.Green)} {Channel(color.Blue)} rg";

    public static void Rectangle(StringBuilder content, float x, float y, float width, float height, PdfColor fill, PdfColor? stroke = null, float strokeWidth = 0.5f)
    {
        content.Append($"{Fill(fill)} {Number(x)} {Number(y)} {Number(width)} {Number(height)} re f\n");
        if (stroke.HasValue)
            content.Append($"{Stroke(stroke.Value)} {Number(strokeWidth)} w {Number(x)} {Number(y)} {Number(width)} {Number(height)} re S\n");
    }

    public static void Line(StringBuilder content, float x1, float y1, float x2, float y2, PdfColor color, float width = 0.5f)
        => content.Append($"{Stroke(color)} {Number(width)} w {Number(x1)} {Number(y1)} m {Number(x2)} {Number(y2)} l S\n");

    public static void Marker(StringBuilder content, ChartMarkerShape shape, float x, float y, float size, PdfColor fill, PdfColor stroke)
    {
        float radius = Math.Max(0.5f, size / 2f);
        switch (shape)
        {
            case ChartMarkerShape.None: return;
            case ChartMarkerShape.Square:
                Rectangle(content, x - radius, y - radius, size, size, fill, stroke);
                return;
            case ChartMarkerShape.Triangle:
                content.Append($"{Fill(fill)} {Stroke(stroke)} {Number(x)} {Number(y + radius)} m {Number(x + radius)} {Number(y - radius)} l {Number(x - radius)} {Number(y - radius)} l h B\n");
                return;
            case ChartMarkerShape.Diamond:
                content.Append($"{Fill(fill)} {Stroke(stroke)} {Number(x)} {Number(y + radius)} m {Number(x + radius)} {Number(y)} l {Number(x)} {Number(y - radius)} l {Number(x - radius)} {Number(y)} l h B\n");
                return;
            case ChartMarkerShape.Cross:
                Line(content, x - radius, y - radius, x + radius, y + radius, stroke, 1f);
                Line(content, x - radius, y + radius, x + radius, y - radius, stroke, 1f);
                return;
            case ChartMarkerShape.Plus:
                Line(content, x - radius, y, x + radius, y, stroke, 1f);
                Line(content, x, y - radius, x, y + radius, stroke, 1f);
                return;
            default:
                const float k = 0.55228475f;
                content.Append($"{Fill(fill)} {Stroke(stroke)} {Number(x + radius)} {Number(y)} m ");
                content.Append($"{Number(x + radius)} {Number(y + k * radius)} {Number(x + k * radius)} {Number(y + radius)} {Number(x)} {Number(y + radius)} c ");
                content.Append($"{Number(x - k * radius)} {Number(y + radius)} {Number(x - radius)} {Number(y + k * radius)} {Number(x - radius)} {Number(y)} c ");
                content.Append($"{Number(x - radius)} {Number(y - k * radius)} {Number(x - k * radius)} {Number(y - radius)} {Number(x)} {Number(y - radius)} c ");
                content.Append($"{Number(x + k * radius)} {Number(y - radius)} {Number(x + radius)} {Number(y - k * radius)} {Number(x + radius)} {Number(y)} c B\n");
                return;
        }
    }

    public static PdfColor WithAlphaBlendedOnWhite(PdfColor color)
    {
        if (color.Alpha == 255) return color;
        float alpha = color.Alpha / 255f;
        return new PdfColor(
            (byte)Math.Round(color.Red * alpha + 255f * (1f - alpha)),
            (byte)Math.Round(color.Green * alpha + 255f * (1f - alpha)),
            (byte)Math.Round(color.Blue * alpha + 255f * (1f - alpha)));
    }

    private static string Channel(byte value) => (value / 255f).ToString("0.###", CultureInfo.InvariantCulture);

}
