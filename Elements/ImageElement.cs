// --- ImageElement.cs ---
using PdfBuilder.Document;
using PdfBuilder.Models;
using System;

namespace PdfBuilder.Elements
{
    public enum ImageClipShape
    {
        None,
        RoundedRect, // Only this + Circle, Ellipse (since QuestPDF can simulate circle/ellipse via border radius)
        Circle,
        Ellipse
    }

    public class ImageElement : PdfElement
    {
        // Required: Image Data (raw bytes, or image ID if deduplicating)
        public byte[] ImageData { get; set; }
        public string? MimeType { get; set; } // "image/png", "image/jpeg", etc.
        public string? ImageId { get; set; } // For deduplication or XObject naming

        // Placement & Sizing
        public float Width { get; set; }           // Rendered width in points
        public float Height { get; set; }          // Rendered height in points
        public float? MaxWidth { get; set; }       // Optional: Auto-scale if larger
        public float? MaxHeight { get; set; }
        public float Rotation { get; set; }        // Degrees
        public float Opacity { get; set; } = 1.0f;

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

        // Shadow
        public string? ShadowColor { get; set; }
        public float? ShadowOffsetX { get; set; }
        public float? ShadowOffsetY { get; set; }
        public float? ShadowBlur { get; set; }

        // Hyperlink (QuestPDF supports)
        public string? Hyperlink { get; set; } // Clickable

        // Internal PDF resource name
        public string? PdfResourceName { get; set; }

        public ImageElement(byte[] imageData, float x, float y, float width, float height) : base(x, y)
        {
            ImageData = imageData;
            Width = width;
            Height = height;
        }
    }
}
