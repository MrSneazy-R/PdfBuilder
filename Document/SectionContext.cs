namespace PdfBuilder.Document
{
    public sealed class SectionContext
    {
        internal SectionContext(ColumnBuilder column, string title, string number, string anchorId, int level)
        {
            Column = column;
            Title = title;
            Number = number;
            AnchorId = anchorId;
            Level = level;
        }

        public ColumnBuilder Column { get; }
        public string Title { get; }
        public string Number { get; }
        public string AnchorId { get; }
        public int Level { get; }

        public string TitleWithNumber => string.IsNullOrEmpty(Number) ? Title : $"{Number} {Title}";
    }
}
