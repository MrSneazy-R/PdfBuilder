using System;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class EmptyComponent : IMeasurable
    {
        public LayoutMeasurement Measure(LayoutMeasureContext context) => new(0f, 0f, 0f, 0f);
        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) { }
    }

    internal sealed class PageBreakComponent : IMeasurable
    {
        public LayoutMeasurement Measure(LayoutMeasureContext context) => throw new InvalidOperationException("Page breaks are handled by ColumnBuilder.");
        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) => throw new InvalidOperationException("Page breaks are handled by ColumnBuilder.");
    }

    internal sealed class EnsureSpaceComponent : IMeasurable
    {
        private readonly float _minimumHeight;
        private readonly IMeasurable _child;

        public EnsureSpaceComponent(IMeasurable child, float minimumHeight)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _minimumHeight = minimumHeight;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (context.AvailableHeight < _minimumHeight)
                return LayoutMeasurement.Wrap(context.AvailableWidth);
            return _child.Measure(context);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) => _child.Draw(context, measurement);
    }

    internal sealed class KeepTogetherComponent : IMeasurable
    {
        private readonly IMeasurable _child;
        public KeepTogetherComponent(IMeasurable child) => _child = child ?? throw new ArgumentNullException(nameof(child));
        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var measurement = _child.Measure(context);
            if (measurement.IsWrap) return measurement;
            return new LayoutMeasurement(measurement.MarginTop, measurement.ContentHeight, measurement.MarginBottom, measurement.UsedWidth, measurement.Metadata, true, measurement.Result, measurement.Remainder);
        }
        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) => _child.Draw(context, measurement);
    }
}
