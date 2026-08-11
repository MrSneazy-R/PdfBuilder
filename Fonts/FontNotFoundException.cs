namespace PdfBuilder.Fonts;

/// <summary>Thrown when strict font matching cannot resolve a requested family or glyph.</summary>
public sealed class FontNotFoundException : InvalidOperationException
{
    public FontNotFoundException(string message) : base(message) { }
}
