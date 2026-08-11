using System;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    public class PdfPageBuilder
    {
        private readonly PdfPage _page;
        private readonly PdfDocument? _document;
        private float _margin;

        // When set, ColumnBuilder will be given a factory to create new pages
        private PdfDocument? _autoDoc;

        public PdfPageBuilder(PdfPage page, PdfDocument? document = null)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _document = document;
        }

        public PdfPageBuilder Margin(float value)
        {
            _margin = value;
            return this;
        }

        /// <summary>
        /// Enable automatic page breaks for this page's content. Any time a block
        /// (text, image, table, chart) won't fit above the bottom margin, a new
        /// page (same size/background) will be created and appended to <paramref name="doc"/>.
        /// </summary>
        public PdfPageBuilder AutoPaginate(PdfDocument doc)
        {
            _autoDoc = doc ?? throw new ArgumentNullException(nameof(doc));
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
            if (columnAction == null) throw new ArgumentNullException(nameof(columnAction));

            // If auto-paginating, make sure the base page is already in the doc
            if (_autoDoc != null && !_autoDoc.Pages.Contains(_page))
                _autoDoc.Pages.Add(_page);

            Func<PdfPage>? newPageFactory = null;
            if (_autoDoc != null)
            {
                var autoDoc = _autoDoc;
                newPageFactory = () =>
                {
                    var p = new PdfPage(_page.Width, _page.Height)
                    {
                        BackgroundColor = _page.BackgroundColor,
                        LayoutOptions = _page.LayoutOptions.Clone(),
                        Theme = _page.Theme.Clone(),
                        MarginTop = _page.MarginTop,
                        MarginBottom = _page.MarginBottom,
                        MarginLeft = _page.MarginLeft,
                        MarginRight = _page.MarginRight,
                        TextDefaults = _page.TextDefaults.Clone(),
                        HeaderFooterOverride = _page.HeaderFooterOverride,
                        MasterOverride = _page.MasterOverride,
                        Columns = _page.Columns == null ? null : new ColumnLayoutSpec
                        {
                            Columns = _page.Columns.Columns,
                            Gutter = _page.Columns.Gutter,
                            Widths = _page.Columns.Widths == null ? null : (float[])_page.Columns.Widths.Clone()
                        }
                    };
                    autoDoc!.Pages.Add(p);
                    p.Owner = autoDoc;
                    p.Pagination = autoDoc.Pagination;
                    p.ProfilerSession = autoDoc.ProfilerSession;
                    p.CompositionPageNumber = autoDoc.Pages.Count;
                    return p;
                };
            }

            var column = new ColumnBuilder(
                _page,
                _margin,
                defaultSpacing: 8f,
                newPage: newPageFactory,
                hfForPage: page => page.HeaderFooterOverride ?? (_document ?? _autoDoc)?.HeaderFooter,
                layoutOptions: _page.LayoutOptions,
                textDefaults: _page.TextDefaults,
                document: _document ?? _autoDoc);
            columnAction(column);
            return this;
        }


        public PdfPage Build() => _page;
    }
}




