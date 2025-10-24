using System;

namespace PdfBuilder.Models
{
    public sealed class DocumentMetadata
    {
        public string? Author { get; set; }
        public string? Subject { get; set; }
        public string? Keywords { get; set; }
        public string? Creator { get; set; }
        public string? Producer { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public DateTime? ModifiedUtc { get; set; }

        public void CopyFrom(DocumentMetadata other)
        {
            if (other == null) return;
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
