using System.IO.Compression;

namespace PdfBuilder.Models
{
    public sealed class PdfOutputOptions
    {
        public bool CompressContentStreams { get; set; }

        public CompressionLevel ContentCompressionLevel { get; set; } = CompressionLevel.Optimal;

        public CompressionLevel ImageCompressionLevel { get; set; } = CompressionLevel.Optimal;

        public bool UsePngPredictor { get; set; } = true;

        public void CopyFrom(PdfOutputOptions other)
        {
            if (other == null) return;
            CompressContentStreams = other.CompressContentStreams;
            ContentCompressionLevel = other.ContentCompressionLevel;
            ImageCompressionLevel = other.ImageCompressionLevel;
            UsePngPredictor = other.UsePngPredictor;
        }

        public PdfOutputOptions Clone()
        {
            return new PdfOutputOptions
            {
                CompressContentStreams = CompressContentStreams,
                ContentCompressionLevel = ContentCompressionLevel,
                ImageCompressionLevel = ImageCompressionLevel,
                UsePngPredictor = UsePngPredictor
            };
        }
    }
}
