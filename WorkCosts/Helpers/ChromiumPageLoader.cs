using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;
using WorkCosts.Services;
using System.Runtime.InteropServices.WindowsRuntime;

namespace WorkCosts.Helpers;

/// <summary>
/// Loads a page in embedded Chromium (WebView2). Autodoc blocks HttpClient,
/// including image downloads, so product photos are taken from this session.
/// </summary>
public sealed class ChromiumPageLoader : IBrowserPageSession, IAsyncDisposable
{
    private readonly Popup _popup;
    private readonly WebView2 _webView;
    private readonly ConcurrentDictionary<string, ProductImageCandidate> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _cdpRequestIds = new(StringComparer.OrdinalIgnoreCase);
    private CoreWebView2DevToolsProtocolEventReceiver? _networkReceiver;
    private bool _disposed;
    private Uri? _pageUri;
    private int _navigationStatus;
    private int _documentStatus;
    private string? _cfMitigated;

    private ChromiumPageLoader(Popup popup, WebView2 webView)
    {
        _popup = popup;
        _webView = webView;
        _webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
    }

    public static async Task<ChromiumPageLoader> CreateAsync(XamlRoot xamlRoot, CancellationToken cancellationToken = default)
    {
        var webView = new WebView2
        {
            Width = 1280,
            Height = 900,
            DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255)
        };

        var popup = new Popup
        {
            XamlRoot = xamlRoot,
            IsHitTestVisible = false,
            HorizontalOffset = 8000,
            VerticalOffset = 0,
            Child = webView
        };
        popup.IsOpen = true;

