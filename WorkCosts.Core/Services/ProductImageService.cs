using System.Collections.Concurrent;
using System.Net;
using AngleSharp;
using AngleSharp.Html.Dom;
using WorkCosts.Helpers;

namespace WorkCosts.Services;

public sealed class ProductImageCandidate
{
    public required string SourceUrl { get; init; }
    public required byte[] Bytes { get; init; }
    public required string ContentType { get; init; }
}

public sealed record ProductPageLoadResult(
    ProductPageMetadata Metadata,
    IReadOnlyList<ProductImageCandidate> Images,
    string Html);

public sealed record BrowserPageLoad(
    string Html,
    IReadOnlyList<ProductImageCandidate> Images,
    int HttpStatusCode,
    string? CfMitigated);

public interface IBrowserPageSession
{
    Task<BrowserPageLoad> LoadAsync(Uri pageUri, CancellationToken cancellationToken = default);
    Task CopyCookiesToAsync(CookieContainer container, Uri pageUri, CancellationToken cancellationToken = default);
}

public sealed class ProductImageService
{
    /// <summary>Desktop Chrome identity used on every outbound request.</summary>
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36";

    private const string ChromeSecChUa =
        "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"150\", \"Google Chrome\";v=\"150\"";
    private const string ChromeAcceptHtml =
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7";
    private const string ChromeAcceptImage =
        "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8";
    private const string ChromeAcceptLanguage = "en-GB,en-US;q=0.9,en;q=0.8";
    private const string ChromeAcceptEncoding = "gzip, deflate, br";

    private static readonly CookieContainer Cookies = new();
    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, CachedPage> PageCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, ProductImageCandidate> ImageCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly WebCacheStore _cache;

