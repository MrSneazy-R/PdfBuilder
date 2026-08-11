using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Document.TextShaping;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class RichTextComponent : IMeasurable
    {
        private readonly RichTextElement _element;
        private readonly float _defaultSpacing;
        private readonly int _startLine;

        public RichTextComponent(RichTextElement element, float defaultSpacing, int startLine = 0)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _defaultSpacing = defaultSpacing;
            _startLine = Math.Max(0, startLine);
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            var rt = _element;

            float marginTop = rt.MarginTop ?? _defaultSpacing;
            float marginBottom = rt.MarginBottom ?? 0f;
            float marginLeft = rt.MarginLeft ?? 0f;
            float marginRight = rt.MarginRight ?? 0f;

            float paddingTop = rt.PaddingTop ?? 0f;
            float paddingBottom = rt.PaddingBottom ?? 0f;
            float paddingLeft = rt.PaddingLeft ?? 0f;
            float paddingRight = rt.PaddingRight ?? 0f;

            float availableWidth = Math.Max(0f, context.Column.Width - marginLeft - marginRight);
            float baseWidth = rt.MaxWidth.HasValue ? Math.Min(rt.MaxWidth.Value, availableWidth) : availableWidth;
            float innerWidth = Math.Max(0f, baseWidth - paddingLeft - paddingRight);

            var layout = RichTextLayouter.Layout(rt, innerWidth);
            rt.ShapedLayout = layout;
            rt.ShapedLayoutWidth = innerWidth;
            int startLine = Math.Min(_startLine, Math.Max(0, layout.Lines.Count - 1));
            int remainingLines = Math.Max(0, layout.Lines.Count - startLine);

            float remainingHeight = layout.Lines.Skip(startLine).Sum(line => line.LineHeight);
            float contentHeight = paddingTop + remainingHeight + paddingBottom;

            float usedWidth = marginLeft + marginRight + paddingLeft + paddingRight + Math.Min(baseWidth, context.Column.Width);
            float availableHeight = context.AvailableHeight - marginTop - marginBottom;

            if (availableHeight <= 0f)
                return LayoutMeasurement.Wrap(usedWidth);

            int renderLineCount = remainingLines;
            LayoutResultKind resultKind = LayoutResultKind.Full;
            IMeasurable? remainder = null;
            if (contentHeight > availableHeight + 0.1f)
            {
                if (rt.AvoidBreakInside)
                    return LayoutMeasurement.Wrap(usedWidth);
                float usableHeight = Math.Max(0f, availableHeight - paddingTop - paddingBottom);
                float consumed = 0f;
                renderLineCount = 0;
                foreach (var line in layout.Lines.Skip(startLine))
                {
                    if (consumed + line.LineHeight > usableHeight + 0.1f) break;
                    consumed += line.LineHeight;
                    renderLineCount++;
                }
                if (renderLineCount == 0) return LayoutMeasurement.Wrap(usedWidth);
                if (renderLineCount < remainingLines)
                {
                    resultKind = LayoutResultKind.Partial;
                    remainder = new RichTextComponent(LayoutSplitUtils.CloneRichText(rt), _defaultSpacing, startLine + renderLineCount);
                }
                contentHeight = paddingTop + consumed + paddingBottom;
            }

            var metadata = new RichTextMetadata(
                marginLeft,
                paddingLeft,
                paddingRight,
                paddingTop,
                paddingBottom,
                innerWidth,
                layout,
                startLine,
                renderLineCount,
                LayoutSplitUtils.CloneRichText(rt));

            return new LayoutMeasurement(
                marginTop,
                contentHeight,
                marginBottom,
                usedWidth,
                metadata,
                rt.AvoidBreakInside,
                resultKind,
                remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not RichTextMetadata metadata)
                throw new InvalidOperationException("RichText measurement metadata missing.");

            var rt = metadata.Element;
            float contentLeft = context.ContentLeft + metadata.MarginLeft + metadata.PaddingLeft;

            rt.X = contentLeft;
            rt.Y = context.ContentTop;
            rt.MaxWidth ??= metadata.InnerWidth + metadata.PaddingLeft + metadata.PaddingRight;
            rt.PaddingLeft = metadata.PaddingLeft;
            rt.PaddingRight = metadata.PaddingRight;
            rt.PaddingTop = metadata.PaddingTop;
            rt.PaddingBottom = metadata.PaddingBottom;
            rt.ShapedLayout = metadata.Layout;
            rt.ShapedLayoutWidth = metadata.InnerWidth;
            rt.ShapedStartLine = metadata.StartLine;
            rt.ShapedLineCount = metadata.LineCount;

            context.Page.AddElement(rt);
        }

        private sealed class RichTextMetadata
        {
            public RichTextMetadata(
                float marginLeft,
                float paddingLeft,
                float paddingRight,
                float paddingTop,
                float paddingBottom,
                float innerWidth,
                RichTextLayoutResult layout,
                int startLine,
                int lineCount,
                RichTextElement element)
            {
                MarginLeft = marginLeft;
                PaddingLeft = paddingLeft;
                PaddingRight = paddingRight;
                PaddingTop = paddingTop;
                PaddingBottom = paddingBottom;
                InnerWidth = innerWidth;
                Layout = layout;
                StartLine = startLine;
                LineCount = lineCount;
                Element = element;
            }

            public float MarginLeft { get; }
            public float PaddingLeft { get; }
            public float PaddingRight { get; }
            public float PaddingTop { get; }
            public float PaddingBottom { get; }
            public float InnerWidth { get; }
            public RichTextLayoutResult Layout { get; }
            public int StartLine { get; }
            public int LineCount { get; }
            public RichTextElement Element { get; }
        }
    }
}
