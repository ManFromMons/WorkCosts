using System.Text.RegularExpressions;
using WorkCosts.Services;

namespace WorkCosts.Helpers;

public static class ProductUrl
{
    public static string Normalize(string url)
    {
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return trimmed;
        }

        return Normalize(uri);
    }

    public static string Normalize(Uri pageUri)
    {
        if (ProductPageMetadataParser.IsAmazonHost(pageUri.Host))
        {
            var asin = Regex.Match(
                pageUri.AbsolutePath,
                @"/(?:dp|gp/product|gp/aw/d|product)/([A-Z0-9]{10})",
                RegexOptions.IgnoreCase);
            if (asin.Success)
            {
                return $"{pageUri.Scheme}://{pageUri.Host.ToLowerInvariant()}/dp/{asin.Groups[1].Value.ToUpperInvariant()}";
            }
        }

        return pageUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    public static bool Same(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Accepts a pasted address, adding https:// when the scheme is missing.
    /// </summary>
    public static bool TryCoerceHttpUrl(string? raw, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim().Trim('"', '\'').Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal).Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "https://" + trimmed.TrimStart('/');
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        url = uri.ToString();
        return true;
    }
}
