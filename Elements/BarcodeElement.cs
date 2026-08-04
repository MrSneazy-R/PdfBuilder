using System;
using PdfBuilder.Imaging;

namespace PdfBuilder.Elements
{
    /// <summary>
    /// Vector barcode element rendered as a series of PDF path commands within a canvas.
    /// </summary>
    public sealed class BarcodeElement : CanvasElement
    {
        private string _value;
        private BarcodeKind _kind;
        private float _moduleSize;
        private int _quietZone;
        private string _foregroundColor;
        private string? _backgroundColor;

        public BarcodeElement(string value, BarcodeKind kind, float moduleSize = 2f, int quietZone = 4)
            : base(0f, 0f, 0f, 0f)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Barcode value cannot be null or empty.", nameof(value));

            _value = value;
            _kind = kind;
            if (moduleSize <= 0f || float.IsNaN(moduleSize) || float.IsInfinity(moduleSize))
                throw new ArgumentOutOfRangeException(nameof(moduleSize), "Barcode module size must be a positive finite value.");
            if (quietZone < 0)
                throw new ArgumentOutOfRangeException(nameof(quietZone), "Barcode quiet zone cannot be negative.");
            _moduleSize = moduleSize;
            _quietZone = quietZone;
            _foregroundColor = "#000000";
            RebuildCommands();
        }

        public string Value
        {
            get => _value;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Barcode value cannot be null or empty.", nameof(value));
                if (!string.Equals(_value, value, StringComparison.Ordinal))
                {
                    _value = value;
                    RebuildCommands();
                }
            }
        }

        public BarcodeKind Kind
        {
            get => _kind;
            set
            {
                if (_kind != value)
                {
                    _kind = value;
                    RebuildCommands();
                }
            }
        }

        public float ModuleSize
        {
            get => _moduleSize;
            set
            {
                if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value), "Barcode module size must be a positive finite value.");
                float normalized = value;
                if (Math.Abs(_moduleSize - normalized) > 0.001f)
                {
                    _moduleSize = normalized;
                    RebuildCommands();
                }
            }
        }

        public int QuietZone
        {
            get => _quietZone;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Barcode quiet zone cannot be negative.");
                int normalized = value;
                if (_quietZone != normalized)
                {
                    _quietZone = normalized;
                    RebuildCommands();
                }
            }
        }

        public string ForegroundColor
        {
            get => _foregroundColor;
            set
            {
                var color = string.IsNullOrWhiteSpace(value) ? "#000000" : value!;
                if (!string.Equals(_foregroundColor, color, StringComparison.OrdinalIgnoreCase))
                {
                    _foregroundColor = color;
                    RebuildCommands();
                }
            }
        }

        public string? BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (!string.Equals(_backgroundColor, value, StringComparison.OrdinalIgnoreCase))
                {
                    _backgroundColor = value;
                    RebuildCommands();
                }
            }
        }

        public void Refresh() => RebuildCommands();

        private void RebuildCommands()
        {
            var geometry = BarcodePathBuilder.Build(_value, _kind, _moduleSize, _quietZone, _foregroundColor, _backgroundColor);
            Width = geometry.Width;
            Height = geometry.Height;
            Commands.Clear();
            if (!string.IsNullOrEmpty(geometry.BackgroundCommand))
                Commands.Add(geometry.BackgroundCommand);
            Commands.Add(geometry.PathCommands);
        }
    }
}