    public ProductImageService(string? pageCacheDirectory = null, DatabaseService? database = null)
    {
        var root = pageCacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkCosts",
            "page-cache");
        _cache = new WebCacheStore(root, database);
    }

    public string PageCacheDirectory => _cache.Root;

    /// <summary>True when this URL already has HTML (and possibly images) in memory or on disk.</summary>
    public bool IsCached(string pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)
            || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var cacheKey = ProductUrl.Normalize(pageUri);
        if (PageCache.ContainsKey(cacheKey))
        {
            return true;
        }

        return _cache.HtmlExists(pageUri, cacheKey);
    }

    /// <summary>True when HTML and chooser images are already on disk for this product URL.</summary>
    public async Task<bool> CanServeFromCacheAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)
            || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var cacheKey = ProductUrl.Normalize(pageUri);
        if (PageCache.TryGetValue(cacheKey, out var memory)
            && !string.IsNullOrWhiteSpace(memory.Html)
            && memory.Images.Count > 0)
        {
            return true;
        }

        var html = await _cache.TryReadHtmlAsync(pageUri, cacheKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(html) || !IsUsablePageHtml(html, pageUri))
        {
            return false;
        }

        var images = await _cache.TryReadImagesAsync(cacheKey, cancellationToken);
        return images.Count > 0;
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 6,
            UseCookies = true,
            CookieContainer = Cookies,
            AllowAutoRedirect = true,
            EnableMultipleHttp2Connections = true
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        client.DefaultRequestHeaders.ExpectContinue = false;
        ApplyChromeIdentity(client.DefaultRequestHeaders);
        return client;
    }

    private static void ApplyChromeIdentity(System.Net.Http.Headers.HttpRequestHeaders headers)
    {
        headers.TryAddWithoutValidation("User-Agent", UserAgent);
        headers.TryAddWithoutValidation("Accept-Language", ChromeAcceptLanguage);
        headers.TryAddWithoutValidation("Accept-Encoding", ChromeAcceptEncoding);
        headers.TryAddWithoutValidation("sec-ch-ua", ChromeSecChUa);
        headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
    }

    /// <summary>Headers currently applied on document (HTML) fetches.</summary>
    public static IReadOnlyDictionary<string, string> DescribeDocumentHeaders(string pageUrl) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = UserAgent,
            ["Accept"] = ChromeAcceptHtml,
            ["Accept-Language"] = ChromeAcceptLanguage,
            ["Accept-Encoding"] = ChromeAcceptEncoding,
            ["Upgrade-Insecure-Requests"] = "1",
            ["sec-ch-ua"] = ChromeSecChUa,
            ["sec-ch-ua-mobile"] = "?0",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["Sec-Fetch-Dest"] = "document",
            ["Sec-Fetch-Mode"] = "navigate",
            ["Sec-Fetch-Site"] = "same-origin",
            ["Sec-Fetch-User"] = "?1",
            ["Priority"] = "u=0, i"
        };

    /// <summary>Headers currently applied on image fetches.</summary>
    public static IReadOnlyDictionary<string, string> DescribeImageHeaders(string pageUrl) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = UserAgent,
            ["Accept"] = ChromeAcceptImage,
            ["Accept-Language"] = ChromeAcceptLanguage,
            ["Accept-Encoding"] = ChromeAcceptEncoding,
            ["sec-ch-ua"] = ChromeSecChUa,
            ["sec-ch-ua-mobile"] = "?0",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["Referer"] = pageUrl,
            ["Sec-Fetch-Dest"] = "image",
            ["Sec-Fetch-Mode"] = "no-cors",
            ["Sec-Fetch-Site"] = "cross-site"
        };

    public async Task<ProductPageLoadResult> LoadPageAsync(
        string pageUrl,
        IBrowserPageSession? browser = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)
            || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Enter a valid http(s) product URL.");
        }

        var cacheKey = ProductUrl.Normalize(pageUri);
        IReadOnlyList<ProductImageCandidate> capturedImages = [];
        var diskImages = await _cache.TryReadImagesAsync(cacheKey, cancellationToken);
        var diskHtml = await _cache.TryReadHtmlAsync(pageUri, cacheKey, cancellationToken);
        string html;
        if (browser is not null && ProductPageMetadataParser.IsAutodocHost(pageUri.Host))
        {
            if (!string.IsNullOrWhiteSpace(diskHtml)
                && IsUsablePageHtml(diskHtml, pageUri)
                && diskImages.Count > 0)
            {
                html = diskHtml;
                capturedImages = diskImages;
            }
            else
            {
                var loaded = await browser.LoadAsync(pageUri, cancellationToken);
                html = loaded.Html;
                capturedImages = loaded.Images;
                if (!IsUsablePageHtml(html, pageUri))
                {
                    throw new InvalidOperationException(
                        FormatUnusablePageMessage("Autodoc", loaded.HttpStatusCode, loaded.CfMitigated, html, inChromium: true));
                }

                await browser.CopyCookiesToAsync(Cookies, pageUri, cancellationToken);
                await _cache.SaveHtmlAsync(pageUri, cacheKey, html, cancellationToken);
            }
        }
        else
        {
            html = await LoadHtmlAsync(pageUri, cacheKey, cancellationToken);
        }

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html).Address(pageUri.ToString()), cancellationToken);
        var metadata = ProductPageMetadataParser.Parse(document, pageUri);

        IReadOnlyList<ProductImageCandidate>? cachedImages = null;
        if (PageCache.TryGetValue(cacheKey, out var memoryPage) && memoryPage.Images.Count > 0)
        {
            cachedImages = memoryPage.Images;
        }

        List<ProductImageCandidate> results;
        if (cachedImages is not null)
        {
            results = cachedImages.ToList();
        }
        else if (diskImages.Count > 0)
        {
            results = diskImages.ToList();
            foreach (var image in results)
            {
                ImageCache[image.SourceUrl] = image;
            }
        }
        else if (capturedImages.Count > 0)
        {
            results = capturedImages.ToList();
            foreach (var image in results)
            {
                ImageCache[image.SourceUrl] = image;
            }
        }
        else if (ProductPageMetadataParser.IsAutodocHost(pageUri.Host))
        {
            throw new InvalidOperationException(
                "The Autodoc page loaded, but product images could not be read from Chromium. Autodoc blocks a separate HTTP download of those images.");
        }
        else
        {
            var imageUrls = new List<string>();
            foreach (var img in document.Images.OfType<IHtmlImageElement>())
            {
                foreach (var candidate in EnumerateImageUrls(img, pageUri))
                {
                    if (!imageUrls.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    {
                        imageUrls.Add(candidate);
                    }
                }
            }

            if (imageUrls.Count == 0)
            {
                throw new InvalidOperationException("No images were found on that page.");
            }

            results = await DownloadImagesAsync(imageUrls, pageUri.ToString(), cancellationToken);
            if (results.Count == 0)
            {
                throw new InvalidOperationException("Images were found but none could be downloaded.");
            }
        }

        await _cache.SaveHtmlAsync(pageUri, cacheKey, html, cancellationToken);
        await _cache.SaveImagesAsync(pageUri, cacheKey, results, cancellationToken);
        PageCache[cacheKey] = new CachedPage(html, results);
        return new ProductPageLoadResult(metadata, results, html);
    }

    /// <summary>
    /// Parses supplied page HTML (paste or file). Never starts Chromium.
    /// Images are optional: HttpClient downloads of &lt;img&gt; / OG URLs may return none.
    /// </summary>
    public async Task<ProductPageLoadResult> LoadFromHtmlAsync(
        string pageUrl,
        string html,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)
            || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Enter a valid http(s) product URL.");
        }

        if (!IsUsablePageHtml(html, pageUri))
        {
            throw new InvalidOperationException(FormatUnusablePageMessage(pageUri, html));
        }

        var cacheKey = ProductUrl.Normalize(pageUri);
        var metadata = await ProductPageMetadataParser.ParseHtmlAsync(html, pageUrl);

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html).Address(pageUri.ToString()), cancellationToken);
        var imageUrls = CollectHtmlImageUrls(document, pageUri);
        var results = imageUrls.Count == 0
            ? []
            : await DownloadImagesAsync(imageUrls, pageUri.ToString(), cancellationToken);

        await _cache.SaveHtmlAsync(pageUri, cacheKey, html, cancellationToken);
        await _cache.SaveImagesAsync(pageUri, cacheKey, results, cancellationToken);
        PageCache[cacheKey] = new CachedPage(html, results);
        return new ProductPageLoadResult(metadata, results, html);
    }

    private async Task<string> LoadHtmlAsync(Uri pageUri, string cacheKey, CancellationToken cancellationToken)
    {
        if (PageCache.TryGetValue(cacheKey, out var memory) && !string.IsNullOrWhiteSpace(memory.Html))
        {
            return memory.Html;
        }

        var diskPath = _cache.PageFilePath(pageUri, cacheKey);
        var cachedHtml = await _cache.TryReadHtmlAsync(pageUri, cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedHtml))
        {
            if (IsUsablePageHtml(cachedHtml, pageUri))
            {
                return cachedHtml;
            }

            try
            {
                File.Delete(diskPath);
            }
            catch
            {
                // Continue to a live fetch.
            }
        }

        using var pageRequest = CreateDocumentRequest(pageUri);
        using var response = await Http.SendAsync(pageRequest, cancellationToken);
        var status = (int)response.StatusCode;
        var cfMitigated = ReadHeader(response, "cf-mitigated");
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not load page (HTTP {status} {response.ReasonPhrase}). The site may be blocking automated requests.");
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!IsUsablePageHtml(html, pageUri))
        {
            throw new InvalidOperationException(
                FormatUnusablePageMessage(
                    ProductPageMetadataParser.IsAutodocHost(pageUri.Host) ? "Autodoc" : "The site",
                    status,
                    cfMitigated,
                    html,
                    inChromium: false));
        }

        await _cache.SaveHtmlAsync(pageUri, cacheKey, html, cancellationToken);
        return html;
    }

    public static bool IsUsablePageHtml(string html, Uri pageUri)
    {
        if (string.IsNullOrWhiteSpace(html) || html.Length < 800)
        {
            return false;
        }

        var autodoc = ProductPageMetadataParser.IsAutodocHost(pageUri.Host);
        // Autodoc's <head> alone is ~95k; product-block / JSON-LD sit after 130k.
        // Searching only a 12k prefix falsely rejects a real HTTP 200 product page.
        if (autodoc && HasAutodocMarkup(html))
        {
            return true;
        }

        if (LooksLikeChallengeHtml(html))
        {
            return false;
        }

        return !autodoc;
    }

    private static bool HasAutodocMarkup(string html) =>
        html.Contains("product-block", StringComparison.OrdinalIgnoreCase)
        || html.Contains("listing-page", StringComparison.OrdinalIgnoreCase)
        || html.Contains("listing-title__name", StringComparison.OrdinalIgnoreCase)
        || html.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeChallengeHtml(string html)
    {
        var prefix = html.Length > 24_000 ? html[..24_000] : html;
        return prefix.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains("cf-mitigated", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains("Enable JavaScript and cookies to continue", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatUnusablePageMessage(Uri pageUri, string html) =>
        FormatUnusablePageMessage(
            ProductPageMetadataParser.IsAutodocHost(pageUri.Host) ? "Autodoc" : "The site",
            httpStatus: 0,
            cfMitigated: null,
            html,
            inChromium: false);

    public static string FormatUnusablePageMessage(
        string site,
        int httpStatus,
        string? cfMitigated,
        string html,
        bool inChromium)
    {
        var statusText = httpStatus > 0 ? $"HTTP {httpStatus}" : "no HTTP status";
        var mitigated = string.IsNullOrWhiteSpace(cfMitigated)
            ? string.Empty
            : $", cf-mitigated: {cfMitigated}";
        var where = inChromium ? " from Chromium" : string.Empty;
        var hint = inChromium
            ? "Try again in a few seconds."
            : "Autodoc needs the in-app Chromium loader.";
        return $"{site} did not return a usable product page{where} ({statusText}{mitigated}; {DescribeUnusableReason(html)}). {hint}";
    }

    private static string DescribeUnusableReason(string html)
    {
        if (string.IsNullOrWhiteSpace(html) || html.Length < 800)
        {
            return $"HTML was too short ({html?.Length ?? 0} characters)";
        }

        if (LooksLikeChallengeHtml(html))
        {
            return "the body looks like a bot-check interstitial";
        }

        return "Autodoc product markup was missing";
    }

    private static string? ReadHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            return values.FirstOrDefault();
        }

        if (response.Content.Headers.TryGetValues(name, out values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private async Task<List<ProductImageCandidate>> DownloadImagesAsync(
        IReadOnlyList<string> imageUrls,
        string pageUrlString,
        CancellationToken cancellationToken)
    {
        var results = new List<ProductImageCandidate>();
        foreach (var imageUrl in imageUrls.Take(40))
        {
            try
            {
                if (ImageCache.TryGetValue(imageUrl, out var cachedImage))
                {
                    results.Add(cachedImage);
                    if (results.Count >= 24)
                    {
                        break;
                    }

                    continue;
                }

                using var imageRequest = CreateImageRequest(imageUrl, pageUrlString);
                using var imageResponse = await Http.SendAsync(imageRequest, cancellationToken);
                if (!imageResponse.IsSuccessStatusCode)
                {
                    continue;
                }

                var contentType = imageResponse.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var bytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                if (bytes.Length < 2_048 || bytes.Length > 8_000_000)
                {
                    continue;
                }

                var item = new ProductImageCandidate
                {
                    SourceUrl = imageUrl,
                    Bytes = bytes,
                    ContentType = contentType
                };
                ImageCache[imageUrl] = item;
                results.Add(item);

                if (results.Count >= 24)
                {
                    break;
                }
            }
            catch
            {
                // Skip images that fail to download.
            }
        }

        return results;
    }

    /// <summary>Backward-compatible helper for callers that only need images.</summary>
    public async Task<IReadOnlyList<ProductImageCandidate>> LoadCandidatesFromPageAsync(
        string pageUrl,
        CancellationToken cancellationToken = default)
    {
        var page = await LoadPageAsync(pageUrl, browser: null, cancellationToken);
        return page.Images;
    }

    private static HttpRequestMessage CreateDocumentRequest(Uri pageUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, pageUri)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        request.Headers.TryAddWithoutValidation("Accept", ChromeAcceptHtml);
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        request.Headers.TryAddWithoutValidation("Priority", "u=0, i");
        request.Headers.TryAddWithoutValidation("Referer", pageUri.GetLeftPart(UriPartial.Authority) + "/");
        return request;
    }

    private static HttpRequestMessage CreateImageRequest(string imageUrl, string pageUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, imageUrl)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        request.Headers.TryAddWithoutValidation("Accept", ChromeAcceptImage);
        request.Headers.TryAddWithoutValidation("Referer", pageUrl);
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "image");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
        var sameHost = Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)
            && Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri)
            && string.Equals(pageUri.Host, imageUri.Host, StringComparison.OrdinalIgnoreCase);
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", sameHost ? "same-origin" : "cross-site");
        return request;
    }

    public PageCacheInfo GetPageCacheInfo() => _cache.GetTotals();

    public IReadOnlyList<DomainCacheSummary> GetDomainCacheSummaries() => _cache.GetDomainSummaries();

    public async Task<int> ClearSelectedCacheAsync(IReadOnlyCollection<string> domains, bool includeImages)
    {
        PageCache.Clear();
        ImageCache.Clear();
        return await _cache.ClearAsync(domains, includeImages);
    }

    public async Task<int> ClearPageCacheAsync()
    {
        var domains = _cache.GetDomainSummaries().Select(d => d.Domain).ToList();
        return await ClearSelectedCacheAsync(domains, includeImages: false);
    }

    private sealed record CachedPage(string Html, IReadOnlyList<ProductImageCandidate> Images);

    private static List<string> CollectHtmlImageUrls(AngleSharp.Dom.IDocument document, Uri pageUri)
    {
        var imageUrls = new List<string>();
        foreach (var img in document.Images.OfType<IHtmlImageElement>())
        {
            foreach (var candidate in EnumerateImageUrls(img, pageUri))
            {
                if (!imageUrls.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    imageUrls.Add(candidate);
                }
            }
        }

        foreach (var meta in document.QuerySelectorAll("meta[property='og:image'], meta[name='og:image']"))
        {
            var content = meta.GetAttribute("content");
            if (string.IsNullOrWhiteSpace(content)
                || !TryMakeAbsolute(content, pageUri, out var ogUrl)
                || imageUrls.Contains(ogUrl, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            imageUrls.Add(ogUrl);
        }

        return imageUrls;
    }

    private static IEnumerable<string> EnumerateImageUrls(IHtmlImageElement img, Uri pageUri)
    {
        if (!string.IsNullOrWhiteSpace(img.Source)
            && TryMakeAbsolute(img.Source, pageUri, out var src))
        {
            yield return src;
        }

        var srcset = img.GetAttribute("srcset");
        if (string.IsNullOrWhiteSpace(srcset))
        {
            yield break;
        }

        foreach (var part in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var urlPart = part.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(urlPart)
                && TryMakeAbsolute(urlPart, pageUri, out var absolute))
            {
                yield return absolute;
                yield break;
            }
        }
    }

    private static bool TryMakeAbsolute(string value, Uri pageUri, out string absolute)
    {
        absolute = string.Empty;
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(pageUri, value, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        absolute = uri.ToString();
        return true;
    }
}

public sealed record PageCacheInfo(
    string Directory,
    int FileCount,
    long TotalBytes,
    int ImageCount = 0,
    long ImageBytes = 0);
