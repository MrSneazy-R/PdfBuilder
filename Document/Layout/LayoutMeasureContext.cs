using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout
{
    /// <summary>
    /// Provides measurement-time metadata to components.
    /// </summary>
    public sealed class LayoutMeasureContext
    {
        public LayoutMeasureContext(PdfPage page, FlowColumn column, LayoutOptions options)
        {
            Page = page;
            Column = column;
            Options = options;
        }

        public PdfPage Page { get; }

        public FlowColumn Column { get; }

        public LayoutOptions Options { get; }

        public float AvailableWidth => Column.Width;

        public float AvailableHeight => Column.Available;
    }
}
