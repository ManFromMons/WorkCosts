using WorkCosts.Helpers;
using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public sealed class LoadFromHtmlAsyncTests : IAsyncLifetime
{
    private string _root = null!;
    private DatabaseService _database = null!;
    private ProductImageService _images = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "workcosts_pastehtml_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _database = new DatabaseService(Path.Combine(_root, "workcosts.db"));
        await _database.InitializeAsync();
        _images = new ProductImageService(_root, _database);
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Temp leftovers are acceptable after file locks.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void LoadFromHtmlAsync_does_not_take_a_browser_session()
    {
        var method = typeof(ProductImageService).GetMethod(nameof(ProductImageService.LoadFromHtmlAsync));
        Assert.NotNull(method);
        Assert.DoesNotContain(method!.GetParameters(), p => p.ParameterType == typeof(IBrowserPageSession));
    }

    [Fact]
    public async Task Amazon_fixture_caches_without_a_browser()
    {
        var product = AmazonProductCases.All[0];
        var html = await File.ReadAllTextAsync(product.FixturePath);

        var loaded = await _images.LoadFromHtmlAsync(product.Url, html);

        Assert.Equal(product.Name, loaded.Metadata.Name);
        Assert.Equal("Amazon", loaded.Metadata.Source);
        Assert.True(_images.IsCached(product.Url));
        Assert.True(HtmlFileExists(product.Url));
    }

    [Fact]
    public async Task Autodoc_fixture_caches_without_a_browser()
    {
        var product = AutodocProductCases.All[0];
        var html = await File.ReadAllTextAsync(product.FixturePath);

        var loaded = await _images.LoadFromHtmlAsync(product.Url, html);

        Assert.Equal(product.Name, loaded.Metadata.Name);
        Assert.Equal("Autodoc", loaded.Metadata.Source);
        Assert.True(_images.IsCached(product.Url));
        Assert.True(HtmlFileExists(product.Url));
    }

    [Fact]
    public async Task Challenge_html_throws_unusable_page_error()
    {
        var html = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "challenge-just-a-moment.snippet.html"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _images.LoadFromHtmlAsync("https://www.autodoc.co.uk/connect/challenge", html));

        Assert.Contains("usable product page", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(_images.IsCached("https://www.autodoc.co.uk/connect/challenge"));
    }

    private bool HtmlFileExists(string pageUrl)
    {
        Assert.True(Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri));
        var cache = new WebCacheStore(_root, _database);
        return cache.HtmlExists(pageUri, ProductUrl.Normalize(pageUri));
    }
}
