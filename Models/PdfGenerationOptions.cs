using System;

namespace PdfBuilder.Models
{
    /// <summary>Controls stable metadata and identifiers during PDF generation.</summary>
    public sealed class PdfGenerationOptions
    {
        public bool Deterministic { get; set; }
        public DateTimeOffset? CreationTime { get; set; }
        public DateTimeOffset? ModificationTime { get; set; }
        public string? DocumentIdSeed { get; set; }

        internal PdfGenerationOptions Clone() => new()
        {
            Deterministic = Deterministic,
            CreationTime = CreationTime,
            ModificationTime = ModificationTime,
            DocumentIdSeed = DocumentIdSeed
        };

        internal void CopyFrom(PdfGenerationOptions other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Deterministic = other.Deterministic;
            CreationTime = other.CreationTime;
            ModificationTime = other.ModificationTime;
            DocumentIdSeed = other.DocumentIdSeed;
        }
    }
}
