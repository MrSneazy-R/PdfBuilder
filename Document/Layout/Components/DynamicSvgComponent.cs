using System.Text;
using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components;

internal sealed class DynamicSvgComponent : IMeasurable
{
    private readonly float? _fixedWidth;
    private readonly float _height;
    private readonly Func<CanvasSize, string> _markupFactory;
    private readonly float _defaultSpacing;
    private CanvasSize? _lastSize;
    private ImageComponent? _imageComponent;

    public DynamicSvgComponent(float? fixedWidth, float height, Func<CanvasSize, string> markupFactory, float defaultSpacing)
    {
        if (fixedWidth.HasValue && (!float.IsFinite(fixedWidth.Value) || fixedWidth.Value <= 0f))
            throw new ArgumentOutOfRangeException(nameof(fixedWidth));
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height));
        _fixedWidth = fixedWidth;
        _height = height;
        _markupFactory = markupFactory ?? throw new ArgumentNullException(nameof(markupFactory));
        _defaultSpacing = defaultSpacing;
    }

    public LayoutMeasurement Measure(LayoutMeasureContext context)
    {
        float width = _fixedWidth ?? Math.Max(0f, context.AvailableWidth);
        var size = new CanvasSize(width, _height);
        if (!_lastSize.HasValue || _lastSize.Value != size || _imageComponent == null)
        {
            string markup = _markupFactory(size);
            if (string.IsNullOrWhiteSpace(markup))
                throw new PdfMediaException("Dynamic SVG generation returned empty markup.");
            context.Page.Owner?.RenderLimits.ValidateSvgBytes(Encoding.UTF8.GetByteCount(markup));
            SvgElement element;
            try
            {
                element = new SvgElement(markup, 0f, 0f, width, _height);
            }
            catch (InvalidDataException exception)
            {
                throw new PdfMediaException("Dynamic SVG generation produced unsafe or invalid markup.", exception);
            }
            _imageComponent = new ImageComponent(element, _defaultSpacing);
            _lastSize = size;
        }

        LayoutMeasurement child = _imageComponent.Measure(context);
        return new LayoutMeasurement(
            child.MarginTop,
            child.ContentHeight,
            child.MarginBottom,
            child.UsedWidth,
            new DynamicSvgMetadata(_imageComponent, child),
            child.AvoidBreakInside,
            child.Result,
            child.Remainder);
    }

    public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
    {
        if (measurement.Metadata is not DynamicSvgMetadata metadata)
            throw new InvalidOperationException("Dynamic SVG measurement metadata is missing.");
        metadata.Component.Draw(context, metadata.Measurement);
    }

    private sealed record DynamicSvgMetadata(ImageComponent Component, LayoutMeasurement Measurement);
}
