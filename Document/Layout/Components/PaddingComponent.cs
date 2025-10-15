using System;
using PdfBuilder.Document;

namespace PdfBuilder.Document.Layout.Components
{
    internal readonly struct PaddingValues
    {
        public PaddingValues(float uniform)
            : this(uniform, uniform, uniform, uniform)
        {
        }

        public PaddingValues(float left, float top, float right, float bottom)
        {
            Left = Math.Max(0f, left);
            Top = Math.Max(0f, top);
            Right = Math.Max(0f, right);
            Bottom = Math.Max(0f, bottom);
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }

        public float Horizontal => Left + Right;
        public float Vertical => Top + Bottom;
    }

    internal sealed class PaddingComponent : IMeasurable
    {
        private readonly IMeasurable _child;
        private readonly PaddingValues _padding;

        public PaddingComponent(IMeasurable child, PaddingValues padding)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _padding = padding;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var innerColumn = CreateInnerColumn(context);
            if (innerColumn == null)
                return LayoutMeasurement.Wrap(context.Column.Width);

            var childContext = new LayoutMeasureContext(context.Page, innerColumn, context.Options);
            var innerMeasurement = _child.Measure(childContext);
            if (innerMeasurement.IsWrap)
                return LayoutMeasurement.Wrap(innerMeasurement.UsedWidth + _padding.Horizontal);

            var metadata = new PaddingMetadata(innerMeasurement);

            float marginTop = _padding.Top + innerMeasurement.MarginTop;
            float marginBottom = _padding.Bottom + innerMeasurement.MarginBottom;
            float contentHeight = innerMeasurement.ContentHeight;
            float usedWidth = innerMeasurement.UsedWidth + _padding.Horizontal;
            bool avoidBreakInside = innerMeasurement.AvoidBreakInside;

            IMeasurable? remainder = innerMeasurement.Remainder != null
                ? new PaddingComponent(innerMeasurement.Remainder, _padding)
                : null;

            return new LayoutMeasurement(
                marginTop,
                contentHeight,
                marginBottom,
                usedWidth,
                metadata,
                avoidBreakInside,
                innerMeasurement.Result,
                remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not PaddingMetadata metadata)
                throw new InvalidOperationException("Padding measurement metadata missing.");

            var innerMeasurement = metadata.InnerMeasurement;
            float childLeft = context.ContentLeft + _padding.Left;
            float childContentTop = context.ContentTop + _padding.Top;
            float childWidth = Math.Max(0f, context.ContentWidth - _padding.Horizontal);

            var childContext = new LayoutDrawContext(context.Page, context.Column, childLeft, childContentTop, childWidth, context.Options);
            _child.Draw(childContext, innerMeasurement);
        }

        private FlowColumn? CreateInnerColumn(LayoutMeasureContext context)
        {
            float top = context.Column.Y - _padding.Top;
            float bottom = context.Column.BottomY + _padding.Bottom;
            float width = Math.Max(0f, context.Column.Width - _padding.Horizontal);

            if (width <= 0f || top <= bottom + 0.05f)
                return null;

            return new FlowColumn(context.Column.Index, context.Column.X + _padding.Left, width, top, bottom);
        }

        private sealed class PaddingMetadata
        {
            public PaddingMetadata(LayoutMeasurement innerMeasurement)
            {
                InnerMeasurement = innerMeasurement;
            }

            public LayoutMeasurement InnerMeasurement { get; }
        }
    }
}
