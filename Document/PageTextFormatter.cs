using System.Globalization;

namespace PdfBuilder.Document;

internal static class PageTextFormatter
{
    private const int MaximumInt32Digits = 10;
    private static readonly string ConservativeDigits = new('8', MaximumInt32Digits);

    internal static bool ContainsToken(string? template)
        => template?.Contains(PageTextTokens.CurrentPage, StringComparison.Ordinal) == true
            || template?.Contains(PageTextTokens.TotalPages, StringComparison.Ordinal) == true;

    internal static string CreateConservativeMeasurementText(string template)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        return template
            .Replace(PageTextTokens.CurrentPage, ConservativeDigits, StringComparison.Ordinal)
            .Replace(PageTextTokens.TotalPages, ConservativeDigits, StringComparison.Ordinal);
    }

    internal static string Resolve(string template, PageContext context)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (context == null) throw new ArgumentNullException(nameof(context));

        return template
            .Replace(PageTextTokens.CurrentPage, context.CurrentPage.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace(PageTextTokens.TotalPages, context.TotalPages.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
