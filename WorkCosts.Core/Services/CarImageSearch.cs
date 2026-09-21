using System.Net;
using System.Text.RegularExpressions;

namespace WorkCosts.Services;

public static class CarImageSearch
{
    public const int MaxCandidates = 12;

    public sealed record Page(string Html, int StatusCode);

    public static string BuildQuery(string make, string modelNumber)
    {
        var left = make.Trim();
        var right = modelNumber.Trim();
        if (left.Length == 0)
        {
            return right;
        }

        if (right.Length == 0)
        {
            return left;
        }

        return left + " " + right;
    }

    public static string BingImagesUrl(string query) =>
        "https://www.bing.com/images/search?q=" + Uri.EscapeDataString(query);

    public static string GoogleImagesUrl(string query) =>
        "https://www.google.com/search?tbm=isch&q=" + Uri.EscapeDataString(query);

    public static bool IsChallenge(int statusCode, string? html)
    {
        if (statusCode is 401 or 403 or 429 or 503)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        return html.Contains("cf-challenge", StringComparison.OrdinalIgnoreCase)
            || html.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || html.Contains("g-recaptcha", StringComparison.OrdinalIgnoreCase)
            || html.Contains("hcaptcha", StringComparison.OrdinalIgnoreCase)
            || html.Contains("unusual traffic", StringComparison.OrdinalIgnoreCase)
            || html.Contains("/sorry/index", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> ExtractImageUrls(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var decoded = WebUtility.HtmlDecode(html);
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in OriginalImageUrls.Matches(decoded))
        {
            Add(match.Groups[1].Value);
        }

        foreach (Match match in ImgSrcUrls.Matches(decoded))
        {
            Add(match.Groups[1].Value);
        }

        return found;

        void Add(string raw)
        {
            var url = WebUtility.HtmlDecode(raw).Replace("\\u0026", "&", StringComparison.Ordinal).Replace("\\/", "/", StringComparison.Ordinal).Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return;
            }

            if (uri.Scheme is not ("http" or "https"))
            {
                return;
            }

            if (!HasImageExtension(uri))
            {
                return;
            }

            if (seen.Add(uri.AbsoluteUri))
            {
                found.Add(uri.AbsoluteUri);
            }
        }
    }

    /// <summary>
    /// Bing Images first. Google Images only when Bing HTML has no usable image files.
    /// <paramref name="fetchPage"/> returns the page after any challenge retry.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindImageUrlsAsync(
        string make,
        string modelNumber,
        Func<string, CancellationToken, Task<Page>> fetchPage,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(make, modelNumber);
        var bing = await fetchPage(BingImagesUrl(query), cancellationToken).ConfigureAwait(false);
        var bingUrls = ExtractImageUrls(bing.Html);
        if (bingUrls.Count > 0)
        {
            return bingUrls;
        }

        var google = await fetchPage(GoogleImagesUrl(query), cancellationToken).ConfigureAwait(false);
        return ExtractImageUrls(google.Html);
    }

    public static async Task<List<ProductImageCandidate>> DownloadCandidatesAsync(
        HttpClient http,
        IReadOnlyList<string> urls,
        CancellationToken cancellationToken = default)
    {
        var list = new List<ProductImageCandidate>();
        foreach (var url in urls)
        {
            if (list.Count >= MaxCandidates)
            {
                break;
            }

            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                if (response.Content.Headers.ContentLength is long length && length > CarImageStore.MaxImageBytes)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var bytes = await ReadLimitedAsync(stream, CarImageStore.MaxImageBytes, cancellationToken).ConfigureAwait(false);
                if (bytes is null || !CarImageStore.TryDescribe(bytes, out var contentType))
                {
                    continue;
                }

                list.Add(new ProductImageCandidate
                {
                    SourceUrl = url,
                    Bytes = bytes,
                    ContentType = contentType,
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Skip a candidate that cannot be downloaded. The sheet can retry or pick a file.
            }
        }

        return list;
    }

    private static async Task<byte[]?> ReadLimitedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.Length == 0 ? null : buffer.ToArray();
    }

    private static bool HasImageExtension(Uri uri)
    {
        var ext = Path.GetExtension(uri.AbsolutePath);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Regex OriginalImageUrls = new(
        """(?:"murl"|"ou"|imgurl=):?(?:"|%22)?(https?:\\?/\\?/[^"'\\\s<>]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ImgSrcUrls = new(
        """<img\b[^>]*\bsrc\s*=\s*["'](https?://[^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
