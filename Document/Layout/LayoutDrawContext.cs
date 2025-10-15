using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout
{
    /// <summary>
    /// Context supplied during the draw phase of the measure/draw pipeline.
    /// </summary>
    public sealed class LayoutDrawContext
    {
        public LayoutDrawContext(
            PdfPage page,
            FlowColumn column,
            float contentLeft,
            float contentTop,
            float contentWidth,
            LayoutOptions options)
        {
            Page = page;
            Column = column;
            ContentLeft = contentLeft;
            ContentTop = contentTop;
            ContentWidth = contentWidth;
            Options = options;
        }

        public PdfPage Page { get; }

        public FlowColumn Column { get; }

        public float ContentLeft { get; }

        public float ContentTop { get; }

        public float ContentWidth { get; }

        public LayoutOptions Options { get; }
    }
}
