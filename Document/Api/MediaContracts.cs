using PdfBuilder.Models;

namespace PdfBuilder.Document;

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
    /// <summary>Sets image opacity.</summary>
    IImageDescriptor Opacity(float value);
    /// <summary>Adds an image border.</summary>
    IImageDescriptor Border(float width = 1f, PdfColor? color = null);
    /// <summary>Rounds image corners.</summary>
    IImageDescriptor CornerRadius(float value);
    /// <summary>Clips the image to a circle.</summary>
    IImageDescriptor Circle();
}
