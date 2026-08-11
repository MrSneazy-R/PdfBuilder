using System;
using System.Collections.Generic;
using System.IO;
using PdfBuilder.Document.Layout;
using PdfBuilder.Fonts;
using PdfBuilder.Models;
using PdfBuilder.Writer.Fonts;

namespace PdfBuilder.Document
{
    public partial class PdfDocumentBuilder
    {
        private readonly PdfDocument _doc;
        private HeaderFooterSpec _currentSectionHF;
        private MasterPageSpec _currentSectionMaster;
        private TextStyleDefaults _currentTextDefaults;
        private float _defaultContentMargin = 36f;

        public PdfDocumentBuilder(PdfDocument doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _currentSectionHF = _doc.HeaderFooter;     // start off pointing at doc defaults
            _currentSectionMaster = _doc.Master;
            _currentTextDefaults = _doc.TextDefaults.Clone();

            ApplyLayoutDebugFromEnvironment();
            ApplyFontSettingsFromEnvironment();
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

        public PdfDocumentBuilder Profiler(Action<LayoutProfilerConfig> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_doc.LayoutOptions.Profiler);
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

        public PdfDocumentBuilder DefaultTextStyle(Action<TextStyleDefaults> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_doc.TextDefaults);
            _currentTextDefaults = _doc.TextDefaults.Clone();
            return this;
        }

        /// <summary>Configures document-scoped named colors, text styles, and spacing values.</summary>
        public PdfDocumentBuilder Theme(Action<DocumentThemeBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(new DocumentThemeBuilder(_doc.Theme));
            _doc.TextDefaults.CopyFrom(_doc.Theme.DefaultTextStyle);
            _currentTextDefaults = _doc.TextDefaults.Clone();
            return this;
        }

        public PdfDocumentBuilder Metadata(Action<DocumentMetadata> configure)
        {
            configure?.Invoke(_doc.Metadata);
            return this;
        }

        // --- New: Header/Footer full control on the document defaults ---
        public PdfDocumentBuilder HeaderFooter(Action<HeaderFooterSpec> cfg)
        {
            cfg?.Invoke(_doc.HeaderFooter);
            _currentSectionHF = _doc.HeaderFooter;
            return this;
        }

        public PdfDocumentBuilder Header(Action<ContentComposer> configure, float? spacing = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var definition = new HeaderFooterLayoutDefinition(configure) { DefaultSpacing = spacing };
            (_currentSectionHF ?? _doc.HeaderFooter).HeaderLayout = definition;
            return this;
        }

        public PdfDocumentBuilder Footer(Action<ContentComposer> configure, float? spacing = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var definition = new HeaderFooterLayoutDefinition(configure) { DefaultSpacing = spacing };
            (_currentSectionHF ?? _doc.HeaderFooter).FooterLayout = definition;
            return this;
        }

        // --- New: Master page / background / watermark defaults ---
        public PdfDocumentBuilder Master(Action<MasterPageSpec> cfg)
        {
            cfg?.Invoke(_doc.Master);
            _currentSectionMaster = _doc.Master;
            return this;
        }

        public PdfDocumentBuilder OutputOptions(Action<PdfOutputOptions> configure)
        {
            configure?.Invoke(_doc.OutputOptions);
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
                                               Action<MasterPageSpec>? master = null,
                                               Action<TextStyleDefaults>? textDefaults = null)
        {
            // Clone current so edits don't mutate the doc defaults
            _currentSectionHF = Clone(_currentSectionHF);
            _currentSectionMaster = Clone(_currentSectionMaster);
            _currentTextDefaults = _currentTextDefaults.Clone();
            headerFooter?.Invoke(_currentSectionHF);
            master?.Invoke(_currentSectionMaster);
            textDefaults?.Invoke(_currentTextDefaults);
            return this;
        }

        // Helper: attach current section settings to a page you just created
        public PdfDocumentBuilder ApplySectionTo(PdfPage page)
        {
            page.HeaderFooterOverride = Clone(_currentSectionHF);
            page.MasterOverride = Clone(_currentSectionMaster);
            page.LayoutOptions = _doc.LayoutOptions.Clone();
            page.TextDefaults = _currentTextDefaults.Clone();
            page.Theme = _doc.Theme.Clone();
            if (page.Theme.Page.Margin.HasValue)
            {
                float margin = Math.Max(0f, page.Theme.Page.Margin.Value);
                page.MarginTop = page.MarginRight = page.MarginBottom = page.MarginLeft = margin;
            }
            if (!string.IsNullOrWhiteSpace(page.Theme.Page.BackgroundColor))
                page.BackgroundColor = page.Theme.ResolveColor(page.Theme.Page.BackgroundColor!);
            return this;
        }

        public PdfDocumentBuilder GenerationOptions(Action<PdfGenerationOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_doc.GenerationOptions);
            return this;
        }


        internal float GetDefaultContentMargin() => _doc.Theme.Page.Margin ?? _defaultContentMargin;

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
            HideOnLastPage = s.HideOnLastPage,
            HeaderLayout = s.HeaderLayout?.Clone(),
            FooterLayout = s.FooterLayout?.Clone(),
            HeaderVisibilityRules = s.HeaderVisibilityRules?.Select(rule => rule.Clone()).ToList(),
            FooterVisibilityRules = s.FooterVisibilityRules?.Select(rule => rule.Clone()).ToList()
        };

        private void ApplyFontSettingsFromEnvironment()
        {
            const string diagnosticsVariable = "PDFBUILDER_FONT_DIAGNOSTICS";
            var diagValue = Environment.GetEnvironmentVariable(diagnosticsVariable);
            if (!string.IsNullOrWhiteSpace(diagValue))
            {
                if (IsTrue(diagValue))
                {
                    FontDiagnostics.Enabled = true;
                }
                else if (IsFalse(diagValue))
                {
                    FontDiagnostics.Enabled = false;
                }
            }

            const string foldersVariable = "PDFBUILDER_FONT_FOLDERS";
            var folderValue = Environment.GetEnvironmentVariable(foldersVariable);
            if (string.IsNullOrWhiteSpace(folderValue))
                return;

            var separators = new[] { ';', ',', '|', ':' };
            var tokens = folderValue.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (token.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    FontCatalog.RegisterSystemFonts();
                    continue;
                }

                try
                {
                    FontCatalog.RegisterFolder(token, SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    FontDiagnostics.Report($"Failed to register font folder '{token}': {ex.Message}");
                }
            }

            static bool IsTrue(string value)
                => value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("on", StringComparison.OrdinalIgnoreCase);

            static bool IsFalse(string value)
                => value.Equals("false", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("0", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("no", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("off", StringComparison.OrdinalIgnoreCase);
        }

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


