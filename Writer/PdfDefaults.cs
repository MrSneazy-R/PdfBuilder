using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfBuilder.Writer
{
    public static class PdfDefaults
    {
        public const float DefaultPadding = 4f;
        public const float DefaultCellSpacing = 2f;
        public const float DefaultBorderWidth = 0.5f;
        public const float PageMargin = 40f;
        public const float LineHeightMultiplier = 1.2f;

        public static readonly Color HeaderBackground = Color.LightGray;
        public static readonly Color AltRowBackground = Color.FromArgb(240, 240, 240);

        public enum BorderConflictPolicy
        {
            CollapsedClassic // CSS-like: thicker wins; ties -> bottom over top, right over left
        }
    }
}
