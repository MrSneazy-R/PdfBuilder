using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    public class PdfPageBuilder
    {
        private readonly PdfPage _page;
        private float _margin;

        public PdfPageBuilder(PdfPage page)
        {
            _page = page;
        }

        public PdfPageBuilder Margin(float value)
        {
            _margin = value;
            return this;
        }

        public PdfPageBuilder Background(string color)
        {
            _page.BackgroundColor = color;
            return this;
        }

        // IMPORTANT: Only call this ONCE per page, with all content in one lambda
        public PdfPageBuilder Content(Action<ColumnBuilder> columnAction)
        {
            var column = new ColumnBuilder(_page, _margin);
            columnAction(column);
            return this;
        }

        public PdfPage Build() => _page;
    }
}
