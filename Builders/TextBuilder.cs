using System;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    public class TextBuilder
    {
        private readonly ColumnBuilder _col;
        private readonly TextElement _text;

        public TextBuilder(ColumnBuilder col, string content, float x, float y, float defaultWidth)
        {
            _col = col;
            _text = new TextElement(content, x, y)
            {
                FontSize = 12,
                FontFamily = "Helvetica",
                LineHeight = 1.2f,
                MaxWidth = defaultWidth
            };
            _col.ApplyTextDefaults(_text);
        }

        // --- Font and main style ---
        public TextBuilder FontFamily(string value)   { _text.FontFamily = value; return this; }
        public TextBuilder FontSize(float value)      { _text.FontSize = value; return this; }
        public TextBuilder Bold()                     { _text.Bold = true; return this; }
        public TextBuilder Italic()                   { _text.Italic = true; return this; }
        public TextBuilder Underline()                { _text.Underline = true; return this; }
        public TextBuilder Strikethrough()            { _text.Strikethrough = true; return this; }
        public TextBuilder Overline()                 { _text.Overline = true; return this; }
        public TextBuilder SmallCaps()                { _text.SmallCaps = true; return this; }
        public TextBuilder Monospace()                { _text.Monospace = true; return this; }

        public TextBuilder Color(string value)        { _text.Color = value; return this; }
        public TextBuilder Opacity(float value)       { _text.Opacity = value; return this; }
        public TextBuilder BackgroundColor(string value) { _text.BackgroundColor = value; return this; }
        public TextBuilder DecorationColor(string value) { _text.DecorationColor = value; return this; }
        public TextBuilder DecorationThickness(float value) { _text.DecorationThickness = value; return this; }
        public TextBuilder DecorationStyle(TextDecorationStyle value) { _text.DecorationStyle = value; return this; }
        public TextBuilder LetterSpacing(float value)  { _text.LetterSpacing = value; return this; }
        public TextBuilder WordSpacing(float value)    { _text.WordSpacing = value; return this; }
        public TextBuilder Transform(TextTransform value) { _text.Transform = value; return this; }

        // --- Background box styling ---
        public TextBuilder BackgroundBorderColor(string value)     { _text.BackgroundBorderColor = value; return this; }
        public TextBuilder BackgroundBorderWidth(float value)      { _text.BackgroundBorderWidth = value; return this; }
        public TextBuilder BackgroundCornerRadius(float value)     { _text.BackgroundCornerRadius = value; return this; }
        public TextBuilder BackgroundCornerRadiusTopLeft(float value)     { _text.BackgroundCornerRadiusTopLeft = value; return this; }
        public TextBuilder BackgroundCornerRadiusTopRight(float value)    { _text.BackgroundCornerRadiusTopRight = value; return this; }
        public TextBuilder BackgroundCornerRadiusBottomLeft(float value)  { _text.BackgroundCornerRadiusBottomLeft = value; return this; }
        public TextBuilder BackgroundCornerRadiusBottomRight(float value) { _text.BackgroundCornerRadiusBottomRight = value; return this; }

        public TextBuilder BackgroundShadowOffsetX(float value)    { _text.BackgroundShadowOffsetX = value; return this; }
        public TextBuilder BackgroundShadowOffsetY(float value)    { _text.BackgroundShadowOffsetY = value; return this; }
        public TextBuilder BackgroundShadowBlur(float value)       { _text.BackgroundShadowBlur = value; return this; }
        public TextBuilder BackgroundShadowColor(string value)     { _text.BackgroundShadowColor = value; return this; }

        // --- Margin (outside) ---
        public TextBuilder MarginTop(float value)      { _text.MarginTop = value; return this; }
        public TextBuilder MarginBottom(float value)   { _text.MarginBottom = value; return this; }
        public TextBuilder MarginLeft(float value)     { _text.MarginLeft = value; return this; }
        public TextBuilder MarginRight(float value)    { _text.MarginRight = value; return this; }

        // --- Padding (inside) ---
        public TextBuilder PaddingTop(float value)     { _text.PaddingTop = value; return this; }
        public TextBuilder PaddingBottom(float value)  { _text.PaddingBottom = value; return this; }
        public TextBuilder PaddingLeft(float value)    { _text.PaddingLeft = value; return this; }
        public TextBuilder PaddingRight(float value)   { _text.PaddingRight = value; return this; }

        // --- Layout and alignment ---
        public TextBuilder Rotation(float value)       { _text.Rotation = value; return this; }
        public TextBuilder Alignment(TextAlignment value) { _text.Alignment = value; return this; }
        public TextBuilder MaxWidth(float value)       { _text.MaxWidth = value; return this; }
        public TextBuilder LineHeight(float value)     { _text.LineHeight = value; return this; }
        public TextBuilder BaselineOffset(float value) { _text.BaselineOffset = value; return this; }
        public TextBuilder FlowDirection(FlowDirection direction) { _text.FlowDirection = direction; return this; }
        public TextBuilder Span(string text, Action<TextSpan>? configure = null)
        {
            var span = new TextSpan { Text = text ?? string.Empty };
            configure?.Invoke(span);
            _text.Spans.Add(span);
            return this;
        }

        public TextBuilder ClearSpans()
        {
            _text.Spans.Clear();
            return this;
        }

        // --- End chain and add to column ---
        public float Add()
        {
            _text.FlowDirection = _col.CurrentFlowDirection;
            return _col.AddText(_text);
        }
    }
}

