using System.Collections.Generic;

namespace PdfBuilder.Models
{
    public sealed class RichRun
    {
        public string Text { get; set; } = "";

        // Inline style
        public string FontFamily { get; set; } = "Helvetica";
        public float? FontSize { get; set; }             // null = inherit paragraph
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Strikethrough { get; set; }
        public bool SmallCaps { get; set; }
        public string Color { get; set; } = "#000";

        // Links (choose one)
        public string? LinkUrl { get; set; }             // "https://", "mailto:..."
        public string? LinkAnchor { get; set; }          // internal anchor id
    }

    public enum ListMarker
    {
        Bullet, Decimal, LowerAlpha, UpperAlpha, LowerRoman, UpperRoman
    }

    public sealed class ListItem
    {
        public List<RichRun> Content { get; set; } = new();
        public List<ListItem> Children { get; set; } = new();
    }
}
