using PdfBuilder.Document;

namespace PdfBuilder.Elements;

internal sealed class ClipGroupElement : PdfElement
{
    internal ClipGroupElement(float x, float y, float width, float height, IReadOnlyList<PdfElement> children)
        : base(x, y)
    {
        Width = Math.Max(0f, width);
        Height = Math.Max(0f, height);
        Children = children ?? throw new ArgumentNullException(nameof(children));
    }

    internal float Width { get; }
    internal float Height { get; }
    internal IReadOnlyList<PdfElement> Children { get; }
}
