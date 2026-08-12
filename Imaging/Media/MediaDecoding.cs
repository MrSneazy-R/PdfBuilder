using System.Buffers.Binary;
using SkiaSharp;

namespace PdfBuilder.Writer.Imaging;

/// <summary>
/// Document-scoped raster decoder contract. Implementations never retain caller-owned bytes.
/// A <see cref="DecodedImage"/> owns its decoded buffers and clears them on disposal.
/// </summary>
internal interface IImageDecoder
{
    bool CanDecode(ReadOnlySpan<byte> data);
    ImageInfo ReadInfo(ReadOnlySpan<byte> data);
    DecodedImage Decode(byte[] data);
}

/// <summary>Validated intrinsic image metadata. Dimensions are expressed in pixels.</summary>
internal readonly record struct ImageInfo(int Width, int Height, float DpiX, float DpiY, ImageOrientation Orientation)
{
    public long PixelCount => (long)Width * Height;
}

/// <summary>Raster orientation declared by image metadata when it is available.</summary>
internal enum ImageOrientation
{
    Normal = 1,
    TopRight = 2,
    BottomRight = 3,
    BottomLeft = 4,
    LeftTop = 5,
    RightTop = 6,
    RightBottom = 7,
    LeftBottom = 8
}

/// <summary>
/// Owned decoded pixel buffers. Consumers must dispose instances when decoder output is retained.
/// The PDF resource writer currently consumes data synchronously and does not retain this type.
/// </summary>
internal sealed class DecodedImage : IDisposable
{
    public DecodedImage(ImageInfo info, byte[] pixels, byte[]? alpha = null)
    {
        Info = info;
        Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        Alpha = alpha;
    }

    public ImageInfo Info { get; }
    public byte[] Pixels { get; private set; }
    public byte[]? Alpha { get; private set; }

    public void Dispose()
    {
        Pixels = Array.Empty<byte>();
        Alpha = null;
    }
}

/// <summary>Central media limits applied before a decoder allocates untrusted image data.</summary>
internal static class MediaLimits
{
    public const int MaximumSourceBytes = 64 * 1024 * 1024;
    public const int MaximumDimension = 32_768;
    public const long MaximumDecodedPixels = 100_000_000;
    public const int MaximumSvgCharacters = 1_000_000;
    public const int MaximumSvgBytes = 2_000_000;
    public const int MaximumSvgNodes = 20_000;
    public const int MaximumSvgPathCharacters = 500_000;

    public static void Validate(ImageInfo info, long maximumPixels = MaximumDecodedPixels)
    {
        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaximumDimension || info.Height > MaximumDimension || info.PixelCount > maximumPixels)
            throw new InvalidDataException($"Image dimensions {info.Width}x{info.Height} exceed PdfBuilder media limits.");
    }

    public static void ValidateSource(byte[] data)
    {
        if (data == null || data.Length == 0)
            throw new InvalidDataException("Image data is empty.");
        if (data.Length > MaximumSourceBytes)
            throw new InvalidDataException($"Image source exceeds the {MaximumSourceBytes / (1024 * 1024)} MB media limit.");
    }
}

/// <summary>Finds a cross-platform decoder and validates source data before decode/write operations.</summary>
internal static class MediaImageDecoders
{
    private static readonly IImageDecoder[] Decoders = [new PngImageDecoder(), new JpegImageDecoder(), new WebpImageDecoder()];

    public static ImageInfo ReadInfo(byte[] data, long maximumPixels = MediaLimits.MaximumDecodedPixels)
    {
        MediaLimits.ValidateSource(data);
        foreach (var decoder in Decoders)
        {
            if (!decoder.CanDecode(data))
                continue;

            var info = decoder.ReadInfo(data);
            MediaLimits.Validate(info, maximumPixels);
            return info;
        }

        throw new InvalidDataException("Unsupported image format. PdfBuilder supports PNG, JPEG, and WebP.");
    }

    private sealed class PngImageDecoder : IImageDecoder
    {
        public bool CanDecode(ReadOnlySpan<byte> data) => PngDecoder.LooksLikePng(data);

        public ImageInfo ReadInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < 24 || !data.Slice(12, 4).SequenceEqual("IHDR"u8))
                throw new InvalidDataException("PNG does not contain a valid IHDR chunk.");

