using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private class CanonicalTextStyle : ITextDescriptor
    {
        private readonly TextStyleDefaults _style = TextStyleDefaults.CreateOverrides();
        private string? _styleName;
        public ITextStyleDescriptor FontFamily(string family) { _style.FontFamily = RequireText(family, nameof(family)); return this; }
        public ITextStyleDescriptor FontSize(float size) { _style.FontSize = Positive(size, nameof(size)); return this; }
        public ITextStyleDescriptor Bold() { _style.Bold = true; return this; }
        public ITextStyleDescriptor Italic() { _style.Italic = true; return this; }
        public ITextStyleDescriptor Color(string color) { _style.Color = RequireText(color, nameof(color)); return this; }
        public ITextStyleDescriptor Highlight(string color) { _style.BackgroundColor = RequireText(color, nameof(color)); return this; }
        public ITextStyleDescriptor LineHeight(float value) { _style.LineHeight = Positive(value, nameof(value)); return this; }
        public ITextStyleDescriptor LetterSpacing(float value) { _style.LetterSpacing = Finite(value, nameof(value)); return this; }
        public ITextStyleDescriptor WordSpacing(float value) { _style.WordSpacing = Finite(value, nameof(value)); return this; }
        public ITextStyleDescriptor Underline() { _style.Underline = true; return this; }
        public ITextStyleDescriptor Strikethrough() { _style.Strikethrough = true; return this; }
        public ITextStyleDescriptor Overline() { _style.Overline = true; return this; }
        public ITextStyleDescriptor Decoration(string? color = null, float? thickness = null, TextDecorationStyle style = TextDecorationStyle.Solid) { SetDecoration(_style, color, thickness, style); return this; }
        public ITextStyleDescriptor Superscript() { _style.Superscript = true; _style.Subscript = false; return this; }
        public ITextStyleDescriptor Subscript() { _style.Subscript = true; _style.Superscript = false; return this; }
        public ITextStyleDescriptor AlignLeft() { _style.Alignment = TextAlignment.Left; return this; }
        public ITextStyleDescriptor AlignCenter() { _style.Alignment = TextAlignment.Center; return this; }
        public ITextStyleDescriptor AlignRight() { _style.Alignment = TextAlignment.Right; return this; }
        public ITextStyleDescriptor Justify() { _style.Alignment = TextAlignment.Justify; return this; }
        public ITextStyleDescriptor Direction(TextDirection direction) { _style.Direction = direction; return this; }
        public ITextStyleDescriptor Wrap() { _style.Wrapping = TextWrapping.Wrap; return this; }
        public ITextStyleDescriptor NoWrap() { _style.Wrapping = TextWrapping.NoWrap; return this; }
        public ITextStyleDescriptor Hyphenate() { _style.Wrapping = TextWrapping.Hyphenate; return this; }
        public ITextStyleDescriptor Ellipsis() { _style.Ellipsis = true; return this; }
        public ITextStyleDescriptor MaximumLines(int value) { _style.MaximumLines = PositiveLines(value); return this; }
        public ITextStyleDescriptor FallbackFonts(params string[] families) { _style.FallbackFonts = ValidateFamilies(families); return this; }
        public ITextDescriptor Style(string name) { _styleName = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A named style is required.", nameof(name)) : name; return this; }
        public void Apply(TextStyleDefaults defaults) => ApplyResolved(defaults, null);
        public void Apply(TextElement element) { element.ThemeStyleName = _styleName; element.CanonicalStyleOverrides = _style.Clone(); _style.ApplyTo(element); }
        public void Apply(RichTextElement element, DocumentTheme theme)
        {
            if (_styleName != null)
            {
                if (!theme.TryGetTextStyle(_styleName, out var named)) throw new KeyNotFoundException($"Theme text style '{_styleName}' is not defined.");
                named.ApplyTo(element);
            }
            _style.ApplyTo(element);
        }
        public void Apply(RichRun run, DocumentTheme theme)
        {
            if (_styleName != null)
            {
                if (!theme.TryGetTextStyle(_styleName, out var named)) throw new KeyNotFoundException($"Theme text style '{_styleName}' is not defined.");
                named.ApplyTo(run);
            }
            _style.ApplyTo(run);
        }
        public void Apply(ChartElement chart, DocumentTheme theme)
        {
            var resolved = TextStyleDefaults.CreateOverrides();
            ApplyResolved(resolved, theme);
            if (!string.IsNullOrWhiteSpace(resolved.FontFamily))
            {
                chart.Font = MapFontVariant(resolved.FontFamily!, resolved.Bold == true, resolved.Italic == true);
                chart.LegendFont = chart.Font;
            }
            if (resolved.FontSize.HasValue)
            {
                chart.FontSize = resolved.FontSize.Value;
                chart.LegendFontSize = resolved.FontSize.Value;
            }
            if (!string.IsNullOrWhiteSpace(resolved.Color))
                chart.AxisColor = System.Drawing.ColorTranslator.FromHtml(theme.ResolveColor(resolved.Color!));
        }
        private void ApplyResolved(TextStyleDefaults target, DocumentTheme? theme)
        {
            if (_styleName != null && theme != null)
            {
                if (!theme.TryGetTextStyle(_styleName, out var named)) throw new KeyNotFoundException($"Theme text style '{_styleName}' is not defined.");
                target.CopyFrom(named);
            }
            CopyDefined(_style, target);
        }
    }

    private sealed class CanonicalRichTextDescriptor : IRichTextDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly CanonicalTextStyle _defaultStyle = new();
        private readonly List<(string Text, CanonicalTextStyle Style, string? Url, string? Anchor)> _spans = new();
        public CanonicalRichTextDescriptor(DocumentTheme theme) => _theme = theme;
        public ITextDescriptor DefaultStyle() => _defaultStyle;
        public ITextDescriptor Span(string text) { var style = new CanonicalTextStyle(); _spans.Add((text ?? string.Empty, style, null, null)); return style; }
        public ITextDescriptor ExternalLink(string text, string uri)
        {
            var style = new CanonicalTextStyle();
            _spans.Add((text ?? string.Empty, style, NavigationUriPolicy.ValidateExternal(uri), null));
            return style;
        }
        public ITextDescriptor InternalLink(string text, string anchorId)
        {
            var style = new CanonicalTextStyle();
            _spans.Add((text ?? string.Empty, style, null, NavigationUriPolicy.ValidateAnchorId(anchorId, nameof(anchorId))));
            return style;
        }
        public void Compose(Layout.ContentComposer composer)
        {
            composer.RichText(element =>
            {
                element.AvoidBreakInside = false;
                _defaultStyle.Apply(element, _theme);
                foreach (var span in _spans)
                {
                    var run = new RichRun
                    {
                        Text = span.Text,
                        FontFamily = element.FontFamily,
                        FontSize = element.FontSize,
                        Color = element.Color,
                        FallbackFonts = element.FallbackFonts?.ToList(),
                        LinkUrl = span.Url,
                        LinkAnchor = span.Anchor
                    };
                    _defaultStyle.Apply(run, _theme);
                    span.Style.Apply(run, _theme);
                    element.Runs.Add(run);
                }
            });
        }
    }

    private static void CopyDefined(TextStyleDefaults source, TextStyleDefaults target)
    {
        source.ApplyOverridesTo(target);
    }

    private static string RequireText(string value, string parameterName) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameterName) : value;
    private static float Positive(float value, string parameterName) => value <= 0f || float.IsNaN(value) || float.IsInfinity(value) ? throw new ArgumentOutOfRangeException(parameterName) : value;
    private static float Finite(float value, string parameterName) => float.IsNaN(value) || float.IsInfinity(value) ? throw new ArgumentOutOfRangeException(parameterName) : value;
    private static int PositiveLines(int value) => value <= 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    private static List<string> ValidateFamilies(string[] families)
    {
        if (families == null) throw new ArgumentNullException(nameof(families));
        var result = families.Where(family => !string.IsNullOrWhiteSpace(family)).Select(family => family.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (result.Count == 0) throw new ArgumentException("At least one fallback font family is required.", nameof(families));
        return result;
    }
    private static void SetDecoration(TextStyleDefaults target, string? color, float? thickness, TextDecorationStyle style)
    {
        if (color != null) target.DecorationColor = RequireText(color, nameof(color));
        if (thickness.HasValue) target.DecorationThickness = Positive(thickness.Value, nameof(thickness));
        target.DecorationStyle = style;
    }
    private static string MapFontVariant(string family, bool bold, bool italic)
    {
        if (!bold && !italic) return family;
        if (family.Contains("Helvetica", StringComparison.OrdinalIgnoreCase)) return bold && italic ? "Helvetica-BoldOblique" : bold ? "Helvetica-Bold" : "Helvetica-Oblique";
        if (family.Contains("Times", StringComparison.OrdinalIgnoreCase)) return bold && italic ? "Times-BoldItalic" : bold ? "Times-Bold" : "Times-Italic";
        if (family.Contains("Courier", StringComparison.OrdinalIgnoreCase)) return bold && italic ? "Courier-BoldOblique" : bold ? "Courier-Bold" : "Courier-Oblique";
        return family;
    }
}
