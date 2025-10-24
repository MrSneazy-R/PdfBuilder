using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Document.Layout.Components
{
    /// <summary>
    /// Measure/draw adapter for <see cref="TextElement"/> so existing builders can participate
    /// in the two-phase layout pipeline.
    /// </summary>
    internal sealed class TextComponent : IMeasurable
    {
        private readonly TextElement _element;
        private readonly float _defaultSpacing;

        public TextComponent(TextElement element, float defaultSpacing)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _defaultSpacing = defaultSpacing;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var column = context.Column;
            var text = _element;

            float marginTop = text.MarginTop ?? _defaultSpacing;
            float marginBottom = text.MarginBottom ?? 0f;
            float marginLeft = text.MarginLeft ?? 0f;
            float marginRight = text.MarginRight ?? 0f;

            float paddingTop = text.PaddingTop ?? 0f;
            float paddingBottom = text.PaddingBottom ?? 0f;
            float paddingLeft = text.PaddingLeft ?? 0f;
            float paddingRight = text.PaddingRight ?? 0f;

            string textContent = text.Text ?? string.Empty;

            float availableWidth = Math.Max(0f, column.Width - marginLeft - marginRight);
            float maxWidthConstraint = text.MaxWidth ?? availableWidth;
            float textMaxWidth = Math.Max(0f, maxWidthConstraint - paddingLeft - paddingRight);

            var shapedParagraph = TextElementLayouter.Layout(text, textMaxWidth);
            var shapedLines = shapedParagraph.Lines;
            if (shapedLines.Count == 0)
                shapedLines = new List<ShapedLine> { new ShapedLine(string.Empty, Array.Empty<ShapedRun>(), 0f, text.FontSize, 0f, text.FontSize * text.LineHeight) };

            text.ShapedLayout = shapedParagraph;
            text.ShapedStartLine = 0;
            text.ShapedLineCount = shapedLines.Count;

            float allLinesHeight = SumLineHeights(shapedLines, shapedLines.Count);
            float maxLineWidthAll = shapedLines.Count > 0 ? shapedLines.Max(l => l.Width) : 0f;
            float usedWidth = marginLeft + maxLineWidthAll + paddingLeft + paddingRight + marginRight;
            float availableHeight = context.AvailableHeight - marginTop - marginBottom;

            if (availableHeight <= 0f)
                return LayoutMeasurement.Wrap(usedWidth);

            float fullHeight = allLinesHeight + paddingTop + paddingBottom;
            bool fitsFully = fullHeight <= availableHeight + 0.1f;

            LayoutResultKind resultKind = LayoutResultKind.Full;
            IMeasurable? remainder = null;
            int renderLineCount = shapedLines.Count;

            if (!fitsFully && text.Rotation == 0f && !text.AvoidBreakInside && shapedLines.Count > 1)
            {
                renderLineCount = ComputeRenderableLineCount(shapedLines, availableHeight, paddingTop + paddingBottom, text.OrphanLines, text.WidowLines);

                if (renderLineCount > 0 && renderLineCount < shapedLines.Count)
                {
                    resultKind = LayoutResultKind.Partial;
                    var remainderLines = shapedLines.Skip(renderLineCount).Select(l => l.Text).ToList();
                    var remainderText = string.Join("\n", remainderLines);
                    var remainderElement = LayoutSplitUtils.CloneText(text, remainderText);
                    remainderElement.KeepWithNext = text.KeepWithNext;
                    remainderElement.AvoidBreakInside = text.AvoidBreakInside;
                    remainderElement.MarginTop = text.MarginTop;
                    remainderElement.MarginBottom = text.MarginBottom;
                    remainder = new TextComponent(remainderElement, _defaultSpacing);
                }
                else if (renderLineCount <= 0)
                {
                    return LayoutMeasurement.Wrap(usedWidth);
                }
            }
            else if (!fitsFully)
            {
                return LayoutMeasurement.Wrap(usedWidth);
            }

            var renderLineList = shapedLines.Take(renderLineCount).ToList();
            float renderInnerHeight = SumLineHeights(renderLineList, renderLineCount);
            float renderMaxLineWidth = renderLineCount > 0 ? renderLineList.Max(l => l.Width) : 0f;
            float renderWidth = renderMaxLineWidth + paddingLeft + paddingRight;
            float renderFullHeight = renderInnerHeight + paddingTop + paddingBottom;

            float angleRad = text.Rotation * (float)(Math.PI / 180.0);
            float renderVerticalSpan = text.Rotation != 0f
                ? Math.Abs(renderFullHeight * (float)Math.Cos(angleRad)) + Math.Abs(renderWidth * (float)Math.Sin(angleRad))
                : renderFullHeight;
            float contentHeight = renderVerticalSpan;
            usedWidth = marginLeft + renderWidth + marginRight;

            string renderText = string.Join("\n", renderLineList.Select(l => l.Text));
            var renderParagraph = new ShapedParagraph(
                renderText,
                renderLineList,
                renderMaxLineWidth,
                renderInnerHeight);

            var metadata = new TextLayoutMetadata(
                marginLeft,
                marginRight,
                paddingLeft,
                paddingRight,
                paddingTop,
                paddingBottom,
                textMaxWidth,
                PrepareSegmentElement(text, renderText, renderParagraph, renderLineCount));

            return new LayoutMeasurement(
                marginTop,
                contentHeight,
                marginBottom,
                usedWidth,
                metadata,
                text.AvoidBreakInside,
                resultKind,
                remainder);
        }

        private static TextElement PrepareSegmentElement(TextElement source, string renderText, ShapedParagraph shapedParagraph, int renderLineCount)
        {
            var segment = LayoutSplitUtils.CloneText(source, renderText);
            segment.KeepWithNext = false;
            segment.ShapedLayout = shapedParagraph;
            segment.ShapedStartLine = 0;
            segment.ShapedLineCount = renderLineCount;
            return segment;
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not TextLayoutMetadata data)
                throw new InvalidOperationException("Text measurement metadata missing.");

            var element = data.Element;

            element.X = context.ContentLeft + data.MarginLeft + data.PaddingLeft;
            element.Y = context.ContentTop;
            element.MaxWidth = data.TextMaxWidth;
            element.PaddingLeft = data.PaddingLeft;
            element.PaddingRight = data.PaddingRight;
            element.PaddingTop = data.PaddingTop;
            element.PaddingBottom = data.PaddingBottom;

            context.Page.AddElement(element);
        }

        private static float SumLineHeights(IReadOnlyList<ShapedLine> lines, int count)
        {
            if (lines.Count == 0 || count <= 0)
                return 0f;

            int limit = Math.Min(count, lines.Count);
            float total = 0f;
            for (int i = 0; i < limit; i++)
                total += lines[i].LineHeight;
            return total;
        }

        private static int ComputeRenderableLineCount(IReadOnlyList<ShapedLine> lines, float availableHeight, float padding, int orphanLines, int widowLines)
        {
            float usableHeight = Math.Max(0f, availableHeight - padding);
            if (usableHeight <= 0f)
                return 0;

            orphanLines = Math.Max(1, orphanLines);
            widowLines = Math.Max(1, widowLines);

            float consumed = 0f;
            int render = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                float next = consumed + lines[i].LineHeight;
                if (next > usableHeight + 0.1f)
                    break;
                consumed = next;
                render++;
            }

            if (render == 0)
                return 0;

            if (render >= lines.Count)
                return lines.Count;

            if (render < orphanLines)
                return 0;

            if (lines.Count - render < widowLines)
                return 0;

            return render;
        }

        private sealed class TextLayoutMetadata
        {
            public TextLayoutMetadata(
                float marginLeft,
                float marginRight,
                float paddingLeft,
                float paddingRight,
                float paddingTop,
                float paddingBottom,
                float textMaxWidth,
                TextElement element)
            {
                MarginLeft = marginLeft;
                MarginRight = marginRight;
                PaddingLeft = paddingLeft;
                PaddingRight = paddingRight;
                PaddingTop = paddingTop;
                PaddingBottom = paddingBottom;
                TextMaxWidth = textMaxWidth;
                Element = element;
            }

            public float MarginLeft { get; }
            public float MarginRight { get; }
            public float PaddingLeft { get; }
            public float PaddingRight { get; }
            public float PaddingTop { get; }
            public float PaddingBottom { get; }
            public float TextMaxWidth { get; }
            public TextElement Element { get; }
        }
    }
}
