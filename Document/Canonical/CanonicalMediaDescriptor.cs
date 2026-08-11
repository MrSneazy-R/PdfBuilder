using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalImageDescriptor : IImageDescriptor
    {
        private ImageFit _fit = ImageFit.Contain;
        private ImageAlignment _alignment = ImageAlignment.Center;
        private float _opacity = 1f;
        private float? _borderWidth;
        private PdfColor _borderColor = PdfColor.Rgb(0, 0, 0);
        private float? _cornerRadius;
        private bool _circle;

        public IImageDescriptor Contain() { _fit = ImageFit.Contain; return this; }
        public IImageDescriptor Cover() { _fit = ImageFit.Cover; return this; }
        public IImageDescriptor Stretch() { _fit = ImageFit.Stretch; return this; }
        public IImageDescriptor OriginalSize() { _fit = ImageFit.Original; return this; }
        public IImageDescriptor AlignCenter() { _alignment = ImageAlignment.Center; return this; }
        public IImageDescriptor Opacity(float value) { if (value < 0f || value > 1f || float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value)); _opacity = value; return this; }
        public IImageDescriptor Border(float width = 1f, PdfColor? color = null) { if (width < 0f || float.IsNaN(width)) throw new ArgumentOutOfRangeException(nameof(width)); _borderWidth = width; _borderColor = color ?? PdfColor.Rgb(0, 0, 0); return this; }
        public IImageDescriptor CornerRadius(float value) { if (value < 0f || float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value)); _cornerRadius = value; return this; }
        public IImageDescriptor Circle() { _circle = true; return this; }
        public void Apply(ImageElement image)
        {
            image.Fit = _fit;
            image.Alignment = _alignment;
            image.Opacity = _opacity;
            image.BorderWidth = _borderWidth;
            image.BorderColor = _borderColor.ToString();
            image.CornerRadius = _cornerRadius;
            image.ClipShape = _circle ? ImageClipShape.Circle : ImageClipShape.None;
        }
    }
}
