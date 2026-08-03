namespace PdfBuilder.Models
{
    public sealed class ColumnLayoutSpec
    {
        // Simple: N equal columns with a fixed gutter.
        public int Columns { get; set; } = 1;
        public float Gutter { get; set; } = 14f;

        // Optional custom widths (sum must be <= page content width). If provided, Columns = widths.Length.
        public float[]? Widths { get; set; }
    }
}
