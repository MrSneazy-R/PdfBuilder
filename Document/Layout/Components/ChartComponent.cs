using System;
using System.Linq;
using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class ChartComponent : IMeasurable
    {
        private const float DefaultMarginTop = 8f;
        private const float DefaultMarginBottom = 0f;

        private readonly ChartElement _chart;

        public ChartComponent(ChartElement chart)
        {
            _chart = chart ?? throw new ArgumentNullException(nameof(chart));
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            float titleSpace = !string.IsNullOrWhiteSpace(_chart.Title) ? _chart.TitleSize * 1.2f : 0f;
            float bodyHeight = _chart.Height > 0f ? _chart.Height : 220f;
            float legendSpace = (_chart.ShowLegend && _chart.LegendPosition == ChartElement.LegendPos.Below)
                ? (14f * Math.Max(1, _chart.Series.Count) + 6f)
                : 0f;

            float contentHeight = titleSpace + bodyHeight + legendSpace;
            float targetWidth = _chart.Width > 0f ? _chart.Width : context.Column.Width;
            float availableHeight = context.AvailableHeight - DefaultMarginTop - DefaultMarginBottom;

            if (availableHeight <= 0f || contentHeight > availableHeight + 0.1f)
                return LayoutMeasurement.Wrap(targetWidth);

            var metadata = new ChartMetadata(targetWidth);

            return new LayoutMeasurement(
                DefaultMarginTop,
                contentHeight,
                DefaultMarginBottom,
                targetWidth,
                metadata,
                avoidBreakInside: true);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not ChartMetadata metadata)
                throw new InvalidOperationException("Chart measurement metadata missing.");

            if (_chart.X == 0f)
                _chart.X = context.ContentLeft;

            if (_chart.Y == 0f)
                _chart.Y = context.ContentTop;

            if (_chart.Width <= 0f)
                _chart.Width = metadata.TargetWidth;

            if (_chart.Height <= 0f)
                _chart.Height = 220f;

            context.Page.AddElement(_chart);
        }

        private sealed class ChartMetadata
        {
            public ChartMetadata(float targetWidth)
            {
                TargetWidth = targetWidth;
            }

            public float TargetWidth { get; }
        }
    }
}
