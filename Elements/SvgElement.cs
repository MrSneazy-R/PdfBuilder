using System;
using PdfBuilder.Writer.Imaging;

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

            ImageData = new SecureSvgRenderer().Render(_svgContent, Width, Height, _dpi);
            MimeType = "image/png";
        }
    }
}
