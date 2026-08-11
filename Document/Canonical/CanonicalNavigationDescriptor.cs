using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalSectionDescriptor : ISectionDescriptor
    {
        internal int SectionLevel { get; private set; } = 1;
        internal bool IsNumbered { get; private set; } = true;
        internal bool StartsOnNewPage { get; private set; }
        internal bool IsInOutline { get; private set; } = true;
        internal bool IsInTableOfContents { get; private set; } = true;

        public void Level(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            SectionLevel = value;
        }

        public void Numbered(bool enabled = true) => IsNumbered = enabled;
        public void StartOnNewPage(bool enabled = true) => StartsOnNewPage = enabled;
        public void IncludeInOutline(bool enabled = true) => IsInOutline = enabled;
        public void IncludeInTableOfContents(bool enabled = true) => IsInTableOfContents = enabled;
    }

    private sealed class CanonicalTableOfContentsDescriptor : ITableOfContentsDescriptor
    {
        internal bool IncludesSectionNumbers { get; private set; } = true;
        internal float LevelIndent { get; private set; } = 12f;
        internal string ReferenceFormat { get; private set; } = "{0}";
        internal string PendingText { get; private set; } = "…";

        public void IncludeSectionNumbers(bool enabled = true) => IncludesSectionNumbers = enabled;

        public void IndentPerLevel(float value)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            LevelIndent = value;
        }

        public void PageNumberFormat(string format)
        {
            _ = PageReferenceFormatter.CreateConservativeMeasurementText(format);
            ReferenceFormat = format;
        }

        public void PendingPageText(string value)
            => PendingText = string.IsNullOrEmpty(value) ? throw new ArgumentException("Pending page text is required.", nameof(value)) : value;

        internal void Compose(Layout.ContentComposer composer, PaginationRegistry pagination)
        {
            foreach (SectionEntry section in pagination.Sections.Where(section => section.IncludeInToc))
            {
                string title = IncludesSectionNumbers && !string.IsNullOrEmpty(section.Number)
                    ? $"{section.Number} {section.Title}"
                    : section.Title;
                float indent = LevelIndent * Math.Max(0, section.Level - 1);

                composer.Row(row =>
                {
                    row.Relative(1f, content => content.RichText(element =>
                    {
                        element.MarginLeft = indent;
                        element.AvoidBreakInside = false;
                        element.Runs.Add(new RichRun
                        {
                            Text = title,
                            FontFamily = element.FontFamily,
                            FontSize = element.FontSize,
                            Color = element.Color,
                            LinkAnchor = section.AnchorId
                        });
                    }));
                    row.Constant(56f, content => content.PageReference(
                        section.AnchorId,
                        ReferenceFormat,
                        PendingText,
                        element => element.Alignment = TextAlignment.Right));
                });
            }
        }
    }
}
