using System.Globalization;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout;

/// <summary>Low-level vector command builder used by legacy and canonical canvas adapters.</summary>
public sealed class CanvasBuilder
{
    private const float CircleKappa = 0.55228475f;
    private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
    private readonly CanvasElement _element;
    private readonly PdfRenderLimits? _limits;
    private CanvasLayer _layer = CanvasLayer.Content;
    private int _stateDepth;

    internal CanvasBuilder(CanvasElement element, PdfRenderLimits? limits = null)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _limits = limits;
    }

    public CanvasBuilder Margin(float all)
    {
        ValidateNonNegative(all, nameof(all));
        _element.MarginTop = all;
        _element.MarginBottom = all;
        _element.MarginLeft = all;
        _element.MarginRight = all;
        return this;
    }

    public CanvasBuilder Margin(float left, float top, float right, float bottom)
    {
        ValidateNonNegative(left, nameof(left));
        ValidateNonNegative(top, nameof(top));
        ValidateNonNegative(right, nameof(right));
        ValidateNonNegative(bottom, nameof(bottom));
        _element.MarginLeft = left;
        _element.MarginTop = top;
        _element.MarginRight = right;
        _element.MarginBottom = bottom;
        return this;
    }

    public CanvasBuilder AvoidBreakInside(bool value = true)
    {
        _element.AvoidBreakInside = value;
        return this;
    }

    public CanvasBuilder Raw(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return this;

        if (!command.EndsWith("\n", StringComparison.Ordinal))
            command += "\n";
        _limits?.ValidateCanvasCommands(
            _element.CommandCount + 1,
            checked(_element.CommandBytes + System.Text.Encoding.UTF8.GetByteCount(command)));
        _element.CommandsFor(_layer).Add(command);
        return this;
    }

    public CanvasBuilder SaveState()
    {
        Raw("q");
        _stateDepth++;
        return this;
    }

    public CanvasBuilder RestoreState()
    {
        if (_stateDepth <= 0)
            throw new PdfDrawingException("Canvas graphics-state restore has no matching save.");
        Raw("Q");
        _stateDepth--;
        return this;
    }

    public CanvasBuilder State(Action<CanvasBuilder> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        SaveState();
        try
        {
            draw(this);
        }
        finally
        {
            RestoreState();
        }
        return this;
    }

    public CanvasBuilder Layer(CanvasLayer layer, Action<CanvasBuilder> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        CanvasLayer previous = _layer;
        int initialDepth = _stateDepth;
        _layer = layer;
        try
        {
            draw(this);
            if (_stateDepth != initialDepth)
                throw new PdfDrawingException($"Canvas layer '{layer}' left an unbalanced graphics state.");
        }
        finally
        {
            _layer = previous;
        }
        return this;
    }

    public CanvasBuilder Transform(float a, float b, float c, float d, float e, float f)
    {
        ValidateFinite(a, nameof(a));
        ValidateFinite(b, nameof(b));
        ValidateFinite(c, nameof(c));
        ValidateFinite(d, nameof(d));
        ValidateFinite(e, nameof(e));
        ValidateFinite(f, nameof(f));
        return Raw($"{N(a)} {N(b)} {N(c)} {N(d)} {N(e)} {N(f)} cm");
    }

    public CanvasBuilder Translate(float x, float y) => Transform(1, 0, 0, 1, x, y);

    public CanvasBuilder Rotate(float degrees)
    {
        ValidateFinite(degrees, nameof(degrees));
        double radians = degrees * Math.PI / 180d;
        float cosine = (float)Math.Cos(radians);
        float sine = (float)Math.Sin(radians);
        return Transform(cosine, sine, -sine, cosine, 0, 0);
    }

    public CanvasBuilder Rotate(float degrees, float centerX, float centerY)
    {
        ValidateFinite(centerX, nameof(centerX));
        ValidateFinite(centerY, nameof(centerY));
        Translate(centerX, centerY);
        Rotate(degrees);
        return Translate(-centerX, -centerY);
    }

    public CanvasBuilder Scale(float x, float y)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        if (Math.Abs(x) < float.Epsilon || Math.Abs(y) < float.Epsilon)
            throw new ArgumentOutOfRangeException(nameof(x), "Canvas scale values must be non-zero.");
        return Transform(x, 0, 0, y, 0, 0);
    }

    public CanvasBuilder FlipHorizontal() => Transform(-1, 0, 0, 1, _element.Width, 0);

    public CanvasBuilder FlipVertical() => Transform(1, 0, 0, -1, 0, _element.Height);

    public CanvasBuilder ClipRectangle(float x, float y, float width, float height)
    {
        ValidateRectangle(x, y, width, height);
        return Raw($"{N(x)} {N(y)} {N(width)} {N(height)} re W n");
    }

    public CanvasBuilder MoveTo(float x, float y)
    {
        ValidatePoint(x, y);
        return Raw($"{N(x)} {N(y)} m");
    }

    public CanvasBuilder LineTo(float x, float y)
    {
        ValidatePoint(x, y);
        return Raw($"{N(x)} {N(y)} l");
    }

    public CanvasBuilder CurveTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y)
    {
        ValidatePoint(control1X, control1Y);
        ValidatePoint(control2X, control2Y);
        ValidatePoint(x, y);
        return Raw($"{N(control1X)} {N(control1Y)} {N(control2X)} {N(control2Y)} {N(x)} {N(y)} c");
    }

    public CanvasBuilder ClosePath() => Raw("h");
    public CanvasBuilder Stroke() => Raw("S");
    public CanvasBuilder Fill() => Raw("f");
    public CanvasBuilder FillAndStroke() => Raw("B");

    public CanvasBuilder StrokeColor(string hex) => Raw($"{Rgb(hex)} RG");

    public CanvasBuilder FillColor(string hex) => Raw($"{Rgb(hex)} rg");

    public CanvasBuilder LineWidth(float width)
    {
        ValidatePositive(width, nameof(width));
        return Raw($"{N(width)} w");
    }

    public CanvasBuilder LinePattern(CanvasLinePattern pattern, float dashLength = 4f, float gapLength = 2f, float phase = 0f)
    {
        ValidateNonNegative(phase, nameof(phase));
        return pattern switch
        {
            CanvasLinePattern.Solid => Raw("[] 0 d"),
            CanvasLinePattern.Dashed => Dash(dashLength, gapLength, phase),
            CanvasLinePattern.Dotted => Dotted(gapLength, phase),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
    }

    public CanvasBuilder Dash(float dashLength, float gapLength, float phase = 0f)
    {
        ValidatePositive(dashLength, nameof(dashLength));
        ValidatePositive(gapLength, nameof(gapLength));
        ValidateNonNegative(phase, nameof(phase));
        return Raw($"0 J [{N(dashLength)} {N(gapLength)}] {N(phase)} d");
    }

    public CanvasBuilder Dotted(float gapLength = 2f, float phase = 0f)
    {
        ValidatePositive(gapLength, nameof(gapLength));
        ValidateNonNegative(phase, nameof(phase));
        return Raw($"1 J [0.01 {N(gapLength)}] {N(phase)} d");
    }

    public CanvasBuilder Line(float x1, float y1, float x2, float y2, float width, string? color = null)
    {
        if (!string.IsNullOrWhiteSpace(color))
            StrokeColor(color);
        LineWidth(width);
        return Line(x1, y1, x2, y2);
    }

    public CanvasBuilder Line(float x1, float y1, float x2, float y2)
    {
        MoveTo(x1, y1);
        LineTo(x2, y2);
        return Stroke();
    }

    public CanvasBuilder Rect(float x, float y, float width, float height, bool stroke = true, bool fill = false)
    {
        ValidateRectangle(x, y, width, height);
        Raw($"{N(x)} {N(y)} {N(width)} {N(height)} re");
        return Paint(stroke, fill);
    }

    public CanvasBuilder Circle(float centerX, float centerY, float radius, bool stroke = true, bool fill = false)
    {
        ValidatePoint(centerX, centerY);
        ValidatePositive(radius, nameof(radius));
        float control = radius * CircleKappa;
        MoveTo(centerX + radius, centerY);
        CurveTo(centerX + radius, centerY + control, centerX + control, centerY + radius, centerX, centerY + radius);
        CurveTo(centerX - control, centerY + radius, centerX - radius, centerY + control, centerX - radius, centerY);
        CurveTo(centerX - radius, centerY - control, centerX - control, centerY - radius, centerX, centerY - radius);
        CurveTo(centerX + control, centerY - radius, centerX + radius, centerY - control, centerX + radius, centerY);
        ClosePath();
        return Paint(stroke, fill);
    }

    public CanvasBuilder LinearGradient(
        float x,
        float y,
        float width,
        float height,
        string startColor,
        string endColor,
        float angleDegrees = 0f,
        int steps = 32)
    {
        ValidateRectangle(x, y, width, height);
        ValidateFinite(angleDegrees, nameof(angleDegrees));
        ValidateEffectSteps(steps);
        PdfColor start = ParseOpaque(startColor);
        PdfColor end = ParseOpaque(endColor);
        double radians = angleDegrees * Math.PI / 180d;
        float dx = (float)Math.Cos(radians);
        float dy = (float)Math.Sin(radians);
        float px = -dy;
        float py = dx;
        (float X, float Y)[] corners = [(x, y), (x + width, y), (x, y + height), (x + width, y + height)];
        float minimum = corners.Min(point => point.X * dx + point.Y * dy);
        float maximum = corners.Max(point => point.X * dx + point.Y * dy);
        float perpendicularMinimum = corners.Min(point => point.X * px + point.Y * py);
        float perpendicularMaximum = corners.Max(point => point.X * px + point.Y * py);

        return State(canvas =>
        {
            canvas.ClipRectangle(x, y, width, height);
            for (int index = 0; index < steps; index++)
            {
                float t0 = index / (float)steps;
                float t1 = (index + 1f) / steps;
                float projection0 = minimum + ((maximum - minimum) * t0);
                float projection1 = minimum + ((maximum - minimum) * t1);
                canvas.FillColor(Interpolate(start, end, (t0 + t1) / 2f).ToString());
                canvas.MoveTo((dx * projection0) + (px * perpendicularMinimum), (dy * projection0) + (py * perpendicularMinimum));
                canvas.LineTo((dx * projection1) + (px * perpendicularMinimum), (dy * projection1) + (py * perpendicularMinimum));
                canvas.LineTo((dx * projection1) + (px * perpendicularMaximum), (dy * projection1) + (py * perpendicularMaximum));
                canvas.LineTo((dx * projection0) + (px * perpendicularMaximum), (dy * projection0) + (py * perpendicularMaximum));
                canvas.ClosePath().Fill();
            }
        });
    }

    public CanvasBuilder RadialGradient(float centerX, float centerY, float radius, string centerColor, string edgeColor, int steps = 32)
    {
        ValidatePoint(centerX, centerY);
        ValidatePositive(radius, nameof(radius));
        ValidateEffectSteps(steps);
        PdfColor center = ParseOpaque(centerColor);
        PdfColor edge = ParseOpaque(edgeColor);
        for (int index = steps; index >= 1; index--)
        {
            float fraction = index / (float)steps;
            FillColor(Interpolate(center, edge, fraction).ToString());
            Circle(centerX, centerY, radius * fraction, stroke: false, fill: true);
        }
        return this;
    }

    public CanvasBuilder RectangleShadow(
        float x,
        float y,
        float width,
        float height,
        string color,
        float offsetX = 2f,
        float offsetY = -2f,
        float blurRadius = 4f,
        int steps = 8)
    {
        ValidateRectangle(x, y, width, height);
        ValidateFinite(offsetX, nameof(offsetX));
        ValidateFinite(offsetY, nameof(offsetY));
        ValidateNonNegative(blurRadius, nameof(blurRadius));
        ValidateEffectSteps(steps);
        PdfColor shadow = ParseOpaque(color);
        PdfColor paper = PdfColor.Rgb(255, 255, 255);
        for (int index = steps; index >= 1; index--)
        {
            float fraction = index / (float)steps;
            float spread = blurRadius * fraction;
            FillColor(Interpolate(shadow, paper, fraction * 0.82f).ToString());
            Rect(x + offsetX - spread, y + offsetY - spread, width + (spread * 2), height + (spread * 2), stroke: false, fill: true);
        }
        return this;
    }

    internal void Complete()
    {
        if (_stateDepth != 0)
            throw new PdfDrawingException($"Canvas drawing left {_stateDepth} unbalanced graphics-state save operation(s).");
        _limits?.ValidateCanvasCommands(_element.CommandCount, _element.CommandBytes);
    }

    private CanvasBuilder Paint(bool stroke, bool fill)
    {
        if (stroke && fill)
            return FillAndStroke();
        if (stroke)
            return Stroke();
        if (fill)
            return Fill();
        return Raw("n");
    }

    private void ValidateEffectSteps(int steps)
    {
        if (steps <= 0)
            throw new ArgumentOutOfRangeException(nameof(steps), "Canvas effect steps must be positive.");
        _element.MaximumEffectStepsUsed = Math.Max(_element.MaximumEffectStepsUsed, steps);
        _limits?.ValidateCanvasEffectSteps(steps);
    }

    private static PdfColor ParseOpaque(string color)
    {
        PdfColor parsed = PdfColor.Parse(color);
        if (parsed.Alpha != byte.MaxValue)
            throw new NotSupportedException("Canonical canvas vector colours must be opaque. Use bounded colour steps rather than alpha transparency.");
        return parsed;
    }

    private static PdfColor Interpolate(PdfColor start, PdfColor end, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return PdfColor.Rgb(
            (byte)Math.Round(start.Red + ((end.Red - start.Red) * amount)),
            (byte)Math.Round(start.Green + ((end.Green - start.Green) * amount)),
            (byte)Math.Round(start.Blue + ((end.Blue - start.Blue) * amount)));
    }

    private static string Rgb(string color)
    {
        PdfColor parsed = ParseOpaque(color);
        return $"{N(parsed.Red / 255d)} {N(parsed.Green / 255d)} {N(parsed.Blue / 255d)}";
    }

    private static void ValidateRectangle(float x, float y, float width, float height)
    {
        ValidatePoint(x, y);
        ValidatePositive(width, nameof(width));
        ValidatePositive(height, nameof(height));
    }

    private static void ValidatePoint(float x, float y)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
    }

    private static void ValidatePositive(float value, string name)
    {
        ValidateFinite(value, name);
        if (value <= 0f)
            throw new ArgumentOutOfRangeException(name, "Value must be positive.");
    }

    private static void ValidateNonNegative(float value, string name)
    {
        ValidateFinite(value, name);
        if (value < 0f)
            throw new ArgumentOutOfRangeException(name, "Value cannot be negative.");
    }

    private static void ValidateFinite(float value, string name)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Canvas values must be finite.");
    }

    private static string N(double value) => value.ToString("0.###", Inv);
}