            int width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(16, 4));
            int height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(20, 4));
            (float dpiX, float dpiY) = ReadPngDpi(data);
            return new ImageInfo(width, height, dpiX, dpiY, ImageOrientation.Normal);
        }

        public DecodedImage Decode(byte[] data)
        {
            var info = ReadInfo(data);
            MediaLimits.Validate(info);
            var decoded = PngDecoder.Decode(data);
            return new DecodedImage(info, decoded.Pixels, decoded.Alpha);
        }

        private static (float X, float Y) ReadPngDpi(ReadOnlySpan<byte> data)
        {
            int offset = 8;
            while (offset + 12 <= data.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
                if (length < 0 || offset + 12 + length > data.Length) break;
                ReadOnlySpan<byte> type = data.Slice(offset + 4, 4);
                if (type.SequenceEqual("pHYs"u8) && length == 9 && data[offset + 16] == 1)
                {
                    uint xPixelsPerMeter = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8, 4));
                    uint yPixelsPerMeter = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 12, 4));
                    const float inchesPerMeter = 39.3700787f;
                    return (Math.Max(1f, xPixelsPerMeter / inchesPerMeter), Math.Max(1f, yPixelsPerMeter / inchesPerMeter));
                }
                offset += length + 12;
            }
            return (96f, 96f);
        }
    }

    private sealed class JpegImageDecoder : IImageDecoder
    {
        public bool CanDecode(ReadOnlySpan<byte> data) => JpegInspector.LooksLikeJpeg(data);

        public ImageInfo ReadInfo(ReadOnlySpan<byte> data)
        {
            var jpeg = JpegInspector.GetInfo(data.ToArray());
            using var stream = new SKMemoryStream(data.ToArray());
            using SKCodec codec = SKCodec.Create(stream) ?? throw new InvalidDataException("JPEG metadata could not be decoded.");
            (float dpiX, float dpiY) = JpegMetadata.ReadDpi(data);
            return new ImageInfo(jpeg.Width, jpeg.Height, dpiX, dpiY, MapOrientation(codec.EncodedOrigin));
        }

        public DecodedImage Decode(byte[] data) => throw new NotSupportedException("JPEG is written as a native PDF DCT stream and is not decoded into managed pixels.");
    }

    private sealed class WebpImageDecoder : IImageDecoder
    {
        public bool CanDecode(ReadOnlySpan<byte> data) => WebpInspector.LooksLikeWebp(data);

        public ImageInfo ReadInfo(ReadOnlySpan<byte> data)
        {
            WebpInspector.Info info = WebpInspector.GetInfo(data.ToArray());
            if (info.Animated) throw new NotSupportedException("Animated WebP images are not supported; provide a still WebP frame.");
            return new ImageInfo(info.Width, info.Height, 96f, 96f, ImageOrientation.Normal);
        }

        public DecodedImage Decode(byte[] data) => SkiaImageOptimiser.Decode(data, ReadInfo(data), null);
    }

    private static ImageOrientation MapOrientation(SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopRight => ImageOrientation.TopRight,
        SKEncodedOrigin.BottomRight => ImageOrientation.BottomRight,
        SKEncodedOrigin.BottomLeft => ImageOrientation.BottomLeft,
        SKEncodedOrigin.LeftTop => ImageOrientation.LeftTop,
        SKEncodedOrigin.RightTop => ImageOrientation.RightTop,
        SKEncodedOrigin.RightBottom => ImageOrientation.RightBottom,
        SKEncodedOrigin.LeftBottom => ImageOrientation.LeftBottom,
        _ => ImageOrientation.Normal
    };
}

internal static class JpegMetadata
{
    public static (float X, float Y) ReadDpi(ReadOnlySpan<byte> data)
    {
        int offset = 2;
        while (offset + 4 <= data.Length)
        {
            if (data[offset] != 0xFF) { offset++; continue; }
            byte marker = data[offset + 1];
            if (marker is 0xDA or 0xD9) break;
            int length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 2, 2));
            if (length < 2 || offset + 2 + length > data.Length) break;
            if (marker == 0xE0 && length >= 16 && data.Slice(offset + 4, 5).SequenceEqual("JFIF\0"u8))
            {
                byte units = data[offset + 11];
                ushort xDensity = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 12, 2));
                ushort yDensity = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 14, 2));
                if (xDensity > 0 && yDensity > 0)
                {
                    float factor = units == 2 ? 2.54f : 1f;
                    if (units is 1 or 2) return (xDensity * factor, yDensity * factor);
                }
            }
            offset += length + 2;
        }
        return (96f, 96f);
    }
}
