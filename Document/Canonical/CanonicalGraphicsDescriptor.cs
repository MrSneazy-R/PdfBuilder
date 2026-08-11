using PdfBuilder.Document.Layout;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalCanvasDescriptor : ICanvasDescriptor
    {
        private readonly CanvasBuilder _builder;
        private readonly DocumentTheme _theme;

        public CanonicalCanvasDescriptor(CanvasBuilder builder, CanvasSize size, DocumentTheme theme)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            Size = size;
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        public CanvasSize Size { get; }
        public ICanvasDescriptor Save() { _builder.SaveState(); return this; }
        public ICanvasDescriptor Restore() { _builder.RestoreState(); return this; }
        public ICanvasDescriptor State(Action<ICanvasDescriptor> draw)
        {
            ArgumentNullException.ThrowIfNull(draw);
            _builder.State(_ => draw(this));
            return this;
        }
        public ICanvasDescriptor Transform(float a, float b, float c, float d, float e, float f) { _builder.Transform(a, b, c, d, e, f); return this; }
        public ICanvasDescriptor Translate(float x, float y) { _builder.Translate(x, y); return this; }
        public ICanvasDescriptor Rotate(float degrees) { _builder.Rotate(degrees); return this; }
        public ICanvasDescriptor Rotate(float degrees, float centerX, float centerY) { _builder.Rotate(degrees, centerX, centerY); return this; }
        public ICanvasDescriptor Scale(float x, float y) { _builder.Scale(x, y); return this; }
        public ICanvasDescriptor FlipHorizontal() { _builder.FlipHorizontal(); return this; }
        public ICanvasDescriptor FlipVertical() { _builder.FlipVertical(); return this; }
        public ICanvasDescriptor ClipRectangle(float x, float y, float width, float height) { _builder.ClipRectangle(x, y, width, height); return this; }
        public ICanvasDescriptor StrokeColor(string color) { _builder.StrokeColor(ResolveColor(color)); return this; }
        public ICanvasDescriptor FillColor(string color) { _builder.FillColor(ResolveColor(color)); return this; }
        public ICanvasDescriptor LineWidth(float width) { _builder.LineWidth(width); return this; }
        public ICanvasDescriptor LinePattern(CanvasLinePattern pattern, float dashLength = 4f, float gapLength = 2f, float phase = 0f) { _builder.LinePattern(pattern, dashLength, gapLength, phase); return this; }
        public ICanvasDescriptor MoveTo(float x, float y) { _builder.MoveTo(x, y); return this; }
        public ICanvasDescriptor LineTo(float x, float y) { _builder.LineTo(x, y); return this; }
        public ICanvasDescriptor CurveTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y) { _builder.CurveTo(control1X, control1Y, control2X, control2Y, x, y); return this; }
        public ICanvasDescriptor ClosePath() { _builder.ClosePath(); return this; }
        public ICanvasDescriptor Stroke() { _builder.Stroke(); return this; }
        public ICanvasDescriptor Fill() { _builder.Fill(); return this; }
        public ICanvasDescriptor FillAndStroke() { _builder.FillAndStroke(); return this; }
        public ICanvasDescriptor Line(float x1, float y1, float x2, float y2) { _builder.Line(x1, y1, x2, y2); return this; }
        public ICanvasDescriptor Rectangle(float x, float y, float width, float height, bool stroke = true, bool fill = false) { _builder.Rect(x, y, width, height, stroke, fill); return this; }
        public ICanvasDescriptor Circle(float centerX, float centerY, float radius, bool stroke = true, bool fill = false) { _builder.Circle(centerX, centerY, radius, stroke, fill); return this; }
        public ICanvasDescriptor LinearGradient(float x, float y, float width, float height, string startColor, string endColor, float angleDegrees = 0f, int steps = 32)
        {
            _builder.LinearGradient(x, y, width, height, ResolveColor(startColor), ResolveColor(endColor), angleDegrees, steps);
            return this;
        }
        public ICanvasDescriptor RadialGradient(float centerX, float centerY, float radius, string centerColor, string edgeColor, int steps = 32)
        {
            _builder.RadialGradient(centerX, centerY, radius, ResolveColor(centerColor), ResolveColor(edgeColor), steps);
            return this;
        }
        public ICanvasDescriptor RectangleShadow(float x, float y, float width, float height, string color, float offsetX = 2f, float offsetY = -2f, float blurRadius = 4f, int steps = 8)
        {
            _builder.RectangleShadow(x, y, width, height, ResolveColor(color), offsetX, offsetY, blurRadius, steps);
            return this;
        }
        public ICanvasDescriptor Layer(CanvasLayer layer, Action<ICanvasDescriptor> draw)
        {
            ArgumentNullException.ThrowIfNull(draw);
            _builder.Layer(layer, _ => draw(this));
            return this;
        }

        private string ResolveColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
                throw new ArgumentException("A theme colour token or hexadecimal colour is required.", nameof(color));
            return _theme.ResolveColor(color);
        }
    }
}
