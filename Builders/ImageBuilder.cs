// --- ImageBuilder.cs ---
using PdfBuilder.Elements;
using PdfBuilder.Models;
using System;

namespace PdfBuilder.Document
{
    public class ImageBuilder
    {
        private readonly ColumnBuilder _column;
        private readonly ImageElement _img;

        public ImageBuilder(ColumnBuilder column, byte[] imageData, float x, float y, float width, float height)
        {
            _column = column;
            _img = new ImageElement(imageData, x, y, width, height);
        }

        // Placement
        public ImageBuilder X(float x) { _img.X = x; return this; }
        public ImageBuilder Y(float y) { _img.Y = y; return this; }
        public ImageBuilder Width(float width) { _img.Width = width; return this; }
        public ImageBuilder Height(float height) { _img.Height = height; return this; }
        public ImageBuilder MaxWidth(float maxWidth) { _img.MaxWidth = maxWidth; return this; }
        public ImageBuilder MaxHeight(float maxHeight) { _img.MaxHeight = maxHeight; return this; }
        public ImageBuilder Rotation(float degrees) { _img.Rotation = degrees; return this; }
        public ImageBuilder Opacity(float opacity) { _img.Opacity = opacity; return this; }

        // Margins & Padding (all, vertical/horizontal, individual)
        public ImageBuilder Margin(float all) => Margin(all, all, all, all);
        public ImageBuilder Margin(float top, float bottom, float left, float right)
        { _img.MarginTop = top; _img.MarginBottom = bottom; _img.MarginLeft = left; _img.MarginRight = right; return this; }
        public ImageBuilder MarginTop(float v) { _img.MarginTop = v; return this; }
        public ImageBuilder MarginBottom(float v) { _img.MarginBottom = v; return this; }
        public ImageBuilder MarginLeft(float v) { _img.MarginLeft = v; return this; }
        public ImageBuilder MarginRight(float v) { _img.MarginRight = v; return this; }

        public ImageBuilder Padding(float all) => Padding(all, all, all, all);
        public ImageBuilder Padding(float top, float bottom, float left, float right)
        { _img.PaddingTop = top; _img.PaddingBottom = bottom; _img.PaddingLeft = left; _img.PaddingRight = right; return this; }
        public ImageBuilder PaddingTop(float v) { _img.PaddingTop = v; return this; }
        public ImageBuilder PaddingBottom(float v) { _img.PaddingBottom = v; return this; }
        public ImageBuilder PaddingLeft(float v) { _img.PaddingLeft = v; return this; }
        public ImageBuilder PaddingRight(float v) { _img.PaddingRight = v; return this; }

        // Border & Shape
        public ImageBuilder Border(string color, float width)
        { _img.BorderColor = color; _img.BorderWidth = width; return this; }
        public ImageBuilder BorderColor(string color) { _img.BorderColor = color; return this; }
        public ImageBuilder BorderWidth(float width) { _img.BorderWidth = width; return this; }
        public ImageBuilder CornerRadius(float radius) { _img.CornerRadius = radius; return this; }
        public ImageBuilder Clip(ImageClipShape shape)
        { _img.ClipShape = shape; return this; }

        // Shadow
        public ImageBuilder Shadow(string color, float offsetX, float offsetY, float? blur = null)
        { _img.ShadowColor = color; _img.ShadowOffsetX = offsetX; _img.ShadowOffsetY = offsetY; _img.ShadowBlur = blur; return this; }
        public ImageBuilder ShadowColor(string color) { _img.ShadowColor = color; return this; }
        public ImageBuilder ShadowOffsetX(float v) { _img.ShadowOffsetX = v; return this; }
        public ImageBuilder ShadowOffsetY(float v) { _img.ShadowOffsetY = v; return this; }
        public ImageBuilder ShadowBlur(float v) { _img.ShadowBlur = v; return this; }

        // Hyperlink (QuestPDF supports this)
        public ImageBuilder Hyperlink(string url) { _img.Hyperlink = url; return this; }

        // Image Metadata
        public ImageBuilder MimeType(string mime) { _img.MimeType = mime; return this; }
        public ImageBuilder ImageId(string id) { _img.ImageId = id; return this; }
        public ImageBuilder FitToMinDpi(float minDpi = 180f)
        {
            // read JPEG dimensions (tiny SOF parser)
            static (int w, int h) JpegSize(byte[] data)
            {
                if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return (1, 1);
                for (int i = 2; i + 9 < data.Length;)
                {
                    if (data[i] != 0xFF) { i++; continue; }
                    byte marker = data[i + 1];
                    int len = (data[i + 2] << 8) + data[i + 3];
                    if (marker == 0xC0 || marker == 0xC2)
                    {
                        int h = (data[i + 5] << 8) + data[i + 6];
                        int w = (data[i + 7] << 8) + data[i + 8];
                        return (Math.Max(1, w), Math.Max(1, h));
                    }
                    i += 2 + len;
                }
                return (1, 1);
            }

            var (pxW, pxH) = JpegSize(_img.ImageData);
            // current effective DPI at requested size
            float dpiW = pxW * 72f / _img.Width;
            float dpiH = pxH * 72f / _img.Height;

            // scale down if either axis is below the minimum
            float scale = Math.Min(1f, Math.Min(dpiW / minDpi, dpiH / minDpi));
            if (scale < 1f)
            {
                _img.Width *= scale;
                _img.Height *= scale;
            }
            return this;
        }
        // Overload with orientation (used when shape == Ellipse)
        public ImageBuilder Clip(ImageClipShape shape, EllipseOrientation orientation)
        { _img.ClipShape = shape; _img.EllipseOrientation = orientation; return this; }

        // Convenience for ellipse + optional squash (0.1–1.0)
        public ImageBuilder ClipEllipse(EllipseOrientation orientation, float squash = 1f)
        { _img.ClipShape = ImageClipShape.Ellipse; _img.EllipseOrientation = orientation; _img.EllipseSquash = Math.Max(0.01f, squash); return this; }
        // Add to the column and finish
        public float Add()
        {
            float height = _column.AddImage(_img);
            var flow = _column.GetFlow();
            if (!flow.CanFit(height))
                flow.Advance(height);
            else
                flow.Reserve(height);

            return height;
        }
    }
}
