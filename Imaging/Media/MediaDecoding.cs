using System.Buffers.Binary;

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
    Normal = 1
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
    public const int MaximumSvgNodes = 20_000;
    public const int MaximumSvgPathCharacters = 500_000;

    public static void Validate(ImageInfo info)
    {
        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaximumDimension || info.Height > MaximumDimension || info.PixelCount > MaximumDecodedPixels)
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
    private static readonly IImageDecoder[] Decoders = [new PngImageDecoder(), new JpegImageDecoder()];

    public static ImageInfo ReadInfo(byte[] data)
    {
        MediaLimits.ValidateSource(data);
        foreach (var decoder in Decoders)
        {
            if (!decoder.CanDecode(data))
                continue;

            var info = decoder.ReadInfo(data);
            MediaLimits.Validate(info);
            return info;
        }

        if (WebpInspector.LooksLikeWebp(data))
            throw new NotSupportedException("WebP is not supported because PdfBuilder does not provide a tested cross-platform WebP decoder.");

        throw new InvalidDataException("Unsupported image format. PdfBuilder supports PNG and JPEG.");
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
            return new ImageInfo(width, height, 96f, 96f, ImageOrientation.Normal);
        }

        public DecodedImage Decode(byte[] data)
        {
            var info = ReadInfo(data);
            MediaLimits.Validate(info);
            var decoded = PngDecoder.Decode(data);
            return new DecodedImage(info, decoded.Pixels, decoded.Alpha);
        }
    }

    private sealed class JpegImageDecoder : IImageDecoder
    {
        public bool CanDecode(ReadOnlySpan<byte> data) => JpegInspector.LooksLikeJpeg(data);

        public ImageInfo ReadInfo(ReadOnlySpan<byte> data)
        {
            var jpeg = JpegInspector.GetInfo(data.ToArray());
            return new ImageInfo(jpeg.Width, jpeg.Height, 96f, 96f, ImageOrientation.Normal);
        }

        public DecodedImage Decode(byte[] data) => throw new NotSupportedException("JPEG is written as a native PDF DCT stream and is not decoded into managed pixels.");
    }
}
