using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalPageDescriptor : IPageDescriptor
    {
        private readonly PdfDocument _document;
        private readonly CanonicalContainer _content;
        private readonly CanonicalContainer _header;
        private readonly CanonicalContainer _footer;
        private readonly CanonicalContainer _background;
        private PageSize _size = PageSizes.Letter;
        private PageOrientation _orientation = PageOrientation.Portrait;
        private float _left = 40f, _top = 40f, _right = 40f, _bottom = 40f;
        private readonly CanonicalTextStyle _defaultStyle = new();
        private int _columnCount = 1;
        private float _columnGutter = 14f;
        private bool _hasHeader, _hasFooter, _hasBackground, _firstPageDifferent, _hideFooterOnLastPage;

        public CanonicalPageDescriptor(PdfDocument document)
        {
            _document = document;
            _content = new CanonicalContainer(document.Theme, pagination: document.Pagination);
            _header = new CanonicalContainer(document.Theme, pagination: document.Pagination);
            _footer = new CanonicalContainer(document.Theme, pagination: document.Pagination);
            _background = new CanonicalContainer(document.Theme, pagination: document.Pagination);

            if (document.Theme.Page.Margin.HasValue)
                _left = _top = _right = _bottom = document.Theme.Page.Margin.Value;
        }
        public void Size(PageSize size)
        {
            if (size.Width <= 0 || size.Height <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            _size = size;
        }
        public void Orientation(PageOrientation orientation) => _orientation = orientation;
        public void Margin(float value) => Margin(value, value, value, value);
        public void Margin(float left, float top, float right, float bottom)
        {
            if (left < 0 || top < 0 || right < 0 || bottom < 0) throw new ArgumentOutOfRangeException(nameof(left));
            _left = left; _top = top; _right = right; _bottom = bottom;
        }
        public void DefaultTextStyle(Action<ITextStyleDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_defaultStyle);
        }
        public IContainer Content() => _content;
        public IContainer Header() { _hasHeader = true; return _header; }
        public IContainer Footer() { _hasFooter = true; return _footer; }
        public IContainer Background() { _hasBackground = true; return _background; }
        public void FirstPageDifferent() => _firstPageDifferent = true;
        public void HideFooterOnLastPage() => _hideFooterOnLastPage = true;
        public void Columns(int count, float gutter = 14f)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (gutter < 0f || float.IsNaN(gutter) || float.IsInfinity(gutter)) throw new ArgumentOutOfRangeException(nameof(gutter));
            _columnCount = count; _columnGutter = gutter;
        }
        public void Build()
        {
            var size = _size.WithOrientation(_orientation);
            var page = _document.AddPage(size.Width, size.Height);
            page.MarginLeft = _left; page.MarginTop = _top; page.MarginRight = _right; page.MarginBottom = _bottom;
            if (!string.IsNullOrWhiteSpace(page.Theme.Page.BackgroundColor))
                page.BackgroundColor = page.Theme.ResolveColor(page.Theme.Page.BackgroundColor!);
            page.Columns = new ColumnLayoutSpec { Columns = _columnCount, Gutter = _columnGutter };
            _defaultStyle.Apply(page.TextDefaults);
            if (_hasHeader || _hasFooter)
            {
                page.HeaderFooterOverride = new HeaderFooterSpec
                {
                    HeaderLayout = _hasHeader ? new HeaderFooterLayoutDefinition(_header.Compose) : null,
                    FooterLayout = _hasFooter ? new HeaderFooterLayoutDefinition(_footer.Compose) : null,
                    FirstPageDifferent = _firstPageDifferent,
                    HideOnLastPage = _hideFooterOnLastPage
                };
            }
            if (_hasBackground)
                new PdfPageBuilder(page, _document).Margin(0).Content(column => column.ComposeContent(_background.Compose));
            new PdfPageBuilder(page, _document).Margin(0).AutoPaginate(_document).Content(column =>
                column.ComposeContent(composer => _content.Compose(composer)));
        }
    }
}
