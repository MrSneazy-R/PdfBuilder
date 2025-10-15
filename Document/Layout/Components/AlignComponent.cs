using System;

namespace PdfBuilder.Document.Layout.Components
{
    public enum LayoutHorizontalAlignment
    {
        Left,
        Center,
        Right,
        Stretch
    }

    public enum LayoutVerticalAlignment
    {
        Top,
        Middle,
        Bottom
    }

    internal sealed class AlignComponent : IMeasurable
    {
        private readonly IMeasurable _child;
        private readonly LayoutHorizontalAlignment _horizontal;
        private readonly LayoutVerticalAlignment _vertical;
        private readonly float? _minHeight;

        public AlignComponent(IMeasurable child, LayoutHorizontalAlignment horizontal, LayoutVerticalAlignment vertical, float? minHeight)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _horizontal = horizontal;
            _vertical = vertical;
            _minHeight = minHeight;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var innerMeasurement = _child.Measure(context);
            if (innerMeasurement.IsWrap)
                return LayoutMeasurement.Wrap(innerMeasurement.UsedWidth);

            float availableWidth = context.Column.Width;
            float reportedWidth = _horizontal == LayoutHorizontalAlignment.Left
                ? innerMeasurement.UsedWidth
                : availableWidth;
            float offsetX = ComputeHorizontalOffset(availableWidth, innerMeasurement.UsedWidth);

            float reservedHeight = Math.Max(innerMeasurement.ReservedHeight, _minHeight ?? innerMeasurement.ReservedHeight);
            float extraHeight = reservedHeight - innerMeasurement.ReservedHeight;
            float marginTop = innerMeasurement.MarginTop;
            float marginBottom = innerMeasurement.MarginBottom;

            if (extraHeight > 0f)
            {
                switch (_vertical)
                {
                    case LayoutVerticalAlignment.Top:
                        marginBottom += extraHeight;
                        break;
                    case LayoutVerticalAlignment.Bottom:
                        marginTop += extraHeight;
                        break;
                    default:
                        float half = extraHeight / 2f;
                        marginTop += half;
                        marginBottom += extraHeight - half;
                        break;
                }
            }

            var metadata = new AlignMetadata(innerMeasurement, offsetX, _horizontal == LayoutHorizontalAlignment.Stretch);
            IMeasurable? remainder = innerMeasurement.Remainder != null
                ? new AlignComponent(innerMeasurement.Remainder, _horizontal, _vertical, _minHeight)
                : null;

            return new LayoutMeasurement(
                marginTop,
                innerMeasurement.ContentHeight,
                marginBottom,
                reportedWidth,
                metadata,
                innerMeasurement.AvoidBreakInside,
                innerMeasurement.Result,
                remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not AlignMetadata metadata)
                throw new InvalidOperationException("Align measurement metadata missing.");

            float contentLeft = context.ContentLeft + metadata.OffsetX;
            float contentWidth = metadata.Stretch ? context.ContentWidth : context.ContentWidth;

            var childContext = new LayoutDrawContext(context.Page, context.Column, contentLeft, context.ContentTop, contentWidth, context.Options);
            _child.Draw(childContext, metadata.InnerMeasurement);
        }

        private float ComputeHorizontalOffset(float availableWidth, float childUsedWidth)
        {
            float remaining = Math.Max(0f, availableWidth - childUsedWidth);
            return _horizontal switch
            {
                LayoutHorizontalAlignment.Left => 0f,
                LayoutHorizontalAlignment.Center => remaining * 0.5f,
                LayoutHorizontalAlignment.Right => remaining,
                LayoutHorizontalAlignment.Stretch => 0f,
                _ => 0f
            };
        }

        private sealed class AlignMetadata
        {
            public AlignMetadata(LayoutMeasurement innerMeasurement, float offsetX, bool stretch)
            {
                InnerMeasurement = innerMeasurement;
                OffsetX = offsetX;
                Stretch = stretch;
            }

            public LayoutMeasurement InnerMeasurement { get; }
            public float OffsetX { get; }
            public bool Stretch { get; }
        }
    }
}

