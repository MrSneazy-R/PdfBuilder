using System;
using System.Collections.Generic;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout.Components
{
    public sealed class RowComponent : IMeasurable
    {
        private readonly List<RowEntry> _entries = new();

        public float Gap { get; set; } = 12f;

        internal RowComponent Add(IMeasurable child, RowWidthSpec spec)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            _entries.Add(new RowEntry(child, spec));
            return this;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (_entries.Count == 0)
            {
                return new LayoutMeasurement(0f, 0f, 0f, 0f, new RowMetadata(new List<RowChild>(), Gap), avoidBreakInside: false);
            }

            int count = _entries.Count;
            var placements = new List<RowChild>(count);

            float totalGap = Gap * Math.Max(0, count - 1);
            float availableWidth = Math.Max(0f, context.Column.Width - totalGap);
            var assignedWidths = AllocateWidths(availableWidth);

            float cursorX = context.Column.X;
            float maxHeight = 0f;
            float usedWidth = 0f;
            bool avoidBreakInside = false;
            RowComponent? remainder = null;

            for (int i = 0; i < count; i++)
            {
                float width = assignedWidths[i];
                var entry = _entries[i];
                var slice = new FlowColumn(i, cursorX, width, context.Column.TopY, context.Column.BottomY);
                var childContext = new LayoutMeasureContext(context.Page, slice, context.Options);
                var measurement = entry.Component.Measure(childContext);

                if (measurement.IsWrap)
                {
                    if (placements.Count == 0)
                        return LayoutMeasurement.Wrap(context.Column.Width);

                    remainder = BuildRemainderIncludingCurrent(i);
                    break;
                }

                placements.Add(new RowChild(entry.Component, measurement, slice));
                maxHeight = Math.Max(maxHeight, measurement.ReservedHeight);
                usedWidth += width;
                avoidBreakInside |= measurement.AvoidBreakInside;

                if (measurement.IsPartial)
                {
                    remainder = BuildPartialRemainder(measurement.Remainder, i);
                    break;
                }

                cursorX += width + Gap;
            }

            if (placements.Count == 0)
                return LayoutMeasurement.Wrap(context.Column.Width);

            var metadata = new RowMetadata(placements, Gap);
            var resultKind = remainder != null ? LayoutResultKind.Partial : LayoutResultKind.Full;
            float totalUsedWidth = Math.Min(context.Column.Width, usedWidth + Gap * Math.Max(0, placements.Count - 1));

            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: maxHeight,
                marginBottom: 0f,
                usedWidth: totalUsedWidth,
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

        private float[] AllocateWidths(float availableWidth)
        {
            int count = _entries.Count;
            var widths = new float[count];
            float remaining = availableWidth;

            for (int i = 0; i < count; i++)
            {
                if (_entries[i].Spec.Kind == RowWidthKind.Fixed)
                {
                    float width = Math.Max(0f, _entries[i].Spec.Value);
                    widths[i] = width;
                    remaining -= width;
                }
            }

            remaining = Math.Max(0f, remaining);

            var weights = new float[count];
            float totalWeight = 0f;

            for (int i = 0; i < count; i++)
            {
                switch (_entries[i].Spec.Kind)
                {
                    case RowWidthKind.Auto:
                        weights[i] = 1f;
                        totalWeight += 1f;
                        break;
                    case RowWidthKind.Even:
                        weights[i] = 1f;
                        totalWeight += 1f;
                        break;
                    case RowWidthKind.Relative:
                        float weight = Math.Max(0f, _entries[i].Spec.Value);
                        if (weight <= 0f)
                            weight = 1f;
                        weights[i] = weight;
                        totalWeight += weight;
                        break;
                }
            }

            if (totalWeight > 0f)
            {
                float assigned = 0f;
                int lastIndex = -1;

                for (int i = 0; i < count; i++)
                {
                    if (weights[i] <= 0f)
                        continue;

                    float width = remaining * (weights[i] / totalWeight);
                    width = Math.Max(0f, width);
                    widths[i] += width;
                    assigned += width;
                    lastIndex = i;
                }

                float leftover = Math.Max(0f, remaining - assigned);
                if (leftover > 0f && lastIndex >= 0)
                    widths[lastIndex] += leftover;
            }
            else if (remaining > 0f && count > 0)
            {
                widths[count - 1] += remaining;
            }

            return widths;
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
            if (startIndex >= _entries.Count)
                return null;

            var remainder = new RowComponent { Gap = Gap };
            for (int r = startIndex; r < _entries.Count; r++)
                remainder.Add(_entries[r].Component, _entries[r].Spec);
            return remainder;
        }

        private RowComponent? BuildPartialRemainder(IMeasurable? remainderChild, int currentIndex)
        {
            var remainder = new RowComponent { Gap = Gap };
            if (remainderChild != null)
                remainder.Add(remainderChild, _entries[currentIndex].Spec);

            for (int r = currentIndex + 1; r < _entries.Count; r++)
                remainder.Add(_entries[r].Component, _entries[r].Spec);

            return remainder._entries.Count == 0 ? null : remainder;
        }

        internal readonly record struct RowWidthSpec(RowWidthKind Kind, float Value)
        {
            public static RowWidthSpec Even() => new RowWidthSpec(RowWidthKind.Even, 1f);
            public static RowWidthSpec Fixed(float width) => new RowWidthSpec(RowWidthKind.Fixed, width);
            public static RowWidthSpec Relative(float weight) => new RowWidthSpec(RowWidthKind.Relative, weight);
            public static RowWidthSpec Auto() => new RowWidthSpec(RowWidthKind.Auto, 0f);
        }

        private readonly record struct RowEntry(IMeasurable Component, RowWidthSpec Spec);

        internal enum RowWidthKind
        {
            Even,
            Fixed,
            Relative,
            Auto
        }
    }
}
