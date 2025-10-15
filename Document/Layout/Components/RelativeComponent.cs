using System;
using System.Collections.Generic;
using PdfBuilder.Document;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class RelativeComponent : IMeasurable
    {
        private readonly List<Entry> _entries;

        public float Spacing { get; set; }

        public RelativeComponent()
        {
            _entries = new List<Entry>();
        }

        private RelativeComponent(List<Entry> entries, float spacing)
        {
            _entries = entries;
            Spacing = spacing;
        }

        public void Add(float weight, IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            float normalizedWeight = weight <= 0f ? 1f : weight;
            _entries.Add(new Entry(component, normalizedWeight));
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (_entries.Count == 0)
                return new LayoutMeasurement(0f, 0f, 0f, 0f, new RelativeMetadata(Array.Empty<Placement>(), Spacing), avoidBreakInside: false);

            float availableHeight = context.AvailableHeight;
            if (availableHeight <= 0f)
                return LayoutMeasurement.Wrap(context.Column.Width);

            float totalWeight = 0f;
            foreach (var entry in _entries)
                totalWeight += entry.Weight;

            float totalSpacing = Spacing * Math.Max(0, _entries.Count - 1);
            float distributableHeight = Math.Max(0f, availableHeight - totalSpacing);

            float cursorTop = context.Column.Y;
            var placements = new List<Placement>(_entries.Count);

            float consumedHeight = 0f;
            float spacingApplied = 0f;
            float usedWidth = 0f;
            bool avoidBreak = false;
            RelativeComponent? remainder = null;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                float share = totalWeight > 0f ? entry.Weight / totalWeight : 1f / _entries.Count;
                float targetHeight = distributableHeight * share;
                float top = cursorTop;
                float bottom = Math.Max(context.Column.BottomY, top - targetHeight);
                var slice = new FlowColumn(context.Column.Index, context.Column.X, context.Column.Width, top, bottom);
                var childContext = new LayoutMeasureContext(context.Page, slice, context.Options);
                var measurement = entry.Component.Measure(childContext);

                if (measurement.IsWrap)
                {
                    if (placements.Count == 0)
                        return LayoutMeasurement.Wrap(measurement.UsedWidth);

                    remainder = BuildRemainder(entry.Weight, measurement.Remainder, i);
                    break;
                }

                placements.Add(new Placement(entry.Component, entry.Weight, measurement, slice));
                consumedHeight += measurement.ReservedHeight;
                usedWidth = Math.Max(usedWidth, measurement.UsedWidth);
                avoidBreak |= measurement.AvoidBreakInside;

                cursorTop = Math.Max(context.Column.BottomY, cursorTop - measurement.ReservedHeight);

                if (measurement.IsPartial)
                {
                    remainder = BuildRemainder(entry.Weight, measurement.Remainder, i);
                    break;
                }

                if (i < _entries.Count - 1)
                {
                    cursorTop = Math.Max(context.Column.BottomY, cursorTop - Spacing);
                    spacingApplied += Spacing;
                }
            }

            if (placements.Count == 0)
                return LayoutMeasurement.Wrap(context.Column.Width);

            float contentHeight = consumedHeight + spacingApplied;
            float reservedHeight = Math.Max(contentHeight, availableHeight);
            usedWidth = Math.Max(usedWidth, context.Column.Width);

            var metadata = new RelativeMetadata(placements, Spacing);
            var result = remainder != null ? LayoutResultKind.Partial : LayoutResultKind.Full;

            return new LayoutMeasurement(0f, reservedHeight, 0f, usedWidth, metadata, avoidBreak, result, remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not RelativeMetadata metadata)
                throw new InvalidOperationException("Relative measurement metadata missing.");

            float cursor = context.ContentTop;
            var children = metadata.Children;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                float contentTop = cursor - child.Measurement.MarginTop;
                var childContext = new LayoutDrawContext(context.Page, child.Column, child.Column.X, contentTop, child.Column.Width, context.Options);
                child.Component.Draw(childContext, child.Measurement);

                cursor = contentTop - child.Measurement.ContentHeight - child.Measurement.MarginBottom;
                if (i < children.Count - 1)
                    cursor -= metadata.Spacing;
            }
        }

        private RelativeComponent? BuildRemainder(float weight, IMeasurable? remainderChild, int index)
        {
            var entries = new List<Entry>();
            if (remainderChild != null)
                entries.Add(new Entry(remainderChild, weight));

            for (int i = index + 1; i < _entries.Count; i++)
                entries.Add(_entries[i]);

            return entries.Count == 0 ? null : new RelativeComponent(entries, Spacing);
        }

        private sealed record Entry(IMeasurable Component, float Weight);

        private sealed record Placement(IMeasurable Component, float Weight, LayoutMeasurement Measurement, FlowColumn Column);

        private sealed class RelativeMetadata
        {
            public RelativeMetadata(IReadOnlyList<Placement> children, float spacing)
            {
                Children = children;
                Spacing = spacing;
            }

            public IReadOnlyList<Placement> Children { get; }

            public float Spacing { get; }
        }
    }
}
