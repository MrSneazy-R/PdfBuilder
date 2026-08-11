using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalChartDescriptor : IChartDescriptor
    {
        private readonly DocumentTheme _theme;
        public ChartElement Chart { get; } = new();
        public CanonicalChartDescriptor(DocumentTheme theme) => _theme = theme;
        public void Size(float width, float height)
        {
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height)) throw new ArgumentOutOfRangeException(nameof(width));
            Chart.Width = width;
            Chart.Height = height;
        }
        public void Title(string value) => Chart.Title = value ?? string.Empty;
        public void LabelStyle(Action<ITextStyleDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var style = new CanonicalTextStyle();
            configure(style);
            style.Apply(Chart, _theme);
        }
        public void Line(string name, IEnumerable<float> values, PdfColor color, float strokeWidth = 1f)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (strokeWidth <= 0f || float.IsNaN(strokeWidth)) throw new ArgumentOutOfRangeException(nameof(strokeWidth));
            var series = new LineSeries { Name = name ?? string.Empty, Stroke = ToDrawingColor(color), StrokeWidth = strokeWidth };
            series.Points.AddRange(values.Select((value, index) => new System.Drawing.PointF(index, value)));
            Chart.Series.Add(series);
        }
        public void Bars(string name, IEnumerable<float> values, PdfColor color)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var series = new BarSeries { Name = name ?? string.Empty, Fill = ToDrawingColor(color), Stroke = ToDrawingColor(color) };
            foreach (var (value, index) in values.Select((value, index) => (value, index))) series.Bars.Add((index, value));
            Chart.Series.Add(series);
        }
        private static System.Drawing.Color ToDrawingColor(PdfColor color) => System.Drawing.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }
}
