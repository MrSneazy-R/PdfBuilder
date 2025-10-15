using System;
using System.Collections.Generic;

namespace PdfBuilder.Document.Layout.Components
{
    public sealed class ColumnComponent : IMeasurable
    {
        private readonly List<IMeasurable> _children = new();

        public float Spacing { get; set; } = 8f;

        public ColumnComponent Add(IMeasurable child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            _children.Add(child);
            return this;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (_children.Count == 0)
            {
                return new LayoutMeasurement(0f, 0f, 0f, 0f, new ColumnMetadata(new List<ChildLayout>(), Spacing), avoidBreakInside: false);
            }

            var placements = new List<ChildLayout>(_children.Count);
            float totalHeight = 0f;
            float maxWidth = 0f;
            bool avoidBreakInside = false;
            ColumnComponent? remainder = null;

            float currentTop = context.Column.Y;
            float columnBottom = context.Column.BottomY;

            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];

                if (currentTop <= columnBottom + 0.01f)
                {
                    remainder = BuildRemainderFrom(i);
                    break;
                }

                var slice = new FlowColumn(context.Column.Index, context.Column.X, context.Column.Width, currentTop, columnBottom);
                var childContext = new LayoutMeasureContext(context.Page, slice, context.Options);
                var measurement = child.Measure(childContext);

                if (measurement.IsWrap)
                {
                    if (placements.Count == 0)
                        return LayoutMeasurement.Wrap(measurement.UsedWidth);

                    remainder = BuildRemainderIncludingCurrent(i);
                    break;
                }

                placements.Add(new ChildLayout(child, measurement));
                totalHeight += measurement.ReservedHeight;
                maxWidth = Math.Max(maxWidth, measurement.UsedWidth);
                avoidBreakInside |= measurement.AvoidBreakInside;

                currentTop = Math.Max(columnBottom, currentTop - measurement.ReservedHeight);

                if (measurement.IsPartial)
                {
                    remainder = BuildRemainderForPartial(measurement.Remainder, i);
                    break;
                }

                if (i < _children.Count - 1)
                {
                    totalHeight += Spacing;
                    currentTop = Math.Max(columnBottom, currentTop - Spacing);
                }
            }

            if (placements.Count == 0)
                return LayoutMeasurement.Wrap(context.Column.Width);

            var metadata = new ColumnMetadata(placements, Spacing);
            var resultKind = remainder != null ? LayoutResultKind.Partial : LayoutResultKind.Full;

            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: totalHeight,
                marginBottom: 0f,
                usedWidth: maxWidth,
                metadata: metadata,
                avoidBreakInside: avoidBreakInside,
                result: resultKind,
                remainder: remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not ColumnMetadata metadata)
                throw new InvalidOperationException("Column layout metadata missing.");

            float cursor = context.ContentTop;

            for (int i = 0; i < metadata.Children.Count; i++)
            {
                var child = metadata.Children[i];
                float contentTop = cursor - child.Measurement.MarginTop;
                var childContext = new LayoutDrawContext(context.Page, context.Column, context.ContentLeft, contentTop, context.ContentWidth, context.Options);
                child.Component.Draw(childContext, child.Measurement);

                cursor = contentTop - child.Measurement.ContentHeight - child.Measurement.MarginBottom;
                if (i < metadata.Children.Count - 1)
                    cursor -= metadata.Spacing;
            }
        }

        private sealed record ChildLayout(IMeasurable Component, LayoutMeasurement Measurement);

        private sealed class ColumnMetadata
        {
            public ColumnMetadata(List<ChildLayout> children, float spacing)
            {
                Children = children;
                Spacing = spacing;
            }

            public List<ChildLayout> Children { get; }
            public float Spacing { get; }
        }

        private ColumnComponent? BuildRemainderIncludingCurrent(int startIndex)
        {
            if (startIndex >= _children.Count)
                return null;

            var remainder = new ColumnComponent { Spacing = Spacing };
            for (int r = startIndex; r < _children.Count; r++)
                remainder.Add(_children[r]);
            return remainder;
        }

        private ColumnComponent? BuildRemainderFrom(int startIndex)
        {
            return BuildRemainderIncludingCurrent(startIndex);
        }

        private ColumnComponent? BuildRemainderForPartial(IMeasurable? remainderChild, int currentIndex)
        {
            var remainder = new ColumnComponent { Spacing = Spacing };
            if (remainderChild != null)
                remainder.Add(remainderChild);

            for (int r = currentIndex + 1; r < _children.Count; r++)
                remainder.Add(_children[r]);

            return remainder._children.Count == 0 ? null : remainder;
        }
    }
}
