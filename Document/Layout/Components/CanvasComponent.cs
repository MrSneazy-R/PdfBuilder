using System;
using PdfBuilder.Document;
using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class CanvasComponent : IMeasurable
    {
        private readonly CanvasElement _element;
        private readonly float _defaultSpacing;
        private readonly Action<CanvasBuilder, CanvasSize>? _dynamicDraw;
        private readonly bool _useAvailableWidth;
        private CanvasSize? _lastDrawSize;

        public CanvasComponent(
            CanvasElement element,
            float defaultSpacing,
            Action<CanvasBuilder, CanvasSize>? dynamicDraw = null,
            bool useAvailableWidth = false)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _defaultSpacing = defaultSpacing;
            _dynamicDraw = dynamicDraw;
            _useAvailableWidth = useAvailableWidth;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            float marginTop = _element.MarginTop ?? _defaultSpacing;
            float marginBottom = _element.MarginBottom ?? 0f;
            float marginLeft = _element.MarginLeft ?? 0f;
            float marginRight = _element.MarginRight ?? 0f;

            float contentWidth = _useAvailableWidth
                ? Math.Max(0f, context.AvailableWidth - marginLeft - marginRight)
                : _element.Width;
            float usedWidth = marginLeft + contentWidth + marginRight;
            float contentHeight = _element.Height;

            float availableHeight = context.AvailableHeight - marginTop - marginBottom;
            if (availableHeight + 0.1f < contentHeight)
            {
                return LayoutMeasurement.Wrap(usedWidth);
            }

            _element.Width = contentWidth;
            var size = new CanvasSize(contentWidth, contentHeight);
            if (_dynamicDraw != null && (!_lastDrawSize.HasValue || _lastDrawSize.Value != size))
            {
                _element.ClearCommands();
                var builder = new CanvasBuilder(_element, context.Page.Owner?.RenderLimits);
                _dynamicDraw(builder, size);
                builder.Complete();
                _lastDrawSize = size;
            }

            context.Page.Owner?.RenderLimits.ValidateCanvasCommands(_element.CommandCount, _element.CommandBytes);
            if (_element.MaximumEffectStepsUsed > 0)
                context.Page.Owner?.RenderLimits.ValidateCanvasEffectSteps(_element.MaximumEffectStepsUsed);

            var metadata = new CanvasMetadata(marginLeft);
            return new LayoutMeasurement(
                marginTop,
                contentHeight,
                marginBottom,
                usedWidth,
                metadata,
                _element.AvoidBreakInside);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not CanvasMetadata meta)
                throw new InvalidOperationException("Canvas measurement metadata missing.");

            _element.X = context.ContentLeft + meta.MarginLeft;
            _element.Y = context.ContentTop - _element.Height;
            context.Page.AddElement(_element);
        }

        private sealed class CanvasMetadata
        {
            public CanvasMetadata(float marginLeft)
            {
                MarginLeft = marginLeft;
            }

            public float MarginLeft { get; }
        }
    }
}
