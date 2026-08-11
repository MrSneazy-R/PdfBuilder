using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;

namespace PdfBuilder.Writer.Imaging;

/// <summary>Internal SVG rendering boundary. Renderers must not resolve external resources.</summary>
internal interface ISvgRenderer
{
    byte[] Render(string markup, float widthPoints, float heightPoints, float dpi);
}

/// <summary>
/// Parses SVG as non-DTD XML, rejects active content and external references, removes comments
/// and processing instructions, then renders the sanitized tree through Skia.
/// </summary>
internal sealed class SecureSvgRenderer : ISvgRenderer
{
    private static readonly Regex UrlReference = new(@"\burl\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> BlockedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "foreignObject", "iframe", "object", "embed", "audio", "video"
    };

    public byte[] Render(string markup, float widthPoints, float heightPoints, float dpi)
    {
        string sanitized = Sanitize(markup, widthPoints, heightPoints, dpi);

        var svg = new SkiaSharp.Extended.Svg.SKSvg();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(sanitized));
        svg.Load(source);

        var picture = svg.Picture ?? throw new InvalidDataException("SVG markup could not be parsed.");
        var bounds = svg.ViewBox;
        if (bounds.Width <= 0f || bounds.Height <= 0f)
            bounds = picture.CullRect;
        if (bounds.Width <= 0f || bounds.Height <= 0f)
            throw new InvalidDataException("SVG must define a non-empty viewBox or drawable bounds.");

        int pixelWidth = checked(Math.Max(1, (int)Math.Ceiling(widthPoints * dpi / 72f)));
        int pixelHeight = checked(Math.Max(1, (int)Math.Ceiling(heightPoints * dpi / 72f)));
        MediaLimits.Validate(new ImageInfo(pixelWidth, pixelHeight, dpi, dpi, ImageOrientation.Normal));

        var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Unable to create an SVG raster surface.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float scale = Math.Min(pixelWidth / bounds.Width, pixelHeight / bounds.Height);
        float offsetX = (pixelWidth - bounds.Width * scale) / 2f - bounds.Left * scale;
        float offsetY = (pixelHeight - bounds.Height * scale) / 2f - bounds.Top * scale;
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale, scale);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded?.ToArray() ?? throw new InvalidOperationException("Unable to encode rendered SVG.");
    }

    private static string Sanitize(string markup, float widthPoints, float heightPoints, float dpi)
    {
        if (string.IsNullOrWhiteSpace(markup))
            throw new ArgumentException("SVG markup is required.", nameof(markup));
        if (markup.Length > MediaLimits.MaximumSvgCharacters)
            throw new InvalidDataException("SVG markup exceeds PdfBuilder's size limit.");
        if (Encoding.UTF8.GetByteCount(markup) > MediaLimits.MaximumSvgBytes)
            throw new InvalidDataException("SVG encoded bytes exceed PdfBuilder's size limit.");
        if (widthPoints <= 0f || heightPoints <= 0f || dpi <= 0f || !float.IsFinite(widthPoints) || !float.IsFinite(heightPoints) || !float.IsFinite(dpi))
            throw new ArgumentOutOfRangeException(nameof(widthPoints), "SVG dimensions and DPI must be positive finite values.");

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MediaLimits.MaximumSvgCharacters,
                MaxCharactersFromEntities = 0
            };
            using var text = new StringReader(markup);
            using XmlReader reader = XmlReader.Create(text, settings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("SVG markup is not safe, well-formed XML.", exception);
        }

        XElement root = document.Root ?? throw new InvalidDataException("SVG markup has no root element.");
        if (!root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SVG markup must have an svg root element.");

        XElement[] elements = root.DescendantsAndSelf().ToArray();
        if (elements.Length > MediaLimits.MaximumSvgNodes)
            throw new InvalidDataException("SVG contains too many nodes.");
        if (elements.Any(element => BlockedElements.Contains(element.Name.LocalName)))
            throw new InvalidDataException("SVG scripts and active embedded content are not allowed.");

        int pathCharacters = 0;
        foreach (XElement element in elements)
        {
            if (element.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase) &&
                (ContainsUnsafeUrl(element.Value) || element.Value.Contains("@import", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("SVG URL-based styles and external resource references are not allowed.");
            foreach (XAttribute attribute in element.Attributes())
            {
                string name = attribute.Name.LocalName;
                string value = attribute.Value.Trim();
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SVG event-handler attributes are not allowed.");
                if (name.Equals("href", StringComparison.OrdinalIgnoreCase) && !IsSafeFragmentReference(value))
                    throw new InvalidDataException("SVG scripts and external resource references are not allowed.");
                if ((name.Equals("style", StringComparison.OrdinalIgnoreCase) || name.Equals("fill", StringComparison.OrdinalIgnoreCase) || name.Equals("stroke", StringComparison.OrdinalIgnoreCase)) &&
                    (ContainsUnsafeUrl(value) || value.Contains("@import", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("SVG URL-based styles and external resource references are not allowed.");
                if (value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) || value.Contains("file:", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SVG executable and file references are not allowed.");
                if (name.Equals("d", StringComparison.OrdinalIgnoreCase))
                    pathCharacters = checked(pathCharacters + value.Length);
            }
        }
        if (pathCharacters > MediaLimits.MaximumSvgPathCharacters)
            throw new InvalidDataException("SVG path data exceeds PdfBuilder's complexity limit.");

        document.DescendantNodes().OfType<XComment>().Remove();
        document.DescendantNodes().OfType<XProcessingInstruction>().Remove();
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static bool IsSafeFragmentReference(string value)
        => string.IsNullOrEmpty(value) || (value.StartsWith('#') && value.Length > 1 && !value.Any(char.IsControl));

    private static bool ContainsUnsafeUrl(string value)
    {
        MatchCollection matches = UrlReference.Matches(value);
        if (matches.Count == 0) return false;
        foreach (Match match in matches)
        {
            int start = match.Index + match.Length;
            int end = value.IndexOf(')', start);
            if (end < 0 || !IsSafeFragmentReference(value[start..end].Trim().Trim('\'', '"')))
                return true;
        }
        return false;
    }
}
