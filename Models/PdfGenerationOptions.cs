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
        /// <summary>Gets or sets an explicit stable hexadecimal trailer identifier (32 or 64 hex characters).</summary>
        public string? DocumentIdentifier { get; set; }

        internal PdfGenerationOptions Clone() => new()
        {
            Deterministic = Deterministic,
            CreationTime = CreationTime,
            ModificationTime = ModificationTime,
            DocumentIdSeed = DocumentIdSeed,
            DocumentIdentifier = DocumentIdentifier
        };

        internal void CopyFrom(PdfGenerationOptions other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Deterministic = other.Deterministic;
            CreationTime = other.CreationTime;
            ModificationTime = other.ModificationTime;
            DocumentIdSeed = other.DocumentIdSeed;
            DocumentIdentifier = other.DocumentIdentifier;
        }

        internal void Validate()
        {
            if (DocumentIdentifier == null)
                return;
            string value = DocumentIdentifier.Trim();
            if (value.Length is not (32 or 64) || value.Any(character => !Uri.IsHexDigit(character)))
                throw new ArgumentException("DocumentIdentifier must contain exactly 32 or 64 hexadecimal characters.", nameof(DocumentIdentifier));
        }
    }
}
