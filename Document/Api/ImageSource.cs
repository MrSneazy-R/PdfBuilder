using System.Reflection;

namespace PdfBuilder.Document;

/// <summary>Immutable intrinsic metadata for an image source.</summary>
public readonly record struct ImageSourceInfo(
    int PixelWidth,
    int PixelHeight,
    float DpiX,
    float DpiY)
{
    public float OriginalWidthPoints => PixelWidth * 72f / DpiX;
    public float OriginalHeightPoints => PixelHeight * 72f / DpiY;
}

/// <summary>
/// A reusable, thread-safe source of caller-owned image bytes. The first load is snapshotted and
/// shared by every document that reuses this instance.
/// </summary>
public sealed class ImageSource
{
    private const int MaximumSourceBytes = 64 * 1024 * 1024;
    private readonly Lazy<byte[]> _content;
    private readonly Lazy<ImageSourceInfo> _info;

    private ImageSource(Func<byte[]> loader)
    {
        _content = new Lazy<byte[]>(() => Snapshot(loader()), LazyThreadSafetyMode.ExecutionAndPublication);
        _info = new Lazy<ImageSourceInfo>(() => ImageSourceMetadata.Read(_content.Value), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Creates a source by immediately snapshotting a byte array.</summary>
    public static ImageSource FromBytes(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        byte[] snapshot = Snapshot(data);
        return new ImageSource(() => snapshot);
    }

    /// <summary>Creates a source by immediately snapshotting read-only memory.</summary>
    public static ImageSource FromMemory(ReadOnlyMemory<byte> data)
    {
        byte[] snapshot = Snapshot(data.ToArray());
        return new ImageSource(() => snapshot);
    }

    /// <summary>Creates a source by reading the stream immediately without taking ownership.</summary>
    public static ImageSource FromStream(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        byte[] snapshot = ReadBounded(stream);
        return new ImageSource(() => snapshot);
    }

    /// <summary>Creates a lazily loaded local-file source. Remote URLs are not accepted.</summary>
    public static ImageSource FromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An image file path is required.", nameof(path));
        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && !uri.IsFile)
            throw new ArgumentException("Remote image URLs are outside the PdfBuilder core API.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        return new ImageSource(() => File.ReadAllBytes(fullPath));
    }

    /// <summary>Creates a lazily loaded embedded-resource source.</summary>
    public static ImageSource FromEmbeddedResource(Assembly assembly, string resourceName)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (string.IsNullOrWhiteSpace(resourceName)) throw new ArgumentException("An embedded resource name is required.", nameof(resourceName));
        return new ImageSource(() =>
        {
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded image resource '{resourceName}' was not found in '{assembly.GetName().Name}'.", resourceName);
            return ReadBounded(stream);
        });
    }

    /// <summary>
    /// Creates a lazy source. The caller retains ownership of the returned array; PdfBuilder
    /// snapshots it exactly once using thread-safe publication.
    /// </summary>
    public static ImageSource FromLazy(Func<byte[]> byteFactory)
        => new(byteFactory ?? throw new ArgumentNullException(nameof(byteFactory)));

    /// <summary>Loads and validates this source now so it can be reused without later I/O.</summary>
    public ImageSource Preload()
    {
        _ = _info.Value;
        return this;
    }

    /// <summary>Returns intrinsic pixel dimensions and DPI-aware original size metadata.</summary>
    public ImageSourceInfo Inspect() => _info.Value;

    internal byte[] GetBytes() => _content.Value;

    private static byte[] Snapshot(byte[] data)
    {
        if (data.Length == 0) throw new InvalidDataException("Image data is empty.");
        if (data.Length > MaximumSourceBytes) throw new InvalidDataException($"Image source exceeds the {MaximumSourceBytes / (1024 * 1024)} MB media limit.");
        return data.ToArray();
    }

    private static byte[] ReadBounded(Stream stream)
    {
        using var destination = new MemoryStream();
        byte[] buffer = new byte[81920];
        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            if (destination.Length + read > MaximumSourceBytes)
                throw new InvalidDataException($"Image source exceeds the {MaximumSourceBytes / (1024 * 1024)} MB media limit.");
            destination.Write(buffer, 0, read);
        }
        return Snapshot(destination.ToArray());
    }
}
