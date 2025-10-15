using System;
using System.Collections.Generic;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class FlexRowComponent : IMeasurable
    {
        private readonly List<Entry> _entries = new();
        public float Gap { get; set; } = 12f;

        public void Add(float weight, IMeasurable child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            float normalized = weight <= 0f ? 1f : weight;
            _entries.Add(new Entry(child, normalized));
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (_entries.Count == 0)
                return new LayoutMeasurement(0f, 0f, 0f, 0f, new FlexRowMetadata(Array.Empty<RowChild>(), Gap), false);

            float availableWidth = Math.Max(0f, context.Column.Width - Gap * Math.Max(0, _entries.Count - 1));
            float totalWeight = 0f;
            foreach (var entry in _entries)
                totalWeight += entry.Weight;

            var placements = new List<RowChild>(_entries.Count);
            float maxHeight = 0f;
            float usedWidth = 0f;
            bool avoidBreakInside = false;
            FlexRowComponent? remainder = null;

            float cursorX = context.Column.X;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                float share = totalWeight > 0f ? entry.Weight / totalWeight : 1f / _entries.Count;
                float width = availableWidth * share;

                var slice = new FlowColumn(i, cursorX, width, context.Column.TopY, context.Column.BottomY);
                var childContext = new LayoutMeasureContext(context.Page, slice, context.Options);
                var measurement = entry.Component.Measure(childContext);

                if (measurement.IsWrap)
                {
                    if (placements.Count == 0)
                        return LayoutMeasurement.Wrap(context.Column.Width);

                    remainder = BuildRemainder(i);
                    break;
                }

                placements.Add(new RowChild(entry.Component, entry.Weight, measurement, slice));
                maxHeight = Math.Max(maxHeight, measurement.ReservedHeight);
                usedWidth += measurement.UsedWidth;
                avoidBreakInside |= measurement.AvoidBreakInside;

                cursorX += width + Gap;

                if (measurement.IsPartial)
                {
                    remainder = BuildPartialRemainder(measurement.Remainder, entry.Weight, i);
                    break;
                }
            }

            if (placements.Count == 0)
                return LayoutMeasurement.Wrap(context.Column.Width);

            float contentHeight = maxHeight;
            var metadata = new FlexRowMetadata(placements, Gap);
            var result = remainder != null ? LayoutResultKind.Partial : LayoutResultKind.Full;

            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: contentHeight,
                marginBottom: 0f,
                usedWidth: Math.Max(context.Column.Width, usedWidth),
                metadata: metadata,
                avoidBreakInside: avoidBreakInside,
                result: result,
                remainder: remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not FlexRowMetadata metadata)
                throw new InvalidOperationException("Flex row metadata missing.");

            foreach (var child in metadata.Children)
            {
                float contentTop = context.ContentTop - child.Measurement.MarginTop;
                var drawContext = new LayoutDrawContext(
                    context.Page,
                    child.Column,
                    child.Column.X,
                    contentTop,
                    child.Column.Width,
                    context.Options);

                child.Component.Draw(drawContext, child.Measurement);
            }
        }

        private FlexRowComponent? BuildRemainder(int startIndex)
        {
            if (startIndex >= _entries.Count)
                return null;

            var comp = new FlexRowComponent { Gap = Gap };
            for (int r = startIndex; r < _entries.Count; r++)
                comp.Add(_entries[r].Weight, _entries[r].Component);
            return comp._entries.Count == 0 ? null : comp;
        }

        private FlexRowComponent? BuildPartialRemainder(IMeasurable? remainderChild, float weight, int index)
        {
            var comp = new FlexRowComponent { Gap = Gap };
            if (remainderChild != null)
                comp.Add(weight, remainderChild);

            for (int r = index + 1; r < _entries.Count; r++)
                comp.Add(_entries[r].Weight, _entries[r].Component);

            return comp._entries.Count == 0 ? null : comp;
        }

        private sealed record Entry(IMeasurable Component, float Weight);

        private sealed record RowChild(IMeasurable Component, float Weight, LayoutMeasurement Measurement, FlowColumn Column);

        private sealed class FlexRowMetadata
        {
            public FlexRowMetadata(IReadOnlyList<RowChild> children, float gap)
            {
                Children = children;
                Gap = gap;
            }

            public IReadOnlyList<RowChild> Children { get; }
            public float Gap { get; }
        }
    }
}

