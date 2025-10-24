using System;

namespace PdfBuilder.Document.Layout.Components
{
    /// <summary>
    /// Allows consumers to plug arbitrary measurement/draw logic into the layout pipeline without
    /// deriving from the built-in component types.
    /// </summary>
    public sealed class DelegateComponent : IMeasurable
    {
        private readonly Func<LayoutMeasureContext, LayoutMeasurement> _measure;
        private readonly Action<LayoutDrawContext, LayoutMeasurement> _draw;

        public DelegateComponent(
            Func<LayoutMeasureContext, LayoutMeasurement> measure,
            Action<LayoutDrawContext, LayoutMeasurement> draw)
        {
            _measure = measure ?? throw new ArgumentNullException(nameof(measure));
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
            => _measure(context ?? throw new ArgumentNullException(nameof(context)));

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));
            _draw(context, measurement);
        }
    }
}
