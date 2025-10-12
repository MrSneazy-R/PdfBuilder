using System.Collections.Generic;
using System.Drawing;

namespace PdfBuilder.Elements.Table
{
    public enum BorderCollapseMode
    {
        Separate,
        Collapse
    }

    public enum BorderLineJoin
    {
        Miter,
        Round,
        Bevel
    }

    public enum BorderLineCap
    {
        Butt,
        Round,
        Square
    }

    public sealed class BorderStyle
    {
        public Color Color { get; set; } = Color.Black;
        public float Width { get; set; } = 0.5f;

        public IReadOnlyList<float>? DashPattern
        {
            get => _dashPattern;
            set => _dashPattern = value == null ? null : new List<float>(value);
        }

        public float DashPhase { get; set; } = 0f;
        public BorderLineJoin LineJoin { get; set; } = BorderLineJoin.Miter;
        public BorderLineCap LineCap { get; set; } = BorderLineCap.Butt;
        public float? MiterLimit { get; set; } = null;

        private List<float>? _dashPattern;

        internal BorderStyle Clone() => new BorderStyle
        {
            Color = Color,
            Width = Width,
            DashPattern = _dashPattern == null ? null : new List<float>(_dashPattern),
            DashPhase = DashPhase,
            LineJoin = LineJoin,
            LineCap = LineCap,
            MiterLimit = MiterLimit
        };
    }
}
