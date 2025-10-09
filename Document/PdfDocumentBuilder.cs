using PdfBuilder.Models;
using System;

namespace PdfBuilder.Document
{
    public partial class PdfDocumentBuilder
    {
        private readonly PdfDocument _doc;
        private HeaderFooterSpec _currentSectionHF;
        private MasterPageSpec _currentSectionMaster;

        public PdfDocumentBuilder(PdfDocument doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _currentSectionHF = _doc.HeaderFooter;     // start off pointing at doc defaults
            _currentSectionMaster = _doc.Master;
        }

        public PdfDocument Build() => _doc;

        // --- Existing header/footer quick setters (kept for compatibility) ---
        private string _headerText;
        private string _footerText;

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
            return this;
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
    }
}
