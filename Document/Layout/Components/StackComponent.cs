using System;
using System.Collections.Generic;

namespace PdfBuilder.Document.Layout.Components
{
    public sealed class StackComponent : IMeasurable
    {
        private readonly List<IMeasurable> _children = new();

        public StackComponent Add(IMeasurable child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            _children.Add(child);
            return this;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (_children.Count == 0)
            {
                return new LayoutMeasurement(0f, 0f, 0f, 0f, metadata: new StackMetadata(new List<StackChild>()), avoidBreakInside: false);
            }

            var placements = new List<StackChild>(_children.Count);
            float maxHeight = 0f;
            float maxWidth = 0f;
            bool avoidBreakInside = false;
            StackComponent? remainder = null;

            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                var measurement = child.Measure(context);
                if (measurement.IsWrap)
                    return LayoutMeasurement.Wrap(measurement.UsedWidth);

                placements.Add(new StackChild(child, measurement));
                maxHeight = Math.Max(maxHeight, measurement.ReservedHeight);
                maxWidth = Math.Max(maxWidth, measurement.UsedWidth);
                avoidBreakInside |= measurement.AvoidBreakInside;

                if (measurement.IsPartial)
                {
                    var remainderStack = new StackComponent();
                    if (measurement.Remainder != null)
                        remainderStack.Add(measurement.Remainder);

                    for (int r = i + 1; r < _children.Count; r++)
                        remainderStack.Add(_children[r]);

                    remainder = remainderStack._children.Count == 0 ? null : remainderStack;
                    break;
                }
            }

            var metadata = new StackMetadata(placements);
            var resultKind = remainder != null ? LayoutResultKind.Partial : LayoutResultKind.Full;

            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: maxHeight,
                marginBottom: 0f,
                usedWidth: maxWidth,
                metadata: metadata,
                avoidBreakInside: avoidBreakInside,
                result: resultKind,
                remainder: remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not StackMetadata metadata)
                throw new InvalidOperationException("Stack layout metadata missing.");

            float baseTop = context.ContentTop;

            foreach (var child in metadata.Children)
            {
                float contentTop = baseTop - child.Measurement.MarginTop;
                var childContext = new LayoutDrawContext(context.Page, context.Column, context.ContentLeft, contentTop, context.ContentWidth, context.Options);
                child.Component.Draw(childContext, child.Measurement);
            }
        }

        private sealed record StackChild(IMeasurable Component, LayoutMeasurement Measurement);

        private sealed class StackMetadata
        {
            public StackMetadata(List<StackChild> children)
            {
                Children = children;
            }

            public List<StackChild> Children { get; }
        }
    }
}

