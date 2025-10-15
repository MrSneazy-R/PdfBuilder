using PdfBuilder.Document;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout
{
    public readonly struct DecorationDrawContext
    {
        public DecorationDrawContext(PdfPage page, FlowRect rect, LayoutOptions options)
        {
            Page = page;
            Rect = rect;
            Options = options;
        }

        public PdfPage Page { get; }

        public FlowRect Rect { get; }

        public LayoutOptions Options { get; }
    }
}

