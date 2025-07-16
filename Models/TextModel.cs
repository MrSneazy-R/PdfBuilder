using System;

namespace PdfBuilder.Models
{
    public class TextStyle
    {
        // Position
        public float X { get; set; } = 100;
        public float Y { get; set; } = 700;

        // Font & Styling
        public float FontSize { get; set; } = 12;
        public string FontFamily { get; set; } = "Helvetica";
        public bool Bold { get; set; } = false;
        public bool Italic { get; set; } = false;
        public bool Underline { get; set; } = false;

        // Colors
        public string Color { get; set; } = "#000000";
        public string? BackgroundColor { get; set; } = null;

        // Text alignment & layout
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;
        public float? MaxWidth { get; set; } = null;
        public float LineHeight { get; set; } = 1.2f;

        // Other
        public float Opacity { get; set; } = 1.0f; // For future ExtGState
        public float Rotation { get; set; } = 0f;  // Degrees
        public float? MarginTop { get; set; }
    }

    public enum TextAlignment
    {
        Left,
        Center,
        Right,
        Justify
    }
}
