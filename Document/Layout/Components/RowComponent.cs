using PdfBuilder.Models;
using System;
using System.Collections.Generic;

namespace PdfBuilder.Document.Layout.Components
{
    public sealed class RowComponent : IMeasurable
    {
        private readonly List<IMeasurable> _children = new();

        public float Gap { get; set; } = 12f;

        public RowComponent Add(IMeasurable child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            _children.Add(child);
            return this;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (_children.Count == 0)
            {
                return new LayoutMeasurement(0f, 0f, 0f, 0f, new RowMetadata(new List<RowChild>(), Gap), avoidBreakInside: false);
            }

            var placements = new List<RowChild>(_children.Count);
            float availableWidth = Math.Max(0f, context.Column.Width - Gap * (_children.Count - 1));
            float cellWidth = _children.Count == 0 ? 0f : availableWidth / _children.Count;

            float cursorX = context.Column.X;
            float maxHeight = 0f;
            float usedWidth = 0f;
            bool avoidBreakInside = false;
            RowComponent? remainder = null;

            for (int i = 0; i < _children.Count; i++)
            {
                var slice = new FlowColumn(i, cursorX, cellWidth, context.Column.TopY, context.Column.BottomY);
                var childContext = new LayoutMeasureContext(context.Page, slice, context.Options);
                var measurement = _children[i].Measure(childContext);

                if (measurement.IsWrap)
                {
                    if (placements.Count == 0)
                        return LayoutMeasurement.Wrap(context.Column.Width);

                    remainder = BuildRemainderIncludingCurrent(i);
                    break;
                }

                placements.Add(new RowChild(_children[i], measurement, slice));
                maxHeight = Math.Max(maxHeight, measurement.ReservedHeight);
                usedWidth += measurement.UsedWidth;
                avoidBreakInside |= measurement.AvoidBreakInside;

                if (measurement.IsPartial)
                {
                    remainder = BuildPartialRemainder(measurement.Remainder, i);
                    break;
                }

                cursorX += cellWidth + Gap;
            }

            if (placements.Count == 0)
                return LayoutMeasurement.Wrap(context.Column.Width);

            var metadata = new RowMetadata(placements, Gap);
            var resultKind = remainder != null ? LayoutResultKind.Partial : LayoutResultKind.Full;

            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: maxHeight,
                marginBottom: 0f,
                usedWidth: usedWidth,
                metadata: metadata,
                avoidBreakInside: avoidBreakInside,
                result: resultKind,
                remainder: remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not RowMetadata metadata)
                throw new InvalidOperationException("Row layout metadata missing.");

            foreach (var child in metadata.Children)
            {
                float contentTop = context.ContentTop - child.Measurement.MarginTop;
                var childContext = new LayoutDrawContext(
                    context.Page,
                    child.Column,
                    child.Column.X,
                    contentTop,
                    child.Column.Width,
                    context.Options);
                child.Component.Draw(childContext, child.Measurement);
            }
        }

        private sealed record RowChild(IMeasurable Component, LayoutMeasurement Measurement, FlowColumn Column);

        private sealed class RowMetadata
        {
            public RowMetadata(List<RowChild> children, float gap)
            {
                Children = children;
                Gap = gap;
            }

            public List<RowChild> Children { get; }
            public float Gap { get; }
        }

        private RowComponent? BuildRemainderIncludingCurrent(int startIndex)
        {
            if (startIndex >= _children.Count)
                return null;

            var remainder = new RowComponent { Gap = Gap };
            for (int r = startIndex; r < _children.Count; r++)
                remainder.Add(_children[r]);
            return remainder;
        }

        private RowComponent? BuildPartialRemainder(IMeasurable? remainderChild, int currentIndex)
        {
            var remainder = new RowComponent { Gap = Gap };
            if (remainderChild != null)
                remainder.Add(remainderChild);

            for (int r = currentIndex + 1; r < _children.Count; r++)
                remainder.Add(_children[r]);

            return remainder._children.Count == 0 ? null : remainder;
        }
    }
}
