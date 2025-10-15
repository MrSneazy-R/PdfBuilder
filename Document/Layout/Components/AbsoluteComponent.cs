using System;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class AbsoluteComponent : IMeasurable
    {
        private readonly IMeasurable _child;
        private readonly float _offsetX;
        private readonly float _offsetY;

        public AbsoluteComponent(IMeasurable child, float offsetX, float offsetY)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var measurement = _child.Measure(context);
            if (measurement.IsWrap)
                return measurement;

            var metadata = new AbsoluteMetadata(_child, measurement, _offsetX, _offsetY);
            var remainder = measurement.Remainder != null
                ? new AbsoluteComponent(measurement.Remainder, _offsetX, _offsetY)
                : null;

            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: 0f,
                marginBottom: 0f,
                usedWidth: measurement.UsedWidth,
                metadata: metadata,
                avoidBreakInside: false,
                result: measurement.Result,
                remainder: remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not AbsoluteMetadata metadata)
                throw new InvalidOperationException("Absolute measurement metadata missing.");

            float contentLeft = context.ContentLeft + metadata.OffsetX;
            float contentTop = context.ContentTop - metadata.OffsetY;

            var childContext = new LayoutDrawContext(
                context.Page,
                context.Column,
                contentLeft,
                contentTop,
                context.ContentWidth,
                context.Options);

            metadata.Component.Draw(childContext, metadata.InnerMeasurement);
        }

        private sealed class AbsoluteMetadata
        {
            public AbsoluteMetadata(IMeasurable component, LayoutMeasurement innerMeasurement, float offsetX, float offsetY)
            {
                Component = component;
                InnerMeasurement = innerMeasurement;
                OffsetX = offsetX;
                OffsetY = offsetY;
            }

            public IMeasurable Component { get; }
            public LayoutMeasurement InnerMeasurement { get; }
            public float OffsetX { get; }
            public float OffsetY { get; }
        }
    }
}

