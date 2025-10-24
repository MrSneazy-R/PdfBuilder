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
                var request = BuildRequestForElement(element, targetWidth);
                return TextShaper.Shared.ShapeParagraph(request);
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

            return new ShapedParagraph(sourceText, shapedLines, layout.MaxLineWidth, layout.TotalHeight);
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
                element.FlowDirection,
                element.LetterSpacing,
                element.WordSpacing,
                element.Transform);
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
                    Color = element.Color,
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
                    Underline = element.Underline,
                    Strikethrough = element.Strikethrough,
                    Color = element.Color,
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
