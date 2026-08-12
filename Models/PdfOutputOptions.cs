using System.IO.Compression;

namespace PdfBuilder.Models;

/// <summary>Named output configurations for common generation scenarios.</summary>
public enum PdfOutputPreset
{
    Debug,
    Balanced,
    SmallFile,
    PrintQuality,
    Deterministic
}

/// <summary>PDF syntax version written in the file header.</summary>
public enum PdfVersion
{
    Pdf14,
    Pdf15,
    Pdf16,
    Pdf17,
    Pdf20
}

/// <summary>Controls PDF stream encoding, version, and document-wide image optimisation defaults.</summary>
public sealed class PdfOutputOptions
{
    public bool CompressContentStreams { get; set; } = true;

    public bool ReadableContentStreams
    {
        get => !CompressContentStreams;
        set => CompressContentStreams = !value;
    }

    public CompressionLevel ContentCompressionLevel { get; set; } = CompressionLevel.Optimal;
    public CompressionLevel ImageCompressionLevel { get; set; } = CompressionLevel.Optimal;
    public bool UsePngPredictor { get; set; } = true;
    public PdfVersion PdfVersion { get; set; } = PdfVersion.Pdf16;
    public bool DownsampleImages { get; set; }
    public float MaximumImageDpi { get; set; } = 300f;
    public int JpegQuality { get; set; } = 85;

    /// <summary>Applies a named preset. Explicit property overrides may be applied afterwards.</summary>
    public PdfOutputOptions ApplyPreset(PdfOutputPreset preset)
    {
        PdfVersion = PdfVersion.Pdf16;
        switch (preset)
        {
            case PdfOutputPreset.Debug:
                CompressContentStreams = false;
                ContentCompressionLevel = CompressionLevel.Fastest;
                ImageCompressionLevel = CompressionLevel.Fastest;
                UsePngPredictor = false;
                DownsampleImages = false;
                MaximumImageDpi = 300f;
                JpegQuality = 90;
                break;
            case PdfOutputPreset.SmallFile:
                CompressContentStreams = true;
                ContentCompressionLevel = CompressionLevel.SmallestSize;
                ImageCompressionLevel = CompressionLevel.SmallestSize;
                UsePngPredictor = true;
                DownsampleImages = true;
                MaximumImageDpi = 150f;
                JpegQuality = 75;
                break;
            case PdfOutputPreset.PrintQuality:
                CompressContentStreams = true;
                ContentCompressionLevel = CompressionLevel.Optimal;
                ImageCompressionLevel = CompressionLevel.Optimal;
                UsePngPredictor = true;
                DownsampleImages = true;
                MaximumImageDpi = 450f;
                JpegQuality = 95;
                break;
            case PdfOutputPreset.Balanced:
            case PdfOutputPreset.Deterministic:
                CompressContentStreams = true;
                ContentCompressionLevel = CompressionLevel.Optimal;
                ImageCompressionLevel = CompressionLevel.Optimal;
                UsePngPredictor = true;
                DownsampleImages = false;
                MaximumImageDpi = 300f;
                JpegQuality = 85;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset));
        }
        return this;
    }

    public void CopyFrom(PdfOutputOptions other)
    {
        if (other == null) return;
        CompressContentStreams = other.CompressContentStreams;
        ContentCompressionLevel = other.ContentCompressionLevel;
        ImageCompressionLevel = other.ImageCompressionLevel;
        UsePngPredictor = other.UsePngPredictor;
        PdfVersion = other.PdfVersion;
        DownsampleImages = other.DownsampleImages;
        MaximumImageDpi = other.MaximumImageDpi;
        JpegQuality = other.JpegQuality;
    }

    public PdfOutputOptions Clone()
    {
        var clone = new PdfOutputOptions();
        clone.CopyFrom(this);
        return clone;
    }

    internal void Validate()
    {
        if (!float.IsFinite(MaximumImageDpi) || MaximumImageDpi <= 0f)
            throw new ArgumentOutOfRangeException(nameof(MaximumImageDpi), "Maximum image DPI must be positive and finite.");
        if (JpegQuality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(JpegQuality), "JPEG quality must be between 1 and 100.");
    }

    internal string VersionToken => PdfVersion switch
    {
        PdfVersion.Pdf14 => "1.4",
        PdfVersion.Pdf15 => "1.5",
        PdfVersion.Pdf16 => "1.6",
        PdfVersion.Pdf17 => "1.7",
        PdfVersion.Pdf20 => "2.0",
        _ => throw new ArgumentOutOfRangeException(nameof(PdfVersion))
    };
}
