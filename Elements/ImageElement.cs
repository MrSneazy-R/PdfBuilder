// --- ImageElement.cs ---
using System;
using PdfBuilder.Document;
using PdfBuilder.Models;

namespace PdfBuilder.Elements
{
    public enum ImageClipShape
    {
        None,
        RoundedRect, // Only this + Circle, Ellipse (since QuestPDF can simulate circle/ellipse via border radius)
        Circle,
        Ellipse
    }

    /// <summary>Specifies how an image is placed inside its allocated layout box.</summary>
    internal enum ImageFit
    {
        /// <summary>Stretches the source to fill the allocated box.</summary>
        Stretch,
        /// <summary>Preserves aspect ratio and fits the entire image inside the box.</summary>
        Contain,
        /// <summary>Preserves aspect ratio and fills the box, cropping overflow.</summary>
        Cover,
        /// <summary>Uses the intrinsic image size at its declared DPI where available.</summary>
        Original
    }

    /// <summary>Specifies alignment of an aspect-ratio-preserving image inside its allocated box.</summary>
    internal enum ImageAlignment
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Center,
        Right,
        BottomLeft,
        Bottom,
        BottomRight
    }
    public enum EllipseOrientation
    {
        Horizontal, // major axis along X (default)
        Vertical    // major axis along Y
    }
    public class ImageElement : PdfElement
    {
        // Required: Image Data (raw bytes, or image ID if deduplicating)
        public byte[] ImageData { get; set; }
        internal ImageSource? Source { get; set; }
        public string? MimeType { get; set; } // "image/png", "image/jpeg", etc.
        public string? ImageId { get; set; } // For deduplication or XObject naming

        // Placement & Sizing
        public float Width { get; set; }           // Rendered width in points
        public float Height { get; set; }          // Rendered height in points
        public float? MaxWidth { get; set; }       // Optional: Auto-scale if larger
        public float? MaxHeight { get; set; }
        public float Rotation { get; set; }        // Degrees
        public float Opacity { get; set; } = 1.0f;
        internal ImageFit Fit { get; set; } = ImageFit.Stretch;
        internal ImageAlignment Alignment { get; set; } = ImageAlignment.Center;
        internal ImageQuality Quality { get; set; } = ImageQuality.High;
        internal bool Downsample { get; set; }
        internal float MaximumEffectiveDpi { get; set; } = 300f;
        internal int JpegQuality { get; set; } = 85;
        internal bool AlphaAwareEncoding { get; set; } = true;
        internal bool UseIntrinsicDimensions { get; set; }

        // Padding & Margin
        public float? MarginTop { get; set; }
        public float? MarginBottom { get; set; }
        public float? MarginLeft { get; set; }
        public float? MarginRight { get; set; }
        public float? PaddingTop { get; set; }
        public float? PaddingBottom { get; set; }
        public float? PaddingLeft { get; set; }
        public float? PaddingRight { get; set; }

        // Border & Shape
        public string? BorderColor { get; set; }
        public float? BorderWidth { get; set; }
        public float? CornerRadius { get; set; }      // For rounded/circle/ellipse
        public ImageClipShape ClipShape { get; set; } = ImageClipShape.None;

        public EllipseOrientation EllipseOrientation { get; set; } = EllipseOrientation.Horizontal;

        // Optional: shrink the minor axis (1 = inscribed ellipse; <1 makes it "more oval")
        public float EllipseSquash { get; set; } = 1f;
        // Shadow
        public string? ShadowColor { get; set; }
        public float? ShadowOffsetX { get; set; }
        public float? ShadowOffsetY { get; set; }
        public float? ShadowBlur { get; set; }

        // Hyperlink (QuestPDF supports)
        public string? Hyperlink { get; set; } // Clickable

        // Internal PDF resource name
        public string? PdfResourceName { get; set; }

        internal int SourcePixelWidth { get; set; }
        internal int SourcePixelHeight { get; set; }
        internal float SourceDpiX { get; set; } = 96f;
        internal float SourceDpiY { get; set; } = 96f;

        public ImageElement(byte[] imageData, float x, float y, float width, float height) : base(x, y)
        {
            ImageData = imageData;
            Width = width;
            Height = height;
        }

        internal ImageElement(ImageSource source, float x, float y, float width, float height) : base(x, y)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ImageData = Array.Empty<byte>();
            Width = width;
            Height = height;
        }

        internal byte[] ResolveImageData() => Source?.GetBytes() ?? ImageData;

        public bool KeepWithNext { get; set; } = false;
        public bool AvoidBreakInside { get; set; } = true;
    }
}