        try
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            var loader = new ChromiumPageLoader(popup, webView);
            await loader.PrepareNetworkCaptureAsync();
            return loader;
        }
        catch
        {
            popup.IsOpen = false;
            webView.Close();
            throw;
        }
    }

    public async Task<BrowserPageLoad> LoadAsync(Uri pageUri, CancellationToken cancellationToken = default)
    {
        _images.Clear();
        _cdpRequestIds.Clear();
        _pageUri = pageUri;
        _navigationStatus = 0;
        _documentStatus = 0;
        _cfMitigated = null;
        var navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            _navigationStatus = args.HttpStatusCode;
            var challengeStatus = args.HttpStatusCode is 403 or 429 or 503;
            if (args.IsSuccess || challengeStatus)
            {
                navigation.TrySetResult(true);
            }
            else
            {
                navigation.TrySetException(new InvalidOperationException(
                    $"Chromium navigation failed ({args.WebErrorStatus}, HTTP {args.HttpStatusCode})."));
            }
        }

        _webView.CoreWebView2.NavigationCompleted += OnCompleted;
        try
        {
            _webView.CoreWebView2.Navigate(pageUri.ToString());
            await navigation.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellationToken);
        }
        finally
        {
            _webView.CoreWebView2.NavigationCompleted -= OnCompleted;
        }

        await WaitForPageReadyAsync(cancellationToken);

        var encoded = await _webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
        var html = JsonSerializer.Deserialize<string>(encoded) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException("Chromium loaded the URL but returned no HTML.");
        }

        try
        {
            await CollectProductImagesFromDomAsync(cancellationToken);
        }
        catch
        {
            // HTML is still usable; image capture falls back to whatever the network hook stored.
        }

        var status = _navigationStatus > 0 ? _navigationStatus : _documentStatus;
        var images = SelectProductImages();
        return new BrowserPageLoad(html, images, status, _cfMitigated);
    }

    public async Task CopyCookiesToAsync(CookieContainer container, Uri pageUri, CancellationToken cancellationToken = default)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            pageUri.GetLeftPart(UriPartial.Authority) + "/"
        };

        if (ProductPageMetadataParser.IsAutodocHost(pageUri.Host))
        {
            origins.Add("https://www.autodoc.co.uk/");
            origins.Add("https://cdn.autodoc.de/");
            origins.Add("https://media.autodoc.de/");
        }

        foreach (var origin in origins)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(origin);
            foreach (var cookie in cookies)
            {
                try
                {
                    container.Add(new Cookie(cookie.Name, cookie.Value, string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path, cookie.Domain)
                    {
                        Secure = cookie.IsSecure,
                        HttpOnly = cookie.IsHttpOnly
                    });
                }
                catch
                {
                    // Ignore cookies the container will not accept.
                }
            }
        }
    }

    private async Task PrepareNetworkCaptureAsync()
    {
        try
        {
            await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.setCacheDisabled", "{\"cacheDisabled\":true}");
            _networkReceiver = _webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.responseReceived");
            _networkReceiver.DevToolsProtocolEventReceived += OnCdpNetworkResponse;
        }
        catch
        {
            // Older runtimes still work via WebResourceResponseReceived.
        }
    }

    private void OnCdpNetworkResponse(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            var requestId = doc.RootElement.GetProperty("requestId").GetString();
            var url = doc.RootElement.GetProperty("response").GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            _cdpRequestIds[url] = requestId;
            _cdpRequestIds[CanonicalImageUrl(url)] = requestId;
        }
        catch
        {
            // Ignore malformed CDP payloads.
        }
    }

    private async void CoreWebView2_WebResourceResponseReceived(CoreWebView2 sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        try
        {
            var status = args.Response.StatusCode;
            CaptureDocumentResponse(args.Request.Uri, status, args.Response.Headers);
            if (status is < 200 or >= 300)
            {
                return;
            }

            var contentType = string.Empty;
            if (args.Response.Headers.Contains("Content-Type"))
            {
                contentType = args.Response.Headers.GetHeader("Content-Type") ?? string.Empty;
            }

            var requestUrl = args.Request.Uri;
            var looksLikeImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || IsAutodocProductImageUrl(requestUrl);
            if (!looksLikeImage)
            {
                return;
            }

            var content = await args.Response.GetContentAsync();
            if (content is null)
            {
                return;
            }

            using (content)
            using (var input = content.AsStreamForRead())
            using (var memory = new MemoryStream())
            {
                await input.CopyToAsync(memory);
                TryStoreImage(requestUrl, memory.ToArray(), contentType);
            }
        }
        catch
        {
            // Cached or empty responses cannot always be read.
        }
    }

    private void CaptureDocumentResponse(string requestUri, int status, CoreWebView2HttpResponseHeaders headers)
    {
        if (_pageUri is null || !Uri.TryCreate(requestUri, UriKind.Absolute, out var uri))
        {
            return;
        }

        var sameHost = string.Equals(uri.Host, _pageUri.Host, StringComparison.OrdinalIgnoreCase);
        if (!sameHost)
        {
            return;
        }

        if (headers.Contains("cf-mitigated"))
        {
            _cfMitigated = headers.GetHeader("cf-mitigated");
        }

        var looksLikeDocument = string.Equals(uri.GetLeftPart(UriPartial.Path), _pageUri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase)
            || headers.Contains("Content-Type")
                && (headers.GetHeader("Content-Type") ?? string.Empty)
                    .StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        if (looksLikeDocument)
        {
            _documentStatus = status;
        }
    }

    private async Task WaitForPageReadyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var encoded = await _webView.CoreWebView2.ExecuteScriptAsync("""
                (() => {
                  const text = document.body ? document.body.innerText : '';
                  if (/just a moment|checking your browser|attention required|access denied/i.test(text)
                      && text.length < 4000) {
                    return 'challenge';
                  }
                  if (document.querySelector('h1.product-block__title, h1.listing-title__name, [data-product-page], .listing-page, .product-block')) {
                    return 'ready';
                  }
                  if (document.querySelector('script[type="application/ld+json"]')) {
                    return 'ready';
                  }
                  return 'wait';
                })()
                """);
            var state = JsonSerializer.Deserialize<string>(encoded);
            if (state == "ready")
            {
                await Task.Delay(400, cancellationToken);
                return;
            }

            if (state == "challenge" && DateTime.UtcNow.AddSeconds(8) > deadline)
            {
                break;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private async Task CollectProductImagesFromDomAsync(CancellationToken cancellationToken)
    {
        await _webView.CoreWebView2.ExecuteScriptAsync("""
            (() => {
              const gallery = document.querySelector('.product-gallery');
              gallery?.scrollIntoView({ block: 'center' });
              document.querySelectorAll('.product-gallery img, img.lazyload, img[data-srcset], img[data-src]').forEach(img => {
                const srcset = img.getAttribute('data-srcset');
                const src = img.getAttribute('data-src');
                if (srcset) img.setAttribute('srcset', srcset);
                if (src) img.src = src;
                img.classList.remove('lazyload');
                img.classList.add('lazyloaded');
              });
              return true;
            })()
            """);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var encoded = await _webView.CoreWebView2.ExecuteScriptAsync("""
                [...document.querySelectorAll('.product-gallery img')]
                  .filter(img => {
                    const src = img.currentSrc || img.src || '';
                    return img.naturalWidth >= 80 && (src.includes('cdn.autodoc.') || src.includes('/thumb'));
                  })
                  .length
                """);
            var loaded = 0;
            try
            {
                loaded = JsonSerializer.Deserialize<int>(encoded);
            }
            catch
            {
                loaded = 0;
            }

            if (loaded > 0)
            {
                break;
            }

            await Task.Delay(250, cancellationToken);
        }

        var urls = await ReadProductImageUrlsAsync();
        var missing = urls.Where(url => !HasImage(url)).ToList();
        if (missing.Count > 0)
        {
            await ForceReloadImagesAsync(missing);
        }

        var waitUntil = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < waitUntil && urls.Exists(url => !HasImage(url)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TryFillFromDevToolsAsync(urls.Where(url => !HasImage(url)));
            if (urls.TrueForAll(HasImage))
            {
                break;
            }

            await Task.Delay(200, cancellationToken);
        }
    }

    private async Task<List<string>> ReadProductImageUrlsAsync()
    {
        var encoded = await _webView.CoreWebView2.ExecuteScriptAsync("""
            (() => {
              const urls = [];
              const add = (value) => {
                if (!value || typeof value !== 'string') return;
                try {
                  const abs = new URL(value, location.href);
                  const href = abs.href;
                  if (!abs.hostname.toLowerCase().includes('autodoc.')) return;
                  const path = abs.pathname + abs.search;
                  if (path.includes('/brands/') || path.includes('/static/') || path.includes('lazyload') || path.includes('.svg')) return;
                  if (!path.includes('/thumb') && !abs.hostname.toLowerCase().includes('cdn.autodoc.')) return;
                  const preferred = href.replace(/([?&])m=1(?=&|$)/, '$1m=0');
                  if (!urls.includes(preferred)) urls.push(preferred);
                } catch {}
              };
              document.querySelectorAll('script[type="application/ld+json"]').forEach(script => {
                try {
                  const walk = (node) => {
                    if (!node || typeof node !== 'object') return;
                    if (Array.isArray(node)) { node.forEach(walk); return; }
                    if (typeof node.image === 'string') add(node.image);
                    else if (Array.isArray(node.image)) {
                      node.image.forEach(item => add(typeof item === 'string' ? item : item?.url));
                    }
                    Object.values(node).forEach(walk);
                  };
                  walk(JSON.parse(script.textContent));
                } catch {}
              });
              add(document.querySelector('meta[property="og:image"]')?.content);
              document.querySelectorAll('.product-gallery img').forEach(img => {
                add(img.currentSrc);
                add(img.src);
                const srcset = img.getAttribute('srcset') || img.getAttribute('data-srcset') || '';
                srcset.split(',').forEach(part => add(part.trim().split(/\\s+/)[0]));
              });
              return urls.slice(0, 12);
            })()
            """);

        try
        {
            return JsonSerializer.Deserialize<List<string>>(encoded) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task ForceReloadImagesAsync(IReadOnlyList<string> urls)
    {
        var payload = JsonSerializer.Serialize(urls);
        await _webView.CoreWebView2.ExecuteScriptAsync(
            $$"""
            (async () => {
              const urls = {{payload}};
              await Promise.all(urls.map(url => new Promise(resolve => {
                const img = new Image();
                const timer = setTimeout(() => resolve(false), 6000);
                img.onload = () => { clearTimeout(timer); resolve(true); };
                img.onerror = () => { clearTimeout(timer); resolve(false); };
                img.src = url + (url.includes('?') ? '&' : '?') + '_wc=' + Date.now();
              })));
              return true;
            })()
            """);
    }

    private async Task TryFillFromDevToolsAsync(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            if (HasImage(url))
            {
                continue;
            }

            if (!_cdpRequestIds.TryGetValue(url, out var requestId)
                && !_cdpRequestIds.TryGetValue(CanonicalImageUrl(url), out requestId))
            {
                var match = _cdpRequestIds.FirstOrDefault(pair =>
                    CanonicalImageUrl(pair.Key) == CanonicalImageUrl(url));
                if (match.Value is null)
                {
                    continue;
                }

                requestId = match.Value;
            }

            try
            {
                var json = await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Network.getResponseBody",
                    JsonSerializer.Serialize(new Dictionary<string, string> { ["requestId"] = requestId }));
                using var doc = JsonDocument.Parse(json);
                var body = doc.RootElement.GetProperty("body").GetString();
                if (string.IsNullOrWhiteSpace(body))
                {
                    continue;
                }

                var bytes = doc.RootElement.GetProperty("base64Encoded").GetBoolean()
                    ? Convert.FromBase64String(body)
                    : System.Text.Encoding.UTF8.GetBytes(body);
                TryStoreImage(url, bytes, "image/jpeg");
            }
            catch
            {
                // Body is unavailable for some requests.
            }
        }
    }

    private bool HasImage(string url)
    {
        var canonical = CanonicalImageUrl(url);
        return _images.ContainsKey(url)
            || _images.ContainsKey(canonical)
            || _images.Keys.Any(key => CanonicalImageUrl(key) == canonical);
    }

    private void TryStoreImage(string url, byte[] bytes, string contentType)
    {
        if (bytes.Length < 2_048 || bytes.Length > 8_000_000)
        {
            return;
        }

        if (!LooksLikeImageBytes(bytes)
            && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = SniffImageContentType(bytes);
        }

        var canonical = CanonicalImageUrl(url);
        var item = new ProductImageCandidate
        {
            SourceUrl = canonical,
            Bytes = bytes,
            ContentType = mediaType
        };
        _images[canonical] = item;
        _images[url] = item;
    }

    private IReadOnlyList<ProductImageCandidate> SelectProductImages()
    {
        var product = _images.Values
            .DistinctBy(image => CanonicalImageUrl(image.SourceUrl), StringComparer.OrdinalIgnoreCase)
            .Where(image => IsAutodocProductImageUrl(image.SourceUrl))
            .ToList();
        if (product.Count > 0)
        {
            return product;
        }

        return _images.Values
            .DistinctBy(image => CanonicalImageUrl(image.SourceUrl), StringComparer.OrdinalIgnoreCase)
            .Where(image => image.Bytes.Length >= 8_000)
            .Take(12)
            .ToList();
    }

    private static bool IsAutodocProductImageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Contains("autodoc.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var pathAndQuery = uri.AbsolutePath + uri.Query;
        if (pathAndQuery.Contains("/brands/", StringComparison.OrdinalIgnoreCase)
            || pathAndQuery.Contains("/static/", StringComparison.OrdinalIgnoreCase)
            || pathAndQuery.Contains("lazyload", StringComparison.OrdinalIgnoreCase)
            || pathAndQuery.Contains(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return pathAndQuery.Contains("/thumb", StringComparison.OrdinalIgnoreCase)
            || uri.Host.StartsWith("cdn.autodoc.", StringComparison.OrdinalIgnoreCase)
            || uri.Host.StartsWith("media.autodoc.", StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalImageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        var query = uri.Query.TrimStart('?');
        var kept = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith("_wc=", StringComparison.OrdinalIgnoreCase));
        builder.Query = string.Join('&', kept);
        return builder.Uri.ToString();
    }

    private static bool LooksLikeImageBytes(byte[] bytes)
    {
        if (bytes.Length < 12)
        {
            return false;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return true;
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return true;
        }

        if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return true;
        }

        return false;
    }

    private static string SniffImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[8] == (byte)'W')
        {
            return "image/webp";
        }

        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50)
        {
            return "image/png";
        }

        return "image/jpeg";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _webView.CoreWebView2.WebResourceResponseReceived -= CoreWebView2_WebResourceResponseReceived;
            if (_networkReceiver is not null)
            {
                _networkReceiver.DevToolsProtocolEventReceived -= OnCdpNetworkResponse;
            }
        }
        catch
        {
            // Control may already be closed.
        }

        _popup.IsOpen = false;
        _webView.Close();
        await Task.CompletedTask;
    }
}
