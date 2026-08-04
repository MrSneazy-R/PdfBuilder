using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace PdfBuilder.Writer.Imaging;

/// <summary>Internal SVG rendering boundary. Renderers must not resolve external resources.</summary>
internal interface ISvgRenderer
{
    byte[] Render(string markup, float widthPoints, float heightPoints, float dpi);
}

/// <summary>
/// SVG renderer with conservative input validation. SVG is supplied as markup only: scripts,
/// external references, filters and URL resources are rejected before Skia parses the document.
/// </summary>
internal sealed class SecureSvgRenderer : ISvgRenderer
{
    private static readonly Regex ExternalReference = new(@"(?:xlink:)?href\s*=|\burl\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Script = new(@"<\s*script\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PathData = new("\\bd\\s*=\\s*(['\\\"])(?<value>.*?)\\1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public byte[] Render(string markup, float widthPoints, float heightPoints, float dpi)
    {
        Validate(markup, widthPoints, heightPoints, dpi);

        var svg = new SkiaSharp.Extended.Svg.SKSvg();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(markup));
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

    private static void Validate(string markup, float widthPoints, float heightPoints, float dpi)
    {
        if (string.IsNullOrWhiteSpace(markup))
            throw new ArgumentException("SVG markup is required.", nameof(markup));
        if (markup.Length > MediaLimits.MaximumSvgCharacters)
            throw new InvalidDataException("SVG markup exceeds PdfBuilder's size limit.");
        if (widthPoints <= 0f || heightPoints <= 0f || dpi <= 0f || float.IsNaN(widthPoints) || float.IsNaN(heightPoints) || float.IsNaN(dpi))
            throw new ArgumentOutOfRangeException(nameof(widthPoints), "SVG dimensions and DPI must be positive finite values.");
        if (Script.IsMatch(markup) || ExternalReference.IsMatch(markup))
            throw new InvalidDataException("SVG scripts and external resource references are not allowed.");
        if (Regex.Matches(markup, "<").Count > MediaLimits.MaximumSvgNodes)
            throw new InvalidDataException("SVG contains too many nodes.");
        if (PathData.Matches(markup).Cast<Match>().Sum(match => match.Groups["value"].Length) > MediaLimits.MaximumSvgPathCharacters)
            throw new InvalidDataException("SVG path data exceeds PdfBuilder's complexity limit.");
    }
}
