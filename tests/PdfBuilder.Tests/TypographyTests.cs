using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests
{
    public class TypographyTests
    {
        private sealed record LineSnapshot(float Width, IReadOnlyList<string> FontFamilies);

        private sealed record ParagraphSnapshot(string SourceText, IReadOnlyList<LineSnapshot> Lines);

        private static ParagraphSnapshot Shape(TextElement element, float width = 0f)
        {
            var layoutType = typeof(TextElement).Assembly.GetType("PdfBuilder.TextShaping.TextElementLayouter");
            layoutType.Should().NotBeNull("TextElementLayouter must exist");
            var method = layoutType!.GetMethod("Layout", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            method.Should().NotBeNull("Layout method must be accessible via reflection");
            var result = method!.Invoke(null, new object[] { element, width });
            result.Should().NotBeNull();
            var paragraphType = result!.GetType();

            var sourceTextProp = paragraphType.GetProperty("SourceText", BindingFlags.Public | BindingFlags.Instance);
            sourceTextProp.Should().NotBeNull();
            string sourceText = (string)sourceTextProp!.GetValue(result)!;

            var linesProp = paragraphType.GetProperty("Lines", BindingFlags.Public | BindingFlags.Instance);
            linesProp.Should().NotBeNull();
            var linesEnumerable = (IEnumerable)linesProp!.GetValue(result)!;

            var lines = new List<LineSnapshot>();
            foreach (var lineObj in linesEnumerable)
            {
                var lineType = lineObj!.GetType();
                var widthProp = lineType.GetProperty("Width", BindingFlags.Public | BindingFlags.Instance);
                widthProp.Should().NotBeNull();
                float lineWidth = (float)widthProp!.GetValue(lineObj)!;

                var runsProp = lineType.GetProperty("Runs", BindingFlags.Public | BindingFlags.Instance);
                runsProp.Should().NotBeNull();
                var runsEnumerable = (IEnumerable)runsProp!.GetValue(lineObj)!;

                var fontFamilies = new List<string>();
                foreach (var runObj in runsEnumerable)
                {
                    var runType = runObj!.GetType();
                    var fontProp = runType.GetProperty("FontFamily", BindingFlags.Public | BindingFlags.Instance);
                    fontProp.Should().NotBeNull();
                    string font = (string)fontProp!.GetValue(runObj)!;
                    fontFamilies.Add(font);
                }

                lines.Add(new LineSnapshot(lineWidth, fontFamilies));
            }

            return new ParagraphSnapshot(sourceText, lines);
        }

        [Fact]
        public void LetterSpacing_IncreasesMeasuredWidth()
        {
            var baseline = new TextElement("AB", 0f, 0f) { FontSize = 12f };
            var spaced = new TextElement("AB", 0f, 0f) { FontSize = 12f, LetterSpacing = 1f };

            var baselineLayout = Shape(baseline);
            var spacedLayout = Shape(spaced);

            float baselineWidth = baselineLayout.Lines[0].Width;
            float spacedWidth = spacedLayout.Lines[0].Width;
            spacedWidth.Should().BeGreaterThan(baselineWidth);
        }

        [Fact]
        public void WordSpacing_AppliesAdditionalGapForSpaces()
        {
            var baseline = new TextElement("A B", 0f, 0f) { FontSize = 12f };
            var spaced = new TextElement("A B", 0f, 0f) { FontSize = 12f, WordSpacing = 1.5f };

            var baselineLayout = Shape(baseline);
            var spacedLayout = Shape(spaced);

            float baselineWidth = baselineLayout.Lines[0].Width;
            float spacedWidth = spacedLayout.Lines[0].Width;
            spacedWidth.Should().BeGreaterThan(baselineWidth);
        }

        [Fact]
        public void TextTransform_Uppercase_IsAppliedDuringLayout()
        {
            const string source = "transform me";

            var element = new TextElement(source, 0f, 0f)
            {
                Transform = TextTransform.Uppercase
            };

            var layout = Shape(element);
            string result = layout.SourceText;
            result.Should().Be(source.ToUpperInvariant());
        }

        [Fact]
        public void InlineSpans_ProduceDistinctRuns()
        {
            var element = new TextElement(string.Empty, 0f, 0f);
            element.Spans.Clear();
            element.Spans.Add(new TextSpan
            {
                Text = "seg",
                FontFamily = "Helvetica",
                Bold = true
            });
            element.Spans.Add(new TextSpan
            {
                Text = "ments",
                FontFamily = "Courier",
                Italic = true
            });

            var layout = Shape(element);
            layout.Lines.Should().NotBeEmpty();
            layout.Lines[0].FontFamilies.Should().HaveCountGreaterThanOrEqualTo(2);
            layout.Lines[0].FontFamilies.Distinct().Should().HaveCountGreaterThanOrEqualTo(2);
        }
    }
}
