using PdfBuilder.Writer.Imaging;

namespace PdfBuilder.Document;

internal static class ImageSourceMetadata
{
    public static ImageSourceInfo Read(byte[] data)
    {
        ImageInfo info = MediaImageDecoders.ReadInfo(data);
        bool swapsDimensions = info.Orientation is ImageOrientation.LeftTop or ImageOrientation.RightTop or ImageOrientation.RightBottom or ImageOrientation.LeftBottom;
        return swapsDimensions
            ? new ImageSourceInfo(info.Height, info.Width, info.DpiY, info.DpiX)
            : new ImageSourceInfo(info.Width, info.Height, info.DpiX, info.DpiY);
    }
}
