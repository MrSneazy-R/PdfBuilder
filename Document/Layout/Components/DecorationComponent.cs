using System;
using PdfBuilder.Document;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class DecorationComponent : IMeasurable
    {
        private readonly IMeasurable _child;
        private readonly Action<DecorationDrawContext>? _before;
        private readonly Action<DecorationDrawContext>? _after;

        public DecorationComponent(IMeasurable child, Action<DecorationDrawContext>? before, Action<DecorationDrawContext>? after)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _before = before;
            _after = after;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var inner = _child.Measure(context);
            if (inner.IsWrap)
                return inner;

            return new LayoutMeasurement(
                inner.MarginTop,
                inner.ContentHeight,
                inner.MarginBottom,
                inner.UsedWidth,
                new DecorationMetadata(inner),
                inner.AvoidBreakInside,
                inner.Result,
                inner.Remainder != null ? new DecorationComponent(inner.Remainder, _before, _after) : null);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not DecorationMetadata metadata)
                throw new InvalidOperationException("Decoration measurement metadata missing.");

            var rect = ComputeRect(context, measurement);
            var drawContext = new DecorationDrawContext(context.Page, rect, context.Options);

            _before?.Invoke(drawContext);

            _child.Draw(context, metadata.InnerMeasurement);

            _after?.Invoke(drawContext);
        }

        private static FlowRect ComputeRect(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            float top = context.ContentTop + measurement.MarginTop;
            float height = measurement.ReservedHeight;
            return new FlowRect(context.ContentLeft, top, context.ContentWidth, height);
        }

        private sealed class DecorationMetadata
        {
            public DecorationMetadata(LayoutMeasurement innerMeasurement)
            {
                InnerMeasurement = innerMeasurement;
            }

            public LayoutMeasurement InnerMeasurement { get; }
        }
    }
}

