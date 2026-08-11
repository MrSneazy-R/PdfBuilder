using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.TextShaping
{
    internal static class TextElementLayouter
    {
        public static ShapedParagraph Layout(TextElement element, float innerWidth)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            float targetWidth = innerWidth > 0f ? innerWidth : element.MaxWidth ?? 0f;
            if (targetWidth < 0f)
                targetWidth = 0f;

            if (element.Spans.Count == 0)
            {
                float shapingWidth = element.Wrapping == TextWrapping.NoWrap ? 0f : targetWidth;
                var request = BuildRequestForElement(element, shapingWidth);
                var paragraph = TextShaper.Shared.ShapeParagraph(request);
                return ConstrainLines(element, paragraph, targetWidth);
            }

            var rich = BuildRichElement(element);
            var layout = RichTextLayouter.Layout(rich, targetWidth);
            var shapedLines = new List<ShapedLine>(layout.Lines.Count);

            foreach (var line in layout.Lines)
            {
                var runs = line.Segments.Select(s => s.ShapedRun).ToList();
                string lineText = string.Concat(runs.Select(r => r.Text));
                shapedLines.Add(new ShapedLine(lineText, runs, line.Width, line.Ascent, line.Descent, line.LineHeight));
            }

            string sourceText = string.Join(string.Empty, element.Spans.Select(s => s.Text));
            if (string.IsNullOrEmpty(sourceText))
                sourceText = element.Text ?? string.Empty;

            var richParagraph = new ShapedParagraph(sourceText, shapedLines, layout.MaxLineWidth, layout.TotalHeight);
            return ConstrainLines(element, richParagraph, targetWidth);
        }

        private static TextShapingRequest BuildRequestForElement(TextElement element, float maxWidth)
        {
            return new TextShapingRequest(
                element.Text ?? string.Empty,
                element.FontFamily,
                element.FontSize,
                element.LineHeight,
                maxWidth,
                element.Bold,
                element.Italic,
                element.SmallCaps,
                element.Monospace,
                element.FallbackFonts,
                TypographyDirectionResolver.Resolve(element.Direction, element.Text, element.FlowDirection),
                element.LetterSpacing,
                element.WordSpacing,
                element.Transform,
                element.Wrapping);
        }

        private static ShapedParagraph ConstrainLines(TextElement element, ShapedParagraph paragraph, float maxWidth)
        {
            int? maximum = element.MaximumLines;
            bool widthOverflow = element.Wrapping == TextWrapping.NoWrap && maxWidth > 0f && paragraph.MaxLineWidth > maxWidth;
            bool lineOverflow = maximum.HasValue && paragraph.Lines.Count > maximum.Value;
            if (!widthOverflow && !lineOverflow)
                return paragraph;

            int count = maximum.HasValue ? Math.Min(maximum.Value, paragraph.Lines.Count) : Math.Min(1, paragraph.Lines.Count);
            var lines = paragraph.Lines.Take(Math.Max(1, count)).ToList();
            if (element.EllipsisWhenConstrained && lines.Count > 0 && maxWidth > 0f)
                lines[^1] = ShapeEllipsized(element, lines[^1].Text, maxWidth);

            float width = lines.Count == 0 ? 0f : lines.Max(line => line.Width);
            return new ShapedParagraph(paragraph.SourceText, lines, width, lines.Sum(line => line.LineHeight));
        }

        private static ShapedLine ShapeEllipsized(TextElement element, string text, float maxWidth)
        {
            const string ellipsis = "…";
            var pieces = new List<string>();
            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext()) pieces.Add(enumerator.GetTextElement());
            for (int count = pieces.Count; count >= 0; count--)
            {
                string candidate = string.Concat(pieces.Take(count)) + ellipsis;
                var shaped = TextShaper.Shared.ShapeParagraph(BuildRequestForElement(element, 0f, candidate));
                var line = shaped.Lines[0];
                if (line.Width <= maxWidth) return line;
            }
            return TextShaper.Shared.ShapeParagraph(BuildRequestForElement(element, 0f, string.Empty)).Lines[0];
        }

        private static TextShapingRequest BuildRequestForElement(TextElement element, float maxWidth, string text)
        {
            return new TextShapingRequest(text, element.FontFamily, element.FontSize, element.LineHeight, maxWidth,
                element.Bold, element.Italic, element.SmallCaps, element.Monospace, element.FallbackFonts,
                TypographyDirectionResolver.Resolve(element.Direction, text, element.FlowDirection), element.LetterSpacing,
                element.WordSpacing, element.Transform, element.Wrapping);
        }

        private static RichTextElement BuildRichElement(TextElement element)
        {
            var rich = new RichTextElement(element.X, element.Y)
            {
                FontFamily = element.FontFamily,
                FontSize = element.FontSize,
                LineHeight = element.LineHeight,
                Alignment = element.Alignment,
                MaxWidth = element.MaxWidth,
                Rotation = element.Rotation,
                FlowDirection = element.FlowDirection
                ,
                Direction = element.Direction
                ,
                Wrapping = element.Wrapping
                ,
                EllipsisWhenConstrained = element.EllipsisWhenConstrained
                ,
                MaximumLines = element.MaximumLines
            };

            if (element.Spans.Count == 0)
            {
                rich.Runs.Add(new RichRun
                {
                    Text = element.Text ?? string.Empty,
                    FontFamily = element.FontFamily,
                    FontSize = element.FontSize,
                    Bold = element.Bold,
                    Italic = element.Italic,
                    SmallCaps = element.SmallCaps,
                    Monospace = element.Monospace,
                    Underline = element.Underline,
                    Strikethrough = element.Strikethrough,
                    Overline = element.Overline,
                    Color = element.Color,
                    BackgroundColor = element.BackgroundColor,
                    DecorationColor = element.DecorationColor,
                    DecorationThickness = element.DecorationThickness,
                    DecorationStyle = element.DecorationStyle,
                    Superscript = (element.BaselineOffset ?? 0f) > 0f,
                    Subscript = (element.BaselineOffset ?? 0f) < 0f,
                    FallbackFonts = element.FallbackFonts == null ? null : new List<string>(element.FallbackFonts),
                    LetterSpacing = element.LetterSpacing,
                    WordSpacing = element.WordSpacing,
                    Transform = element.Transform == TextTransform.None ? null : element.Transform
                });
                return rich;
            }

            foreach (var span in element.Spans)
            {
                var run = new RichRun
                {
                    Text = span.Text ?? string.Empty,
                    FontFamily = string.IsNullOrWhiteSpace(span.FontFamily) ? element.FontFamily : span.FontFamily!,
                    FontSize = span.FontSize ?? element.FontSize,
                    Bold = span.Bold ?? element.Bold,
                    Italic = span.Italic ?? element.Italic,
                    SmallCaps = span.SmallCaps ?? element.SmallCaps,
                    Monospace = span.Monospace ?? element.Monospace,
                    Underline = span.Underline ?? element.Underline,
                    Strikethrough = span.Strikethrough ?? element.Strikethrough,
                    Overline = span.Overline ?? element.Overline,
                    Color = span.Color ?? element.Color,
                    BackgroundColor = span.BackgroundColor ?? element.BackgroundColor,
                    DecorationColor = span.DecorationColor ?? element.DecorationColor,
                    DecorationThickness = span.DecorationThickness ?? element.DecorationThickness,
                    DecorationStyle = span.DecorationStyle ?? element.DecorationStyle,
                    Superscript = span.Superscript ?? ((element.BaselineOffset ?? 0f) > 0f),
                    Subscript = span.Subscript ?? ((element.BaselineOffset ?? 0f) < 0f),
                    FallbackFonts = span.FallbackFonts != null ? new List<string>(span.FallbackFonts)
                        : element.FallbackFonts != null ? new List<string>(element.FallbackFonts) : null,
                    LetterSpacing = span.LetterSpacing ?? element.LetterSpacing,
                    WordSpacing = span.WordSpacing ?? element.WordSpacing,
                    Transform = ResolveTransform(span.Transform, element.Transform)
                };
                rich.Runs.Add(run);
            }

            return rich;
        }

        private static TextTransform? ResolveTransform(TextTransform? spanTransform, TextTransform elementTransform)
        {
            if (spanTransform.HasValue)
                return spanTransform.Value == TextTransform.None ? null : spanTransform;
            return elementTransform == TextTransform.None ? null : elementTransform;
        }
    }
}
