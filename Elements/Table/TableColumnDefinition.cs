namespace PdfBuilder.Elements.Table
{
    public enum TableColumnWidthMode
    {
        Auto,
        Fixed,
        Relative
    }

    public sealed class TableColumnDefinition
    {
        public TableColumnWidthMode Mode { get; init; } = TableColumnWidthMode.Auto;
        public float Value { get; init; }
        public float? MinWidth { get; init; }
        public float? MaxWidth { get; init; }

        internal TableColumnDefinition Clone() => new TableColumnDefinition
        {
            Mode = Mode,
            Value = Value,
            MinWidth = MinWidth,
            MaxWidth = MaxWidth
        };
    }

    public static class TableColumn
    {
        public static TableColumnDefinition Auto(float? minWidth = null, float? maxWidth = null) =>
            new TableColumnDefinition
            {
                Mode = TableColumnWidthMode.Auto,
                MinWidth = minWidth,
                MaxWidth = maxWidth
            };

        public static TableColumnDefinition Fixed(float width, float? minWidth = null, float? maxWidth = null) =>
            new TableColumnDefinition
            {
                Mode = TableColumnWidthMode.Fixed,
                Value = width,
                MinWidth = minWidth,
                MaxWidth = maxWidth
            };

        public static TableColumnDefinition Relative(float weight, float? minWidth = null, float? maxWidth = null) =>
            new TableColumnDefinition
            {
                Mode = TableColumnWidthMode.Relative,
                Value = weight,
                MinWidth = minWidth,
                MaxWidth = maxWidth
            };
    }
}
