using System.Collections.Generic;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class LayerComponent : IMeasurable
    {
        private readonly List<IMeasurable> _background = new();
        private readonly List<IMeasurable> _content = new();
        private readonly List<IMeasurable> _foreground = new();

        public void AddBackground(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _background.Add(component);
        }

        public void AddContent(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _content.Add(component);
        }

        public void AddForeground(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _foreground.Add(component);
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var backgrounds = MeasureGroup(_background, context);
            var contents = MeasureGroup(_content, context);
            var foregrounds = MeasureGroup(_foreground, context);

            if (ContainsWrap(backgrounds) || ContainsWrap(contents) || ContainsWrap(foregrounds))
                return LayoutMeasurement.Wrap(context.Column.Width);

            float maxHeight = Math.Max(Math.Max(MaxReservedHeight(backgrounds), MaxReservedHeight(contents)), MaxReservedHeight(foregrounds));
            float usedWidth = Math.Max(Math.Max(MaxUsedWidth(backgrounds), MaxUsedWidth(contents)), MaxUsedWidth(foregrounds));
            usedWidth = Math.Max(usedWidth, context.Column.Width);

            bool avoidBreakInside = AnyAvoidBreak(backgrounds) || AnyAvoidBreak(contents) || AnyAvoidBreak(foregrounds);

            var metadata = new LayerMetadata(backgrounds, contents, foregrounds);
            return new LayoutMeasurement(0f, maxHeight, 0f, usedWidth, metadata, avoidBreakInside);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not LayerMetadata metadata)
                throw new InvalidOperationException("Layer measurement metadata missing.");

            DrawGroup(metadata.Background, context);
            DrawGroup(metadata.Content, context);
            DrawGroup(metadata.Foreground, context);
        }

        private static List<LayerChild> MeasureGroup(IReadOnlyList<IMeasurable> group, LayoutMeasureContext context)
        {
            var list = new List<LayerChild>(group.Count);
            foreach (var component in group)
            {
                var measurement = component.Measure(context);
                list.Add(new LayerChild(component, measurement));
            }
            return list;
        }

        private static bool ContainsWrap(IEnumerable<LayerChild> group)
        {
            foreach (var child in group)
            {
                if (child.Measurement.IsWrap || child.Measurement.IsPartial)
                    return true;
            }
            return false;
        }

        private static float MaxReservedHeight(IEnumerable<LayerChild> group)
        {
            float max = 0f;
            foreach (var child in group)
            {
                max = Math.Max(max, child.Measurement.ReservedHeight);
            }
            return max;
        }

        private static float MaxUsedWidth(IEnumerable<LayerChild> group)
        {
            float max = 0f;
            foreach (var child in group)
            {
                max = Math.Max(max, child.Measurement.UsedWidth);
            }
            return max;
        }

        private static bool AnyAvoidBreak(IEnumerable<LayerChild> group)
        {
            foreach (var child in group)
            {
                if (child.Measurement.AvoidBreakInside)
                    return true;
            }
            return false;
        }

        private static void DrawGroup(IEnumerable<LayerChild> group, LayoutDrawContext context)
        {
            foreach (var child in group)
            {
                float contentTop = context.ContentTop - child.Measurement.MarginTop;
                var childContext = new LayoutDrawContext(context.Page, context.Column, context.ContentLeft, contentTop, context.ContentWidth, context.Options);
                child.Component.Draw(childContext, child.Measurement);
            }
        }

        private sealed record LayerChild(IMeasurable Component, LayoutMeasurement Measurement);

        private sealed class LayerMetadata
        {
            public LayerMetadata(List<LayerChild> background, List<LayerChild> content, List<LayerChild> foreground)
            {
                Background = background;
                Content = content;
                Foreground = foreground;
            }

            public List<LayerChild> Background { get; }
            public List<LayerChild> Content { get; }
            public List<LayerChild> Foreground { get; }
        }
    }
}
