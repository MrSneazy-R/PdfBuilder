using System;
using System.IO;
using System.Text;
using SkiaSharp;

namespace PdfBuilder.Elements
{
    /// <summary>
    /// Rasterizes SVG markup into an image element using SkiaSharp.Svg.
    /// </summary>
    public sealed class SvgElement : ImageElement
    {
        private string _svgContent;
        private float _dpi = 96f;

        public SvgElement(string svgContent, float x, float y, float width, float height)
            : base(Array.Empty<byte>(), x, y, Math.Max(0f, width), Math.Max(0f, height))
        {
            MimeType = "image/png";
            _svgContent = svgContent ?? string.Empty;
            Render();
        }

        public string SvgContent
        {
            get => _svgContent;
            set
            {
                _svgContent = value ?? string.Empty;
                Render();
            }
        }

        public float Dpi
        {
            get => _dpi;
            set
            {
                float normalized = Math.Max(1f, value);
                if (Math.Abs(_dpi - normalized) > 0.001f)
                {
                    _dpi = normalized;
                    Render();
                }
            }
        }

        public new float Width
        {
            get => base.Width;
            set
            {
                float normalized = Math.Max(0f, value);
                if (Math.Abs(base.Width - normalized) > 0.001f)
                {
                    base.Width = normalized;
                    Render();
                }
            }
        }

        public new float Height
        {
            get => base.Height;
            set
            {
                float normalized = Math.Max(0f, value);
                if (Math.Abs(base.Height - normalized) > 0.001f)
                {
                    base.Height = normalized;
                    Render();
                }
            }
        }

        public void Refresh() => Render();

        private void Render()
        {
            if (string.IsNullOrWhiteSpace(_svgContent) || Width <= 0f || Height <= 0f)
            {
                ImageData = Array.Empty<byte>();
                return;
            }

            var svg = new SkiaSharp.Extended.Svg.SKSvg();
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(_svgContent)))
            {
                svg.Load(stream);
            }

            var picture = svg.Picture ?? throw new InvalidOperationException("Unable to parse SVG content.");
            var bounds = svg.ViewBox;
            if (bounds.Width <= 0f || bounds.Height <= 0f)
                bounds = picture.CullRect;

            if (bounds.Width <= 0f || bounds.Height <= 0f)
                bounds = new SKRect(0, 0, Width, Height);

            int pixelWidth = Math.Max(1, (int)Math.Ceiling(Width * _dpi / 72f));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(Height * _dpi / 72f));

            var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            float scaleX = pixelWidth / bounds.Width;
            float scaleY = pixelHeight / bounds.Height;
            canvas.Scale(scaleX, scaleY);
            canvas.DrawPicture(picture);
            canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            ImageData = data.ToArray();
            MimeType = "image/png";
        }
    }
}
