using System;
using PdfBuilder.Document.Layout;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    /// <summary>
    /// Provides a declarative entry point for composing documents using the measure/draw pipeline.
    /// </summary>
    public sealed class DocumentComposer
    {
        private readonly PdfDocumentBuilder _builder;
        private readonly PdfDocument _document;

        internal DocumentComposer(PdfDocumentBuilder builder, PdfDocument document)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public DocumentComposer Page(Action<PageComposer> configure) =>
            Page(PdfPage.DefaultWidth, PdfPage.DefaultHeight, configure);

        public DocumentComposer Page(float width, float height, Action<PageComposer> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var page = _document.AddPage(width, height);
            _builder.ApplySectionTo(page);

            var pageComposer = new PageComposer(_builder, page);
            configure(pageComposer);
            pageComposer.Flush();
            return this;
        }
    }

    public sealed class PageComposer
    {
        private readonly PdfDocumentBuilder _builder;
        private readonly PdfDocument _document;
        private readonly PdfPage _page;
        private readonly PdfPageBuilder _pageBuilder;
        private bool _contentInvoked;

        internal PageComposer(PdfDocumentBuilder builder, PdfPage page)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _document = builder.Document;
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _pageBuilder = new PdfPageBuilder(_page, _document);

            float defaultMargin = builder.GetDefaultContentMargin();
            if (defaultMargin > 0)
            {
                Margin(defaultMargin);
            }
        }

        public PageComposer Margin(float value)
        {
            _pageBuilder.Margin(Math.Max(0f, value));
            return this;
        }

        public PageComposer Margins(float left, float top, float right, float bottom)
        {
            _page.MarginLeft = Math.Max(0f, left);
            _page.MarginTop = Math.Max(0f, top);
            _page.MarginRight = Math.Max(0f, right);
            _page.MarginBottom = Math.Max(0f, bottom);
            return this;
        }

        public PageComposer DefaultTextStyle(Action<TextStyleDefaults> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_page.TextDefaults);
            return this;
        }

        public PageComposer Background(string color)
        {
            _pageBuilder.Background(color);
            return this;
        }

        public PageComposer AutoPaginate(bool enabled = true)
        {
            if (enabled)
                _pageBuilder.AutoPaginate(_document);
            return this;
        }

        public PageComposer Compose(Action<LayoutComponentCollection> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            _contentInvoked = true;
            _pageBuilder.Content(column => column.Compose(configure));
            return this;
        }

        public PageComposer Content(Action<ContentComposer> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            _contentInvoked = true;
            _pageBuilder.Content(column => column.ComposeContent(configure));
            return this;
        }

        public PageComposer Column(Action<ColumnBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            _contentInvoked = true;
            _pageBuilder.Content(configure);
            return this;
        }

        public PageComposer HeaderText(string template, TextAlignment align = TextAlignment.Left)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            var hf = EnsureHeaderFooter();
            hf.HeaderTemplate = template;
            hf.HeaderAlign = align;
            return this;
        }

        public PageComposer FooterText(string template, TextAlignment align = TextAlignment.Right)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            var hf = EnsureHeaderFooter();
            hf.FooterTemplate = template;
            hf.FooterAlign = align;
            return this;
        }

        public PageComposer HeaderFooter(Action<HeaderFooterSpec> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var hf = EnsureHeaderFooter();
            configure(hf);
            return this;
        }

        public PageComposer Header(Action<ContentComposer> configure, float? spacing = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var hf = EnsureHeaderFooter();
            hf.HeaderLayout = new HeaderFooterLayoutDefinition(configure) { DefaultSpacing = spacing };
            return this;
        }

        public PageComposer Footer(Action<ContentComposer> configure, float? spacing = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var hf = EnsureHeaderFooter();
            hf.FooterLayout = new HeaderFooterLayoutDefinition(configure) { DefaultSpacing = spacing };
            return this;
        }

        public PageComposer PageNumbering(PageNumberPlacement placement = PageNumberPlacement.FooterRight, string template = "{page} / {pages}")
        {
            var hf = EnsureHeaderFooter();
            ApplyPageNumberTemplate(hf, placement, template);
            return this;
        }

        internal void Flush()
        {
            if (!_contentInvoked)
            {
                _pageBuilder.Content(_ => { });
            }
        }

        private HeaderFooterSpec EnsureHeaderFooter()
        {
            if (_page.HeaderFooterOverride == null)
            {
                var source = _document.HeaderFooter ?? new HeaderFooterSpec();
                _page.HeaderFooterOverride = CloneHeaderFooter(source);
            }
            return _page.HeaderFooterOverride!;
        }

        private static HeaderFooterSpec CloneHeaderFooter(HeaderFooterSpec source) => new HeaderFooterSpec
        {
            HeaderTemplate = source.HeaderTemplate,
            FooterTemplate = source.FooterTemplate,
            HeaderHeight = source.HeaderHeight,
            FooterHeight = source.FooterHeight,
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            Color = source.Color,
            HeaderAlign = source.HeaderAlign,
            FooterAlign = source.FooterAlign,
            FirstPageDifferent = source.FirstPageDifferent,
            FirstPageHeaderTemplate = source.FirstPageHeaderTemplate,
            FirstPageFooterTemplate = source.FirstPageFooterTemplate,
            HideOnLastPage = source.HideOnLastPage,
            HeaderLayout = source.HeaderLayout?.Clone(),
            FooterLayout = source.FooterLayout?.Clone(),
            HeaderVisibilityRules = source.HeaderVisibilityRules?.Select(rule => rule.Clone()).ToList(),
            FooterVisibilityRules = source.FooterVisibilityRules?.Select(rule => rule.Clone()).ToList()
        };

        private static void ApplyPageNumberTemplate(HeaderFooterSpec spec, PageNumberPlacement placement, string template)
        {
            switch (placement)
            {
                case PageNumberPlacement.HeaderLeft:
                    spec.HeaderTemplate = template;
                    spec.HeaderAlign = TextAlignment.Left;
                    break;
                case PageNumberPlacement.HeaderCenter:
                    spec.HeaderTemplate = template;
                    spec.HeaderAlign = TextAlignment.Center;
                    break;
                case PageNumberPlacement.HeaderRight:
                    spec.HeaderTemplate = template;
                    spec.HeaderAlign = TextAlignment.Right;
                    break;
                case PageNumberPlacement.FooterLeft:
                    spec.FooterTemplate = template;
                    spec.FooterAlign = TextAlignment.Left;
                    break;
                case PageNumberPlacement.FooterCenter:
                    spec.FooterTemplate = template;
                    spec.FooterAlign = TextAlignment.Center;
                    break;
                case PageNumberPlacement.FooterRight:
                default:
                    spec.FooterTemplate = template;
                    spec.FooterAlign = TextAlignment.Right;
                    break;
            }
        }
    }
}

