using PdfBuilder.Document.Layout;
using PdfBuilder.Models;
using System;

namespace PdfBuilder.Document
{
    public partial class PdfDocumentBuilder
    {
        private readonly PdfDocument _doc;
        private HeaderFooterSpec _currentSectionHF;
        private MasterPageSpec _currentSectionMaster;
        private float _defaultContentMargin = 36f;

        public PdfDocumentBuilder(PdfDocument doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _currentSectionHF = _doc.HeaderFooter;     // start off pointing at doc defaults
            _currentSectionMaster = _doc.Master;

            ApplyLayoutDebugFromEnvironment();
        }

        internal PdfDocument Document => _doc;

        public PdfDocument Build() => _doc;

        public PdfDocumentBuilder UseLayout(Action<LayoutOptions> configure)
        {
            configure?.Invoke(_doc.LayoutOptions);
            return this;
        }

        public PdfDocumentBuilder DefaultContentMargin(float value)
        {
            _defaultContentMargin = Math.Max(0f, value);
            return this;
        }

        public PdfDocumentBuilder LayoutDebug(Action<LayoutDebugOptions> configure)
        {
            configure?.Invoke(_doc.LayoutOptions.Debug);
            return this;
        }

        public PdfDocumentBuilder Compose(Action<DocumentComposer> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var composer = new DocumentComposer(this, _doc);
            configure(composer);
            return this;
        }

        // --- Existing header/footer quick setters (kept for compatibility) ---
        private string _headerText = string.Empty;
        private string _footerText = string.Empty;

        public PdfDocumentBuilder SetHeader(string text)
        {
            _headerText = text;
            _doc.HeaderFooter.HeaderTemplate = text;
            return this;
        }

        public PdfDocumentBuilder SetFooter(string text)
        {
            _footerText = text;
            _doc.HeaderFooter.FooterTemplate = text;
            return this;
        }

        public string GetHeader() => _headerText;
        public string GetFooter() => _footerText;

        // --- New: Document title (used by {title}) ---
        public PdfDocumentBuilder Title(string title) { _doc.Title = title; return this; }

        // --- New: Header/Footer full control on the document defaults ---
        public PdfDocumentBuilder HeaderFooter(Action<HeaderFooterSpec> cfg)
        {
            cfg?.Invoke(_doc.HeaderFooter);
            _currentSectionHF = _doc.HeaderFooter;
            return this;
        }

        // --- New: Master page / background / watermark defaults ---
        public PdfDocumentBuilder Master(Action<MasterPageSpec> cfg)
        {
            cfg?.Invoke(_doc.Master);
            _currentSectionMaster = _doc.Master;
            return this;
        }

        public PdfDocumentBuilder EnablePageNumbers(PageNumberPlacement placement = PageNumberPlacement.FooterRight, string template = "{page} / {pages}")
        {
            ApplyPageNumberTemplate(_doc.HeaderFooter, placement, template);
            _currentSectionHF = _doc.HeaderFooter;
            return this;
        }

        // --- New: Section semantics (subsequent pages inherit these until changed) ---
        public PdfDocumentBuilder StartSection(Action<HeaderFooterSpec>? headerFooter = null,
                                               Action<MasterPageSpec>? master = null)
        {
            // Clone current so edits don't mutate the doc defaults
            _currentSectionHF = Clone(_currentSectionHF);
            _currentSectionMaster = Clone(_currentSectionMaster);
            headerFooter?.Invoke(_currentSectionHF);
            master?.Invoke(_currentSectionMaster);
            return this;
        }

        // Helper: attach current section settings to a page you just created
        public PdfDocumentBuilder ApplySectionTo(PdfPage page)
        {
            page.HeaderFooterOverride = Clone(_currentSectionHF);
            page.MasterOverride = Clone(_currentSectionMaster);
            page.LayoutOptions = _doc.LayoutOptions.Clone();
            return this;
        }

        internal float GetDefaultContentMargin() => _defaultContentMargin;

        private void ApplyLayoutDebugFromEnvironment()
        {
            const string variable = "PDFBUILDER_LAYOUT_DEBUG";
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value))
                return;

            var tokens = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                switch (token.ToLowerInvariant())
                {
                    case "boxes":
                    case "bbox":
                        _doc.LayoutOptions.Debug.DrawBoundingBoxes = true;
                        break;
                    case "guides":
                    case "grid":
                        _doc.LayoutOptions.Debug.ShowFlowGuides = true;
                        break;
                    case "trace":
                    case "log":
                        _doc.LayoutOptions.Debug.TraceLayout = true;
                        break;
                }
            }
        }

        private static HeaderFooterSpec Clone(HeaderFooterSpec s) => new HeaderFooterSpec
        {
            HeaderTemplate = s.HeaderTemplate,
            FooterTemplate = s.FooterTemplate,
            HeaderHeight = s.HeaderHeight,
            FooterHeight = s.FooterHeight,
            FontFamily = s.FontFamily,
            FontSize = s.FontSize,
            Color = s.Color,
            HeaderAlign = s.HeaderAlign,
            FooterAlign = s.FooterAlign,
            FirstPageDifferent = s.FirstPageDifferent,
            FirstPageHeaderTemplate = s.FirstPageHeaderTemplate,
            FirstPageFooterTemplate = s.FirstPageFooterTemplate,
            HideOnLastPage = s.HideOnLastPage
        };

        private static MasterPageSpec Clone(MasterPageSpec m) => new MasterPageSpec
        {
            BackgroundColor = m.BackgroundColor,
            BackgroundImage = m.BackgroundImage,
            BackgroundImageMime = m.BackgroundImageMime,
            BackgroundImageX = m.BackgroundImageX,
            BackgroundImageY = m.BackgroundImageY,
            BackgroundImageWidth = m.BackgroundImageWidth,
            BackgroundImageHeight = m.BackgroundImageHeight,
            Watermark = m.Watermark == null ? null : new WatermarkSpec
            {
                Text = m.Watermark.Text,
                ImageData = m.Watermark.ImageData,
                ImageMime = m.Watermark.ImageMime,
                CenterOnPage = m.Watermark.CenterOnPage,
                FontFamily = m.Watermark.FontFamily,
                FontSize = m.Watermark.FontSize,
                Color = m.Watermark.Color,
                Opacity = m.Watermark.Opacity,
                RotationDegrees = m.Watermark.RotationDegrees,
                ImageWidth = m.Watermark.ImageWidth,
                ImageHeight = m.Watermark.ImageHeight,
                X = m.Watermark.X,
                Y = m.Watermark.Y,
                Layer = m.Watermark.Layer
            }
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
