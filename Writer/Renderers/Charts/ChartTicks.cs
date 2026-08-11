namespace PdfBuilder.Writer.Charts;

internal static class ChartTicks
{
    public static (float Minimum, float Maximum, IReadOnlyList<float> Values) Create(float minimum, float maximum, int desired)
    {
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum)) return (0f, 1f, [0f, 1f]);
        if (Math.Abs(maximum - minimum) < 0.000001f) { minimum -= 0.5f; maximum += 0.5f; }
        float range = NiceNumber(maximum - minimum, false);
        float step = NiceNumber(range / Math.Max(1, desired - 1), true);
        float niceMinimum = (float)Math.Floor(minimum / step) * step;
        float niceMaximum = (float)Math.Ceiling(maximum / step) * step;
        var values = new List<float>();
        for (float value = niceMinimum; value <= niceMaximum + step * 0.25f; value += step)
            values.Add(value);
        return (niceMinimum, niceMaximum, values);
    }

    private static float NiceNumber(float value, bool round)
    {
        double exponent = Math.Floor(Math.Log10(Math.Max(value, double.Epsilon)));
        double fraction = value / Math.Pow(10d, exponent);
        double nice = round
            ? fraction < 1.5d ? 1d : fraction < 3d ? 2d : fraction < 7d ? 5d : 10d
            : fraction <= 1d ? 1d : fraction <= 2d ? 2d : fraction <= 5d ? 5d : 10d;
        return (float)(nice * Math.Pow(10d, exponent));
    }
}
