namespace PdfBuilder.Fonts;

/// <summary>
/// The exception that is thrown when strict font matching cannot resolve a requested font or glyph.
/// </summary>
public sealed class FontNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontNotFoundException"/> class.
    /// </summary>
    /// <param name="message">A description of the missing font or glyph.</param>
    public FontNotFoundException(string message)
        : base(message)
    {
    }
}
