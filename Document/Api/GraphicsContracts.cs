namespace PdfBuilder.Document;

/// <summary>Available canvas dimensions in PDF points.</summary>
public readonly record struct CanvasSize(float Width, float Height);

/// <summary>Stable canvas paint layers. Layers render background, content, then foreground.</summary>
public enum CanvasLayer
{
    Background,
    Content,
    Foreground
}

/// <summary>Built-in line patterns for canonical canvas strokes.</summary>
public enum CanvasLinePattern
{
    Solid,
    Dashed,
    Dotted
}

/// <summary>
/// Records bounded vector drawing operations inside a canonical canvas.
/// Coordinates use a bottom-left origin and PDF point units.
/// </summary>
public interface ICanvasDescriptor
{
    /// <summary>Gets the final canvas size available to the drawing callback.</summary>
    CanvasSize Size { get; }
    /// <summary>Saves the current graphics state. Every save must have a matching restore.</summary>
    ICanvasDescriptor Save();
    /// <summary>Restores the most recently saved graphics state.</summary>
    ICanvasDescriptor Restore();
    /// <summary>Runs drawing operations inside an automatically balanced graphics state.</summary>
    ICanvasDescriptor State(Action<ICanvasDescriptor> draw);
    /// <summary>Applies a finite affine matrix. Transform calls compose in the order supplied.</summary>
    ICanvasDescriptor Transform(float a, float b, float c, float d, float e, float f);
    /// <summary>Translates subsequent drawing operations.</summary>
    ICanvasDescriptor Translate(float x, float y);
    /// <summary>Rotates subsequent drawing operations around the origin.</summary>
    ICanvasDescriptor Rotate(float degrees);
    /// <summary>Rotates subsequent drawing operations around a specified point.</summary>
    ICanvasDescriptor Rotate(float degrees, float centerX, float centerY);
    /// <summary>Scales subsequent drawing operations. Scale values must be non-zero and finite.</summary>
    ICanvasDescriptor Scale(float x, float y);
    /// <summary>Flips subsequent drawing horizontally inside the canvas bounds.</summary>
    ICanvasDescriptor FlipHorizontal();
    /// <summary>Flips subsequent drawing vertically inside the canvas bounds.</summary>
    ICanvasDescriptor FlipVertical();
    /// <summary>Clips subsequent drawing to a rectangle.</summary>
    ICanvasDescriptor ClipRectangle(float x, float y, float width, float height);
    /// <summary>Sets the stroke colour from a theme token or hexadecimal colour.</summary>
    ICanvasDescriptor StrokeColor(string color);
    /// <summary>Sets the fill colour from a theme token or hexadecimal colour.</summary>
    ICanvasDescriptor FillColor(string color);
    /// <summary>Sets the stroke width in points.</summary>
    ICanvasDescriptor LineWidth(float width);
    /// <summary>Sets a solid, dashed, or dotted stroke pattern.</summary>
    ICanvasDescriptor LinePattern(CanvasLinePattern pattern, float dashLength = 4f, float gapLength = 2f, float phase = 0f);
    /// <summary>Starts a path at the supplied point.</summary>
    ICanvasDescriptor MoveTo(float x, float y);
    /// <summary>Adds a straight path segment.</summary>
    ICanvasDescriptor LineTo(float x, float y);
    /// <summary>Adds a cubic Bezier path segment.</summary>
    ICanvasDescriptor CurveTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y);
    /// <summary>Closes the current path.</summary>
    ICanvasDescriptor ClosePath();
    /// <summary>Strokes the current path.</summary>
    ICanvasDescriptor Stroke();
    /// <summary>Fills the current path.</summary>
    ICanvasDescriptor Fill();
    /// <summary>Fills and strokes the current path.</summary>
    ICanvasDescriptor FillAndStroke();
    /// <summary>Draws a line using the current stroke settings.</summary>
    ICanvasDescriptor Line(float x1, float y1, float x2, float y2);
    /// <summary>Draws a rectangle using the current stroke and fill settings.</summary>
    ICanvasDescriptor Rectangle(float x, float y, float width, float height, bool stroke = true, bool fill = false);
    /// <summary>Draws a circle using the current stroke and fill settings.</summary>
    ICanvasDescriptor Circle(float centerX, float centerY, float radius, bool stroke = true, bool fill = false);
    /// <summary>Paints a bounded vector approximation of a linear gradient.</summary>
    ICanvasDescriptor LinearGradient(float x, float y, float width, float height, string startColor, string endColor, float angleDegrees = 0f, int steps = 32);
    /// <summary>Paints a bounded concentric vector approximation of a radial gradient.</summary>
    ICanvasDescriptor RadialGradient(float centerX, float centerY, float radius, string centerColor, string edgeColor, int steps = 32);
    /// <summary>Paints a bounded vector rectangle shadow behind subsequent content.</summary>
    ICanvasDescriptor RectangleShadow(float x, float y, float width, float height, string color, float offsetX = 2f, float offsetY = -2f, float blurRadius = 4f, int steps = 8);
    /// <summary>Records operations into an explicit paint layer.</summary>
    ICanvasDescriptor Layer(CanvasLayer layer, Action<ICanvasDescriptor> draw);
}
