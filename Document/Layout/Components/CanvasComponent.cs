using System;
using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class CanvasComponent : IMeasurable
    {
        private readonly CanvasElement _element;
        private readonly float _defaultSpacing;

        public CanvasComponent(CanvasElement element, float defaultSpacing)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _defaultSpacing = defaultSpacing;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            float marginTop = _element.MarginTop ?? _defaultSpacing;
            float marginBottom = _element.MarginBottom ?? 0f;
            float marginLeft = _element.MarginLeft ?? 0f;
            float marginRight = _element.MarginRight ?? 0f;

            float usedWidth = marginLeft + _element.Width + marginRight;
            float contentHeight = _element.Height;

            float availableHeight = context.AvailableHeight - marginTop - marginBottom;
            if (availableHeight + 0.1f < contentHeight)
            {
                return LayoutMeasurement.Wrap(usedWidth);
            }

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
