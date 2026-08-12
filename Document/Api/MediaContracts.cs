using PdfBuilder.Models;

namespace PdfBuilder.Document;

/// <summary>Resampling quality used by image optimisation.</summary>
public enum ImageQuality
{
    Low,
    Medium,
    High
}

/// <summary>Alignment used for contain margins and cover cropping.</summary>
public enum ImageCropAlignment
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

/// <summary>Configures a canonical raster image.</summary>
public interface IImageDescriptor
{
    /// <summary>Fits the complete image inside the allocated box.</summary>
    IImageDescriptor Contain();
    /// <summary>Fills the allocated box and crops overflow.</summary>
    IImageDescriptor Cover();
    /// <summary>Stretches the image to the allocated box.</summary>
    IImageDescriptor Stretch();
    /// <summary>Uses intrinsic image size where DPI metadata is available.</summary>
    IImageDescriptor OriginalSize();
    /// <summary>Centres an aspect-ratio-preserving image.</summary>
    IImageDescriptor AlignCenter();
    /// <summary>Controls which part of a covered image remains visible.</summary>
    IImageDescriptor CropAlignment(ImageCropAlignment alignment);
    /// <summary>Sets resampling quality for image optimisation.</summary>
    IImageDescriptor Quality(ImageQuality quality);
    /// <summary>Enables downsampling above the specified effective DPI.</summary>
    IImageDescriptor MaximumEffectiveDpi(float dpi);
    /// <summary>Enables or disables image downsampling.</summary>
    IImageDescriptor Downsample(bool enabled = true);
    /// <summary>Sets JPEG encoding quality for optimised opaque images.</summary>
    IImageDescriptor JpegQuality(int quality);
    /// <summary>Preserves alpha by selecting an alpha-capable encoding when required.</summary>
    IImageDescriptor AlphaAwareEncoding(bool enabled = true);
    /// <summary>Sets image opacity.</summary>
    IImageDescriptor Opacity(float value);
    /// <summary>Adds an image border.</summary>
    IImageDescriptor Border(float width = 1f, PdfColor? color = null);
    /// <summary>Rounds image corners.</summary>
    IImageDescriptor CornerRadius(float value);
    /// <summary>Clips the image to a circle.</summary>
    IImageDescriptor Circle();
}
