using System;
using System.Drawing;

using PdfBuilder.Document;

namespace PdfBuilder.Models
{
    public sealed class HeaderFooterSpec
    {
        // Text templates (tokens supported: {page}, {pages}, {title}, {date:...}, {time:...})
        public string? HeaderTemplate { get; set; } = null;
        public string? FooterTemplate { get; set; } = null;

        // Layout (space reserved in content area)
        public float HeaderHeight { get; set; } = 28f;
        public float FooterHeight { get; set; } = 28f;

        // Typographic defaults
        public string FontFamily { get; set; } = "Helvetica";
        public float FontSize { get; set; } = 9f;
        public string Color { get; set; } = "#555555";

        // Alignment within the page box (respecting page margins)
        public TextAlignment HeaderAlign { get; set; } = TextAlignment.Left;
        public TextAlignment FooterAlign { get; set; } = TextAlignment.Right;

        // Behavior
        public bool FirstPageDifferent { get; set; } = false;
        public string? FirstPageHeaderTemplate { get; set; } = null;
        public string? FirstPageFooterTemplate { get; set; } = null;

        public bool HideOnLastPage { get; set; } = false;

        public HeaderFooterLayoutDefinition? HeaderLayout { get; set; }

        public HeaderFooterLayoutDefinition? FooterLayout { get; set; }

        internal List<PageVisibilityRule>? HeaderVisibilityRules { get; set; }
        internal List<PageVisibilityRule>? FooterVisibilityRules { get; set; }

        internal bool IsHeaderVisible(int currentPage, int totalPages)
        {
            if (HeaderVisibilityRules != null)
                return HeaderVisibilityRules.Any(rule => rule.Matches(currentPage, totalPages));
            return !(FirstPageDifferent && currentPage == 1);
        }

        internal bool IsFooterVisible(int currentPage, int totalPages)
        {
            if (HideOnLastPage && currentPage == totalPages)
                return false;
            if (FooterVisibilityRules != null)
                return FooterVisibilityRules.Any(rule => rule.Matches(currentPage, totalPages));
            return !(FirstPageDifferent && currentPage == 1);
        }
    }

    public enum WatermarkLayer { BehindContent, AboveContent }

    public sealed class WatermarkSpec
    {
        // Choose one: Text OR Image (if both present, both are drawn)
        public string? Text { get; set; }
        public byte[]? ImageData { get; set; }   // e.g., PNG/JPEG bytes
        public string? ImageMime { get; set; }

        // Placement & style
        public float X { get; set; } = 0;     // if 0 + Center = true, it's auto-centered
        public float Y { get; set; } = 0;
        public bool CenterOnPage { get; set; } = true;

        // Text styles
        public string FontFamily { get; set; } = "Helvetica";
        public float FontSize { get; set; } = 80f;
        public string Color { get; set; } = "#000000";
        public float Opacity { get; set; } = 0.08f;  // note: real alpha needs ExtGState; we’ll simulate for now
        public float RotationDegrees { get; set; } = 45f;

        // Image sizing (points)
        public float ImageWidth { get; set; } = 300f;
        public float ImageHeight { get; set; } = 300f;

        public WatermarkLayer Layer { get; set; } = WatermarkLayer.BehindContent;

        internal string? ExtGStateResourceName { get; set; }
    }

    public sealed class MasterPageSpec
    {
        // Global background (page fill)
        public string? BackgroundColor { get; set; } = null; // e.g., "#F8F8F8"

        // Optional background image (full-page or specific size — simple first pass)
        public byte[]? BackgroundImage { get; set; }
        public string? BackgroundImageMime { get; set; }
        public float BackgroundImageX { get; set; } = 0f;
        public float BackgroundImageY { get; set; } = 0f;
        public float? BackgroundImageWidth { get; set; } = null;   // null = use image's natural size mapping
        public float? BackgroundImageHeight { get; set; } = null;

        // Optional watermark
        public WatermarkSpec? Watermark { get; set; }
    }
}
