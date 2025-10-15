using System;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class ListComponent : IMeasurable
    {
        private readonly ListElement _element;
        private readonly float _defaultSpacing;

        public ListComponent(ListElement element, float defaultSpacing)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _defaultSpacing = defaultSpacing;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            float marginTop = _element.MarginTop ?? _defaultSpacing;
            float marginBottom = _element.MarginBottom ?? 0f;
            float lineHeight = _element.FontSize * _element.LineHeight;
            float itemSpacing = Math.Max(0f, _element.ItemSpacing);

            int topLevelCount = _element.Items.Count;
            if (topLevelCount == 0)
            {
                var metaEmpty = new ListLayoutMetadata(LayoutSplitUtils.CloneList(_element));
                return new LayoutMeasurement(
                    marginTop,
                    0f,
                    marginBottom,
                    context.Column.Width,
                    metaEmpty,
                    _element.AvoidBreakInside);
            }

            float totalHeight = (topLevelCount * lineHeight) + Math.Max(0, topLevelCount - 1) * itemSpacing;
            float availableHeight = context.AvailableHeight - marginTop - marginBottom;

            if (availableHeight <= 0f)
                return LayoutMeasurement.Wrap(context.Column.Width);

            if (totalHeight <= availableHeight + 0.1f)
            {
                var metaFull = new ListLayoutMetadata(LayoutSplitUtils.CloneList(_element));
                return new LayoutMeasurement(
                    marginTop,
                    totalHeight,
                    marginBottom,
                    context.Column.Width,
                    metaFull,
                    _element.AvoidBreakInside);
            }

            if (_element.AvoidBreakInside)
                return LayoutMeasurement.Wrap(context.Column.Width);

            float perItem = lineHeight + itemSpacing;
            int maxItems = (int)Math.Floor((availableHeight + itemSpacing) / perItem);
            if (maxItems <= 0)
                return LayoutMeasurement.Wrap(context.Column.Width);

            if (maxItems >= topLevelCount)
                maxItems = topLevelCount - 1;
            if (maxItems <= 0)
                return LayoutMeasurement.Wrap(context.Column.Width);

            var renderList = LayoutSplitUtils.CloneList(_element);
            renderList.Items.Clear();
            for (int i = 0; i < maxItems && i < _element.Items.Count; i++)
                renderList.Items.Add(LayoutSplitUtils.CloneListItem(_element.Items[i]));
            renderList.KeepWithNext = false;

            var remaining = LayoutSplitUtils.CloneList(_element);
            remaining.Items.Clear();
            for (int i = maxItems; i < _element.Items.Count; i++)
                remaining.Items.Add(LayoutSplitUtils.CloneListItem(_element.Items[i]));

            float renderHeight = (maxItems * lineHeight) + Math.Max(0, maxItems - 1) * itemSpacing;
            var metadata = new ListLayoutMetadata(renderList);

            return new LayoutMeasurement(
                marginTop,
                renderHeight,
                marginBottom,
                context.Column.Width,
                metadata,
                _element.AvoidBreakInside,
                LayoutResultKind.Partial,
                new ListComponent(remaining, _defaultSpacing));
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            var list = measurement.Metadata is ListLayoutMetadata meta
                ? meta.Element
                : _element;

            if (list.X == 0f) list.X = context.ContentLeft;
            if (list.Y == 0f) list.Y = context.ContentTop;
            if (!list.MaxWidth.HasValue || list.MaxWidth <= 0f)
                list.MaxWidth = context.ContentWidth;

            context.Page.AddElement(list);
        }

        private sealed class ListLayoutMetadata
        {
            public ListLayoutMetadata(ListElement element)
            {
                Element = element;
            }

            public ListElement Element { get; }
        }
    }
}
