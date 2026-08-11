namespace PdfBuilder.Document;

internal static class NavigationUriPolicy
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeMailto
    };

    internal static string ValidateExternal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("An external link URI is required.", nameof(value));
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            throw new PdfNavigationException($"External link URI '{value}' is not a valid absolute URI.");
        if (!AllowedSchemes.Contains(uri.Scheme))
            throw new PdfNavigationException(
                $"External link URI scheme '{uri.Scheme}' is not allowed. Use HTTP, HTTPS, or mailto; executable and file schemes are rejected by default.");
        return value;
    }

    internal static string ValidateAnchorId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("An internal anchor id is required.", parameterName);
        return value.Trim();
    }
}
