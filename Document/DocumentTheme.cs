using System;
using System.Collections.Generic;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    /// <summary>Document-scoped visual tokens. Instances are cloned when inherited by pages.</summary>
    public sealed class DocumentTheme
    {
        private readonly Dictionary<string, string> _colors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextStyleDefaults> _textStyles = new(StringComparer.OrdinalIgnoreCase);

        public TextStyleDefaults DefaultTextStyle { get; } = new();
        public PageTheme Page { get; } = new();
        public SpacingTheme Spacing { get; } = new();

        public IReadOnlyDictionary<string, string> Colors => _colors;
        public IReadOnlyDictionary<string, TextStyleDefaults> TextStyles => _textStyles;

        internal void SetColor(string name, string value) => _colors[name] = value;
        internal void SetTextStyle(string name, TextStyleDefaults value) => _textStyles[name] = value.Clone();

        internal string ResolveColor(string value)
            => _colors.TryGetValue(value, out var resolved) ? resolved : value;

        internal bool TryGetTextStyle(string name, out TextStyleDefaults style)
        {
            if (_textStyles.TryGetValue(name, out var stored))
            {
                style = stored.Clone();
                return true;
            }

            style = null!;
            return false;
        }

        internal DocumentTheme Clone()
        {
            var clone = new DocumentTheme();
            clone.DefaultTextStyle.CopyFrom(DefaultTextStyle);
            clone.Page.CopyFrom(Page);
            clone.Spacing.CopyFrom(Spacing);
            foreach (var color in _colors)
                clone._colors.Add(color.Key, color.Value);
            foreach (var style in _textStyles)
                clone._textStyles.Add(style.Key, style.Value.Clone());
            return clone;
        }
    }

    public sealed class PageTheme
    {
        public string? BackgroundColor { get; set; }
        public float? Margin { get; set; }

        internal void CopyFrom(PageTheme source)
        {
            BackgroundColor = source.BackgroundColor;
            Margin = source.Margin;
        }
    }

    public sealed class SpacingTheme
    {
        private readonly Dictionary<string, float> _values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, float> Values => _values;

        public float this[string name]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("A theme spacing token is required.", nameof(name));
                return _values.TryGetValue(name, out var value)
                    ? value
                    : throw new KeyNotFoundException($"Theme spacing '{name}' is not defined.");
            }
        }

        internal void Set(string name, float value) => _values[name] = value;

        internal void CopyFrom(SpacingTheme source)
        {
            _values.Clear();
            foreach (var value in source._values)
                _values.Add(value.Key, value.Value);
        }
    }

    /// <summary>Fluent, document-scoped theme configuration.</summary>
    public sealed class DocumentThemeBuilder
    {
        private readonly DocumentTheme _theme;

        internal DocumentThemeBuilder(DocumentTheme theme) => _theme = theme;

        public DocumentThemeBuilder DefaultTextStyle(Action<ThemeTextStyleBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(new ThemeTextStyleBuilder(_theme.DefaultTextStyle));
            return this;
        }

        public DocumentThemeBuilder Color(string name, string value)
        {
            ValidateName(name, nameof(name));
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A color value is required.", nameof(value));
            _theme.SetColor(name, value);
            return this;
        }

        public DocumentThemeBuilder TextStyle(string name, Action<ThemeTextStyleBuilder> configure)
        {
            ValidateName(name, nameof(name));
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var style = new TextStyleDefaults();
            configure(new ThemeTextStyleBuilder(style));
            _theme.SetTextStyle(name, style);
            return this;
        }

        public DocumentThemeBuilder Spacing(string name, float value)
        {
            ValidateName(name, nameof(name));
            if (!float.IsFinite(value) || value < 0f) throw new ArgumentOutOfRangeException(nameof(value));
            _theme.Spacing.Set(name, value);
            return this;
        }

        public DocumentThemeBuilder Page(Action<PageTheme> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_theme.Page);
            return this;
        }

        private static void ValidateName(string name, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Theme token names cannot be null or whitespace.", parameterName);
        }
    }

    public sealed class ThemeTextStyleBuilder
    {
        private readonly TextStyleDefaults _style;

        internal ThemeTextStyleBuilder(TextStyleDefaults style) => _style = style;

        public ThemeTextStyleBuilder FontFamily(string value) { _style.FontFamily = value; return this; }
        public ThemeTextStyleBuilder FontSize(float value) { _style.FontSize = value; return this; }
        public ThemeTextStyleBuilder Color(string value) { _style.Color = value; return this; }
        public ThemeTextStyleBuilder LineHeight(float value) { _style.LineHeight = value; return this; }
        public ThemeTextStyleBuilder LetterSpacing(float value) { _style.LetterSpacing = value; return this; }
        public ThemeTextStyleBuilder WordSpacing(float value) { _style.WordSpacing = value; return this; }
        public ThemeTextStyleBuilder Bold(bool value = true) { _style.Bold = value; return this; }
        public ThemeTextStyleBuilder Italic(bool value = true) { _style.Italic = value; return this; }
        public ThemeTextStyleBuilder Underline(bool value = true) { _style.Underline = value; return this; }
        public ThemeTextStyleBuilder Strikethrough(bool value = true) { _style.Strikethrough = value; return this; }
    }
}
