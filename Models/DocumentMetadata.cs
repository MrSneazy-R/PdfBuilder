using System;

namespace PdfBuilder.Models
{
    public sealed class DocumentMetadata
    {
        /// <summary>Gets or sets the document title.</summary>
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Subject { get; set; }
        public string? Keywords { get; set; }
        public string? Creator { get; set; }
        public string? Producer { get; set; }
        public DateTimeOffset? CreatedUtc { get; set; }
        public DateTimeOffset? ModifiedUtc { get; set; }

        public void CopyFrom(DocumentMetadata other)
        {
            if (other == null) return;
            Title = other.Title;
            Author = other.Author;
            Subject = other.Subject;
            Keywords = other.Keywords;
            Creator = other.Creator;
            Producer = other.Producer;
            CreatedUtc = other.CreatedUtc;
            ModifiedUtc = other.ModifiedUtc;
        }

        public DocumentMetadata Clone()
        {
            return new DocumentMetadata
            {
                Author = Author,
                Title = Title,
                Subject = Subject,
                Keywords = Keywords,
                Creator = Creator,
                Producer = Producer,
                CreatedUtc = CreatedUtc,
                ModifiedUtc = ModifiedUtc
            };
        }
    }
}
