using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalPageDescriptor : IPageDescriptor
    {
        private readonly PdfDocument _document;
        private readonly CanonicalContainer _content;
        private readonly CanonicalContainer _header;
        private readonly CanonicalContainer _firstPageHeader;
        private readonly CanonicalContainer _continuationHeader;
        private readonly CanonicalContainer _footer;
        private readonly CanonicalContainer _firstPageFooter;
        private readonly CanonicalContainer _continuationFooter;
        private readonly CanonicalContainer _background;
        private PageSize _size = PageSizes.Letter;
        private PageOrientation _orientation = PageOrientation.Portrait;
        private float _left = 40f, _top = 40f, _right = 40f, _bottom = 40f;
        private readonly CanonicalTextStyle _defaultStyle = new();
        private int _columnCount = 1;
        private float _columnGutter = 14f;
        private bool _hasHeader, _hasFirstPageHeader, _hasContinuationHeader;
        private bool _hasFooter, _hasFirstPageFooter, _hasContinuationFooter;
        private bool _hasBackground, _firstPageDifferent, _hideFooterOnLastPage;
        private readonly CanonicalCompositionState _compositionState;

        public CanonicalPageDescriptor(PdfDocument document, CanonicalCompositionState compositionState)
        {
            _document = document;
            _compositionState = compositionState;
            _content = NewContainer();
            _header = NewContainer();
            _firstPageHeader = NewContainer();
            _continuationHeader = NewContainer();
            _footer = NewContainer();
            _firstPageFooter = NewContainer();
            _continuationFooter = NewContainer();
            _background = NewContainer();

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
        public IContainer FirstPageHeader()
        {
            if (!_hasFirstPageHeader) _firstPageHeader.FirstPageOnly();
            _hasFirstPageHeader = true;
            return _firstPageHeader;
        }
        public IContainer ContinuationHeader()
        {
            if (!_hasContinuationHeader) _continuationHeader.ContinuationPagesOnly();
            _hasContinuationHeader = true;
            return _continuationHeader;
        }
        public IContainer Footer() { _hasFooter = true; return _footer; }
        public IContainer FirstPageFooter()
        {
            if (!_hasFirstPageFooter) _firstPageFooter.FirstPageOnly();
            _hasFirstPageFooter = true;
            return _firstPageFooter;
        }
        public IContainer ContinuationFooter()
        {
            if (!_hasContinuationFooter) _continuationFooter.ContinuationPagesOnly();
            _hasContinuationFooter = true;
            return _continuationFooter;
        }
        public IContainer Background() { _hasBackground = true; return _background; }
        public void FirstPageDifferent()
        {
            if (!_firstPageDifferent)
            {
                _header.ContinuationPagesOnly();
                _footer.ContinuationPagesOnly();
            }
            _firstPageDifferent = true;
        }
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
            if (HasAnyHeader || HasAnyFooter)
            {
                page.HeaderFooterOverride = new HeaderFooterSpec
                {
                    HeaderLayout = HasAnyHeader ? new HeaderFooterLayoutDefinition(ComposeHeader) : null,
                    FooterLayout = HasAnyFooter ? new HeaderFooterLayoutDefinition(ComposeFooter) : null,
                    FirstPageDifferent = _firstPageDifferent,
                    HideOnLastPage = _hideFooterOnLastPage,
                    HeaderVisibilityRules = HeaderRules(),
                    FooterVisibilityRules = FooterRules()
                };
            }
            if (_hasBackground)
                new PdfPageBuilder(page, _document).Margin(0).Content(column => column.ComposeContent(_background.Compose));
            new PdfPageBuilder(page, _document).Margin(0).AutoPaginate(_document).Content(column =>
                column.ComposeContent(composer => _content.Compose(composer)));
        }

        private bool HasAnyHeader => _hasHeader || _hasFirstPageHeader || _hasContinuationHeader;
        private bool HasAnyFooter => _hasFooter || _hasFirstPageFooter || _hasContinuationFooter;
        private CanonicalContainer NewContainer() => new(_document.Theme, pagination: _document.Pagination, compositionState: _compositionState);

        private void ComposeHeader(Layout.ContentComposer composer)
        {
            if (_hasHeader) _header.Compose(composer, "Header");
            if (_hasFirstPageHeader) _firstPageHeader.Compose(composer, "First-page header");
            if (_hasContinuationHeader) _continuationHeader.Compose(composer, "Continuation header");
        }

        private void ComposeFooter(Layout.ContentComposer composer)
        {
            if (_hasFooter) _footer.Compose(composer, "Footer");
            if (_hasFirstPageFooter) _firstPageFooter.Compose(composer, "First-page footer");
            if (_hasContinuationFooter) _continuationFooter.Compose(composer, "Continuation footer");
        }

        private List<PageVisibilityRule> HeaderRules()
        {
            var rules = new List<PageVisibilityRule>();
            if (_hasHeader) rules.Add(_header.VisibilityRule);
            if (_hasFirstPageHeader) rules.Add(_firstPageHeader.VisibilityRule);
            if (_hasContinuationHeader) rules.Add(_continuationHeader.VisibilityRule);
            return rules;
        }

        private List<PageVisibilityRule> FooterRules()
        {
            var rules = new List<PageVisibilityRule>();
            if (_hasFooter) rules.Add(_footer.VisibilityRule);
            if (_hasFirstPageFooter) rules.Add(_firstPageFooter.VisibilityRule);
            if (_hasContinuationFooter) rules.Add(_continuationFooter.VisibilityRule);
            return rules;
        }
    }
}
