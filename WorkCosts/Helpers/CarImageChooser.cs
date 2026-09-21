using WorkCosts.Services;

namespace WorkCosts.Helpers;

public sealed record CarImageChooserResult(ProductImageCandidate? Chosen, string? Error, bool Cancelled);

public static class CarImageChooser
{
    public static async Task<CarImageChooserResult> SearchAsync(
        Microsoft.UI.Xaml.XamlRoot xamlRoot,
        string make,
        string modelNumber,
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> urls;
        try
        {
            urls = await CarImageSearch.FindImageUrlsAsync(
                make,
                modelNumber,
                (url, token) => FetchPageAsync(xamlRoot, url, status, token),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CarImageChooserResult(null, string.IsNullOrWhiteSpace(ex.Message)
                ? "Image search failed. Try again or choose a file."
                : ex.Message, false);
        }

        if (urls.Count == 0)
        {
            return new CarImageChooserResult(null, null, false);
        }

        status?.Invoke("Downloading photos…");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ProductImageService.UserAgent);
        var candidates = await CarImageSearch.DownloadCandidatesAsync(http, urls, cancellationToken);
        if (candidates.Count == 0)
        {
            return new CarImageChooserResult(null, null, false);
        }

        if (candidates.Count == 1)
        {
            return new CarImageChooserResult(candidates[0], null, false);
        }

        var chosen = await ProductImagePicker.ChooseFromCandidatesAsync(xamlRoot, candidates, "Select a photo");
        return new CarImageChooserResult(chosen, null, chosen is null);
    }

    private static async Task<CarImageSearch.Page> FetchPageAsync(
        Microsoft.UI.Xaml.XamlRoot xamlRoot,
        string url,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        var host = "Bing";
        if (Uri.TryCreate(url, UriKind.Absolute, out var pageUri)
            && pageUri.Host.Contains("google.", StringComparison.OrdinalIgnoreCase))
        {
            host = "Google";
        }

        status?.Invoke($"Searching {host}…");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ProductImageService.UserAgent);
            using var response = await http.GetAsync(url, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!CarImageSearch.IsChallenge((int)response.StatusCode, html))
            {
                return new CarImageSearch.Page(html, (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }

        status?.Invoke($"Opening {host} in the browser…");
        await using var browser = await ChromiumPageLoader.CreateAsync(xamlRoot, cancellationToken);
        var loaded = await browser.LoadAsync(new Uri(url), cancellationToken);
        return new CarImageSearch.Page(loaded.Html, loaded.HttpStatusCode);
    }
}
