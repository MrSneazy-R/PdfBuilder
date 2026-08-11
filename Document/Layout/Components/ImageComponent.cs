using System;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class ImageComponent : IMeasurable
    {
        private readonly ImageElement _element;
        private readonly float _defaultSpacing;

        public ImageComponent(ImageElement element, float defaultSpacing)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _defaultSpacing = defaultSpacing;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            float marginTop = _element.MarginTop ?? _defaultSpacing;
            float marginBottom = _element.MarginBottom ?? 0f;
            float marginLeft = _element.MarginLeft ?? 0f;
            float marginRight = _element.MarginRight ?? 0f;

            float paddingTop = _element.PaddingTop ?? 0f;
            float paddingBottom = _element.PaddingBottom ?? 0f;
            float paddingLeft = _element.PaddingLeft ?? 0f;
            float paddingRight = _element.PaddingRight ?? 0f;

            float imageWidth = _element.Width;
            float imageHeight = _element.Height;
            if (_element.UseIntrinsicDimensions && _element.Source != null)
            {
                ImageSourceInfo info = _element.Source.Inspect();
                imageWidth = info.OriginalWidthPoints;
                imageHeight = info.OriginalHeightPoints;
            }

            if (_element.MaxWidth.HasValue && imageWidth > _element.MaxWidth.Value && imageWidth > 0f)
            {
                float scale = _element.MaxWidth.Value / imageWidth;
                imageWidth = _element.MaxWidth.Value;
                imageHeight *= scale;
            }

            if (_element.MaxHeight.HasValue && imageHeight > _element.MaxHeight.Value && imageHeight > 0f)
            {
                float scale = _element.MaxHeight.Value / imageHeight;
                imageHeight = _element.MaxHeight.Value;
                imageWidth *= scale;
            }

            float blockWidth = imageWidth + paddingLeft + paddingRight;
            float blockHeight = imageHeight + paddingTop + paddingBottom;

            double theta = _element.Rotation * Math.PI / 180.0;
            float s = (float)Math.Sin(theta);
            float c = (float)Math.Cos(theta);

            float rotatedHeight = _element.Rotation != 0f
                ? Math.Abs(blockHeight * c) + Math.Abs(blockWidth * s)
                : blockHeight;

            float extraShadowY = 0f;
            float shadowUp = 0f;
            if (!string.IsNullOrWhiteSpace(_element.ShadowColor) &&
                ((_element.ShadowOffsetX ?? 0f) != 0f || (_element.ShadowOffsetY ?? 0f) != 0f))
            {
                float sox = _element.ShadowOffsetX ?? 0f;
                float soy = _element.ShadowOffsetY ?? 0f;
                float yPrime = s * sox - c * soy;
                extraShadowY = Math.Abs(yPrime);
                shadowUp = Math.Max(0f, yPrime);
            }

            float halfW = blockWidth * 0.5f;
            float halfH = blockHeight * 0.5f;
            float rotatedHalfH = Math.Abs(halfH * c) + Math.Abs(halfW * s);
            float overhangTop = Math.Max(0f, rotatedHalfH - halfH);

            float verticalSpan = rotatedHeight + extraShadowY;
            float contentHeight = overhangTop + shadowUp + verticalSpan;

            float usedWidth = marginLeft + blockWidth + marginRight;
            float availableHeight = context.AvailableHeight - marginTop - marginBottom;

            if (availableHeight <= 0f || contentHeight > availableHeight + 0.1f)
            {
                return LayoutMeasurement.Wrap(usedWidth);
            }

            var metadata = new ImageMetadata(
                marginLeft,
                paddingLeft,
                paddingRight,
                paddingTop,
                paddingBottom,
                imageWidth,
                imageHeight,
                overhangTop,
                shadowUp);

            return new LayoutMeasurement(
                marginTop,
                contentHeight,
                marginBottom,
                usedWidth,
                metadata,
                _element.AvoidBreakInside);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not ImageMetadata meta)
                throw new InvalidOperationException("Image measurement metadata missing.");

            var image = _element;
            image.X = context.ContentLeft + meta.MarginLeft + meta.PaddingLeft;
            image.Y = context.ContentTop - meta.OverhangTop - meta.ShadowUp;
            image.Width = meta.ImageWidth;
            image.Height = meta.ImageHeight;
            image.PaddingLeft = meta.PaddingLeft;
            image.PaddingRight = meta.PaddingRight;
            image.PaddingTop = meta.PaddingTop;
            image.PaddingBottom = meta.PaddingBottom;

            context.Page.AddElement(image);
        }

        private sealed class ImageMetadata
        {
            public ImageMetadata(
                float marginLeft,
                float paddingLeft,
                float paddingRight,
                float paddingTop,
                float paddingBottom,
                float imageWidth,
                float imageHeight,
                float overhangTop,
                float shadowUp)
            {
                MarginLeft = marginLeft;
                PaddingLeft = paddingLeft;
                PaddingRight = paddingRight;
                PaddingTop = paddingTop;
                PaddingBottom = paddingBottom;
                ImageWidth = imageWidth;
                ImageHeight = imageHeight;
                OverhangTop = overhangTop;
                ShadowUp = shadowUp;
            }

            public float MarginLeft { get; }
            public float PaddingLeft { get; }
            public float PaddingRight { get; }
            public float PaddingTop { get; }
            public float PaddingBottom { get; }
            public float ImageWidth { get; }
            public float ImageHeight { get; }
            public float OverhangTop { get; }
            public float ShadowUp { get; }
        }
    }
}
