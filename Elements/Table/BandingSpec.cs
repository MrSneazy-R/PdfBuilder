using System.Collections.Generic;
using System.Drawing;

namespace PdfBuilder.Elements.Table
{
    public sealed class BandFill
    {
        public Color? FillColor { get; set; }
        public BorderStyle? BorderOverride { get; set; }

        internal BandFill Clone() => new BandFill
        {
            FillColor = FillColor,
            BorderOverride = BorderOverride?.Clone()
        };
    }

    public sealed class RowBandingSpec
    {
        public int Step { get; set; } = 2;
        public List<BandFill> Fills { get; set; } = new();
        public BorderStyle? BorderOverride { get; set; }

        internal RowBandingSpec Clone()
        {
            return new RowBandingSpec
            {
                Step = Step,
                Fills = CloneList(Fills),
                BorderOverride = BorderOverride?.Clone()
            };
        }

        private static List<BandFill> CloneList(List<BandFill> fills)
        {
            var clone = new List<BandFill>(fills.Count);
            foreach (var f in fills)
                clone.Add(f.Clone());
            return clone;
        }
    }

    public sealed class ColumnBandingSpec
    {
        public int Step { get; set; } = 2;
        public List<BandFill> Fills { get; set; } = new();
        public BorderStyle? BorderOverride { get; set; }

        internal ColumnBandingSpec Clone()
        {
            return new ColumnBandingSpec
            {
                Step = Step,
                Fills = CloneList(Fills),
                BorderOverride = BorderOverride?.Clone()
            };
        }

        private static List<BandFill> CloneList(List<BandFill> fills)
        {
            var clone = new List<BandFill>(fills.Count);
            foreach (var f in fills)
                clone.Add(f.Clone());
            return clone;
        }
    }
}
