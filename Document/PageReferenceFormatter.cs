using System.Globalization;

namespace PdfBuilder.Document;

internal static class PageReferenceFormatter
{
    internal static string CreateConservativeMeasurementText(string format)
        => Format(format, int.MaxValue);

    internal static string Resolve(string format, int pageNumber)
        => Format(format, pageNumber);

    private static string Format(string format, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(format))
            throw new ArgumentException("A page-reference format is required.", nameof(format));
        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, pageNumber);
        }
        catch (FormatException exception)
        {
            throw new PdfNavigationException($"Page-reference format '{format}' is invalid: {exception.Message}");
        }
    }
}
