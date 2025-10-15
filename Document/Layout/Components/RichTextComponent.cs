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

        public RichTextComponent(RichTextElement element, float defaultSpacing)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _defaultSpacing = defaultSpacing;
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

            float baseWidth = rt.MaxWidth ?? Math.Max(0f, context.Column.Width - marginLeft - marginRight);
            float innerWidth = Math.Max(0f, baseWidth - paddingLeft - paddingRight);

            var layout = RichTextLayouter.Layout(rt, innerWidth);
            rt.ShapedLayout = layout;
            rt.ShapedLayoutWidth = innerWidth;
            rt.ShapedStartLine = 0;
            rt.ShapedLineCount = layout.Lines.Count;

            float contentHeight = paddingTop + layout.TotalHeight + paddingBottom;

            float usedWidth = marginLeft + marginRight + paddingLeft + paddingRight + Math.Min(baseWidth, context.Column.Width);
            float availableHeight = context.AvailableHeight - marginTop - marginBottom;

            if (availableHeight <= 0f || contentHeight > availableHeight + 0.1f)
                return LayoutMeasurement.Wrap(usedWidth);

            var metadata = new RichTextMetadata(
                marginLeft,
                paddingLeft,
                paddingRight,
                paddingTop,
                paddingBottom,
                innerWidth,
                layout);

            return new LayoutMeasurement(
                marginTop,
                contentHeight,
                marginBottom,
                usedWidth,
                metadata,
                rt.AvoidBreakInside);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not RichTextMetadata metadata)
                throw new InvalidOperationException("RichText measurement metadata missing.");

            var rt = _element;
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
            rt.ShapedStartLine = 0;
            rt.ShapedLineCount = metadata.Layout.Lines.Count;

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
                RichTextLayoutResult layout)
            {
                MarginLeft = marginLeft;
                PaddingLeft = paddingLeft;
                PaddingRight = paddingRight;
                PaddingTop = paddingTop;
                PaddingBottom = paddingBottom;
                InnerWidth = innerWidth;
                Layout = layout;
            }

            public float MarginLeft { get; }
            public float PaddingLeft { get; }
            public float PaddingRight { get; }
            public float PaddingTop { get; }
            public float PaddingBottom { get; }
            public float InnerWidth { get; }
            public RichTextLayoutResult Layout { get; }
        }
    }
}
