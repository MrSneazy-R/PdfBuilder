using System;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class SizedComponent : IMeasurable
    {
        private readonly IMeasurable _child;
        private readonly float? _minWidth;
        private readonly float? _maxWidth;
        private readonly float? _width;
        private readonly float? _minHeight;
        private readonly float? _maxHeight;
        private readonly float? _height;
        private readonly float? _aspectRatio;
        private readonly bool _fillWidth;
        private readonly bool _fillHeight;
        private readonly bool _shrinkWidth;
        private readonly bool _shrinkHeight;

        public SizedComponent(
            IMeasurable child,
            float? minWidth,
            float? maxWidth,
            float? width,
            float? minHeight,
            float? maxHeight,
            float? height,
            float? aspectRatio,
            bool fillWidth,
            bool fillHeight,
            bool shrinkWidth,
            bool shrinkHeight)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _minWidth = Normalize(minWidth);
            _maxWidth = Normalize(maxWidth);
            _width = Normalize(width);
            _minHeight = Normalize(minHeight);
            _maxHeight = Normalize(maxHeight);
            _height = Normalize(height);
            _aspectRatio = aspectRatio.HasValue && aspectRatio.Value > 0f ? aspectRatio : null;
            _fillWidth = fillWidth;
            _fillHeight = fillHeight;
            _shrinkWidth = shrinkWidth;
            _shrinkHeight = shrinkHeight;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var column = context.Column;
            float desiredWidth = ResolveWidth(column.Width);
            var innerColumn = new FlowColumn(column.Index, column.X, desiredWidth, column.TopY, column.BottomY);
            var childContext = new LayoutMeasureContext(context.Page, innerColumn, context.Options);
            var childMeasurement = _child.Measure(childContext);

            if (childMeasurement.IsWrap)
                return childMeasurement;

            IMeasurable? remainder = null;
            if (childMeasurement.Result == LayoutResultKind.Partial && childMeasurement.Remainder != null)
            {
                remainder = new SizedComponent(
                    childMeasurement.Remainder,
                    _minWidth,
                    _maxWidth,
                    _width,
                    _minHeight,
                    _maxHeight,
                    _height,
                    _aspectRatio,
                    _fillWidth,
                    _fillHeight,
                    _shrinkWidth,
                    _shrinkHeight);
            }

            float contentHeight = childMeasurement.ContentHeight;

            if (_aspectRatio.HasValue)
            {
                contentHeight = desiredWidth / _aspectRatio.Value;
            }

            if (_height.HasValue)
            {
                contentHeight = _height.Value;
            }
            else
            {
                if (_minHeight.HasValue)
                    contentHeight = Math.Max(contentHeight, _minHeight.Value);
                if (_maxHeight.HasValue)
                    contentHeight = Math.Min(contentHeight, _maxHeight.Value);
            }

            float marginTop = childMeasurement.MarginTop;
            float marginBottom = childMeasurement.MarginBottom;
            float availableHeightWithoutMargins = Math.Max(0f, context.AvailableHeight - marginTop - marginBottom);

            if (_fillHeight)
            {
                contentHeight = Math.Max(contentHeight, availableHeightWithoutMargins);
            }

            if (_shrinkHeight)
            {
                contentHeight = Math.Min(contentHeight, availableHeightWithoutMargins);
            }

            float usedWidth = Math.Max(desiredWidth, childMeasurement.UsedWidth);

            var metadata = new SizedMetadata(_child, childMeasurement, desiredWidth);

            return new LayoutMeasurement(
                marginTop,
                contentHeight,
                marginBottom,
                usedWidth,
                metadata,
                childMeasurement.AvoidBreakInside,
                childMeasurement.Result,
                remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (measurement.Metadata is not SizedMetadata meta)
                throw new InvalidOperationException("Sized measurement metadata missing.");

            var column = context.Column;
            var innerColumn = new FlowColumn(column.Index, column.X, meta.Width, column.TopY, column.BottomY);
            var drawContext = new LayoutDrawContext(context.Page, innerColumn, context.ContentLeft, context.ContentTop, meta.Width, context.Options);
            meta.Child.Draw(drawContext, meta.ChildMeasurement);
        }

        private float ResolveWidth(float availableWidth)
        {
            float width = _fillWidth ? availableWidth : availableWidth;
            if (_width.HasValue)
                width = Math.Min(width, _width.Value);
            if (_minWidth.HasValue)
                width = Math.Max(width, _minWidth.Value);
            if (_maxWidth.HasValue)
                width = Math.Min(width, _maxWidth.Value);
            if (_shrinkWidth)
                width = Math.Min(width, availableWidth);
            return Math.Max(0f, width);
        }

        private static float? Normalize(float? value)
        {
            if (!value.HasValue)
                return null;
            if (float.IsNaN(value.Value) || float.IsInfinity(value.Value))
                return null;
            return Math.Max(0f, value.Value);
        }

        private sealed class SizedMetadata
        {
            public SizedMetadata(IMeasurable child, LayoutMeasurement childMeasurement, float width)
            {
                Child = child;
                ChildMeasurement = childMeasurement;
                Width = width;
            }

            public IMeasurable Child { get; }
            public LayoutMeasurement ChildMeasurement { get; }
            public float Width { get; }
        }
    }
}
