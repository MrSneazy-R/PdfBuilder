using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components;

internal sealed class NavigationAnchorComponent : IMeasurable
{
    private readonly string _id;
    private readonly string? _title;
    private readonly int _level;

    internal NavigationAnchorComponent(string id, string? title, int level)
    {
        _id = id;
        _title = title;
        _level = Math.Max(1, level);
    }

    public LayoutMeasurement Measure(LayoutMeasureContext context)
        => new(0f, 0f, 0f, 0f);

    public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        => context.Page.AddElement(new AnchorElement(_id, context.ContentLeft, context.ContentTop)
        {
            Title = _title,
            Level = _level
        });
}
