using PdfBuilder.Document;
using PdfBuilder.Elements;
using SkiaSharp;

namespace PdfBuilder.Writer.Imaging;

internal readonly record struct PreparedImage(byte[] Data, ImageInfo SourceInfo);

/// <summary>Cross-platform decode, EXIF-orientation, resampling, and alpha-aware encoding.</summary>
internal static class SkiaImageOptimiser
{
    public static PreparedImage Prepare(byte[] data, ImageElement element, long maximumPixels)
    {
        ImageInfo sourceInfo = MediaImageDecoders.ReadInfo(data, maximumPixels);
        bool webp = WebpInspector.LooksLikeWebp(data);
        bool oriented = sourceInfo.Orientation != ImageOrientation.Normal;
        (int orientedWidth, int orientedHeight) = GetOrientedDimensions(sourceInfo);
        (int targetWidth, int targetHeight) = GetTargetDimensions(element, orientedWidth, orientedHeight);
        bool resize = targetWidth < orientedWidth || targetHeight < orientedHeight;

        if (!webp && !oriented && !resize)
            return new PreparedImage(data, sourceInfo);

        using DecodedImage decoded = Decode(data, sourceInfo, resize ? (targetWidth, targetHeight) : null, element.Quality);
        bool hasAlpha = decoded.Alpha != null && decoded.Alpha.Any(value => value != 255);
        byte[] encoded = Encode(decoded, hasAlpha && element.AlphaAwareEncoding, element.JpegQuality);
        return new PreparedImage(encoded, sourceInfo);
    }

    public static DecodedImage Decode(byte[] data, ImageInfo sourceInfo, (int Width, int Height)? target, ImageQuality quality = ImageQuality.High)
    {
        using var stream = new SKMemoryStream(data);
        using SKCodec codec = SKCodec.Create(stream) ?? throw new InvalidDataException("Image data could not be decoded by the cross-platform Skia codec.");
        var decodeInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        SKCodecResult result = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new InvalidDataException($"Image decoding failed with status {result}.");

        using SKBitmap oriented = ApplyOrientation(decoded, sourceInfo.Orientation);
        SKBitmap final = oriented.Copy();
        if (target.HasValue && (target.Value.Width != oriented.Width || target.Value.Height != oriented.Height))
        {
            SKFilterQuality filter = quality switch
            {
                ImageQuality.Low => SKFilterQuality.Low,
                ImageQuality.Medium => SKFilterQuality.Medium,
                _ => SKFilterQuality.High
            };
            SKBitmap? resized = oriented.Resize(new SKImageInfo(target.Value.Width, target.Value.Height, SKColorType.Bgra8888, SKAlphaType.Premul), filter);
            final.Dispose();
            final = resized ?? throw new InvalidOperationException("Image downsampling failed.");
        }

        try
        {
            var rgb = new byte[checked(final.Width * final.Height * 3)];
            var alpha = new byte[checked(final.Width * final.Height)];
            bool anyAlpha = false;
            int rgbOffset = 0;
            int alphaOffset = 0;
            for (int y = 0; y < final.Height; y++)
            {
                for (int x = 0; x < final.Width; x++)
                {
                    SKColor color = final.GetPixel(x, y);
                    rgb[rgbOffset++] = color.Red;
                    rgb[rgbOffset++] = color.Green;
                    rgb[rgbOffset++] = color.Blue;
                    alpha[alphaOffset++] = color.Alpha;
                    anyAlpha |= color.Alpha != 255;
                }
            }
            var info = new ImageInfo(final.Width, final.Height, sourceInfo.DpiX, sourceInfo.DpiY, ImageOrientation.Normal);
            return new DecodedImage(info, rgb, anyAlpha ? alpha : null);
        }
        finally
        {
            final.Dispose();
        }
    }

    private static byte[] Encode(DecodedImage decoded, bool preserveAlpha, int jpegQuality)
    {
        var info = new SKImageInfo(decoded.Info.Width, decoded.Info.Height, SKColorType.Bgra8888, preserveAlpha ? SKAlphaType.Premul : SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        int pixel = 0;
        for (int y = 0; y < info.Height; y++)
        {
            for (int x = 0; x < info.Width; x++)
            {
                byte red = decoded.Pixels[pixel * 3];
                byte green = decoded.Pixels[pixel * 3 + 1];
                byte blue = decoded.Pixels[pixel * 3 + 2];
                byte alpha = preserveAlpha ? decoded.Alpha?[pixel] ?? (byte)255 : (byte)255;
                bitmap.SetPixel(x, y, new SKColor(red, green, blue, alpha));
                pixel++;
            }
        }
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData encoded = image.Encode(preserveAlpha ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, preserveAlpha ? 100 : jpegQuality)
            ?? throw new InvalidOperationException("Optimised image encoding failed.");
        return encoded.ToArray();
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, ImageOrientation orientation)
    {
        (int width, int height) = orientation is ImageOrientation.LeftTop or ImageOrientation.RightTop or ImageOrientation.RightBottom or ImageOrientation.LeftBottom
            ? (source.Height, source.Width)
            : (source.Width, source.Height);
        var output = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(output);
        switch (orientation)
        {
            case ImageOrientation.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
            case ImageOrientation.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
            case ImageOrientation.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
            case ImageOrientation.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case ImageOrientation.RightTop: canvas.Translate(width, 0); canvas.RotateDegrees(90); break;
            case ImageOrientation.RightBottom: canvas.Translate(width, height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case ImageOrientation.LeftBottom: canvas.Translate(0, height); canvas.RotateDegrees(-90); break;
        }
        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return output;
    }

    private static (int Width, int Height) GetOrientedDimensions(ImageInfo info)
        => info.Orientation is ImageOrientation.LeftTop or ImageOrientation.RightTop or ImageOrientation.RightBottom or ImageOrientation.LeftBottom
            ? (info.Height, info.Width)
            : (info.Width, info.Height);

    private static (int Width, int Height) GetTargetDimensions(ImageElement element, int sourceWidth, int sourceHeight)
    {
        if (!element.Downsample || element.Width <= 0f || element.Height <= 0f)
            return (sourceWidth, sourceHeight);
        int maximumWidth = Math.Max(1, (int)Math.Ceiling(element.Width / 72f * element.MaximumEffectiveDpi));
        int maximumHeight = Math.Max(1, (int)Math.Ceiling(element.Height / 72f * element.MaximumEffectiveDpi));
        float scale = Math.Min(1f, Math.Min(maximumWidth / (float)sourceWidth, maximumHeight / (float)sourceHeight));
        return (Math.Max(1, (int)Math.Round(sourceWidth * scale)), Math.Max(1, (int)Math.Round(sourceHeight * scale)));
    }
}
