using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Services;

public sealed record DomainCacheSummary(
    string Domain,
    int PageCount,
    long PageBytes,
    int ImageCount,
    long ImageBytes);

public sealed class WebCacheStore
{
    public const string LegacyDomain = "legacy";
    private const string PagesFolder = "pages";
    private const string ImagesFolder = "images";

    private readonly string _root;
    private readonly DatabaseService _database;

    public WebCacheStore(string rootDirectory, DatabaseService? database = null)
    {
        _root = rootDirectory;
        _database = database ?? new DatabaseService();
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public string DomainFolder(string domain) => Path.Combine(_root, SanitizeSegment(domain));

    public string PageFilePath(Uri pageUri, string cacheKey) =>
        Path.Combine(DomainFolder(Host(pageUri)), PagesFolder, FileSlug(pageUri.AbsolutePath, cacheKey) + ".html");

    public string ImageFilePath(string pageDomain, string imageUrl, string contentType)
    {
        var ext = Extension(contentType, imageUrl);
        var hint = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath + uri.Query
            : imageUrl;
        return Path.Combine(DomainFolder(pageDomain), ImagesFolder, FileSlug(hint, imageUrl) + ext);
    }

    public string LegacyPagePath(string cacheKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey.ToLowerInvariant())));
        return Path.Combine(_root, hash + ".html");
    }

    public async Task SaveHtmlAsync(Uri pageUri, string cacheKey, string html, CancellationToken cancellationToken)
    {
        var path = PageFilePath(pageUri, cacheKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, html, cancellationToken);
        var relative = Path.GetRelativePath(_root, path);
        var domain = Host(pageUri);
        await using var db = _database.CreateContext();
        var row = await db.CachedWebPages.FirstOrDefaultAsync(p => p.PageUrl == cacheKey, cancellationToken);
        if (row is null)
        {
            db.CachedWebPages.Add(new CachedWebPage
            {
                PageUrl = cacheKey,
                Domain = domain,
                RelativePath = relative,
                ByteSize = new FileInfo(path).Length,
                CachedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            row.Domain = domain;
            row.RelativePath = relative;
            row.ByteSize = new FileInfo(path).Length;
            row.CachedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveImagesAsync(
        Uri pageUri,
        string cacheKey,
        IReadOnlyList<ProductImageCandidate> images,
        CancellationToken cancellationToken)
    {
        var domain = Host(pageUri);
        var saved = new List<(string ImageUrl, string RelativePath, string ContentType, long ByteSize)>();
        foreach (var image in images)
        {
            var path = ImageFilePath(domain, image.SourceUrl, image.ContentType);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, image.Bytes, cancellationToken);
            saved.Add((image.SourceUrl, Path.GetRelativePath(_root, path), image.ContentType, image.Bytes.Length));
        }

        await using var db = _database.CreateContext();
        var existing = await db.CachedWebImages
            .Where(i => i.PageUrl == cacheKey)
            .ToListAsync(cancellationToken);
        var keep = saved.Select(item => item.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var old in existing)
        {
            if (keep.Contains(old.RelativePath))
            {
                continue;
            }

            var oldPath = Path.Combine(_root, old.RelativePath);
            try
            {
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
            }
            catch
            {
                // Leave locked files.
            }
        }

        db.CachedWebImages.RemoveRange(existing);

        foreach (var item in saved)
        {
            db.CachedWebImages.Add(new CachedWebImage
            {
                PageUrl = cacheKey,
                ImageUrl = item.ImageUrl,
                Domain = domain,
                RelativePath = item.RelativePath,
                ContentType = item.ContentType,
                ByteSize = item.ByteSize,
                CachedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> TryReadHtmlAsync(Uri pageUri, string cacheKey, CancellationToken cancellationToken)
    {
        foreach (var path in HtmlCandidatePaths(pageUri, cacheKey))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch
            {
                // Try the next candidate.
            }
        }

        await using var db = _database.CreateContext();
        var row = await db.CachedWebPages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PageUrl == cacheKey, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var mapped = Path.Combine(_root, row.RelativePath);
        if (!File.Exists(mapped))
        {
            return null;
        }

        return await File.ReadAllTextAsync(mapped, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductImageCandidate>> TryReadImagesAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        await using var db = _database.CreateContext();
        var rows = await db.CachedWebImages.AsNoTracking()
            .Where(i => i.PageUrl == cacheKey)
            .OrderBy(i => i.CachedAtUtc)
            .ToListAsync(cancellationToken);

        var results = new List<ProductImageCandidate>();
        foreach (var row in rows)
        {
            var path = Path.Combine(_root, row.RelativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                if (bytes.Length == 0)
                {
                    continue;
                }

                results.Add(new ProductImageCandidate
                {
                    SourceUrl = row.ImageUrl,
                    Bytes = bytes,
                    ContentType = string.IsNullOrWhiteSpace(row.ContentType) ? "image/jpeg" : row.ContentType
                });
            }
            catch
            {
                // Skip unreadable files.
            }
        }

        return results;
    }

    public bool HtmlExists(Uri pageUri, string cacheKey) =>
        HtmlCandidatePaths(pageUri, cacheKey).Any(File.Exists);

    public IReadOnlyList<DomainCacheSummary> GetDomainSummaries()
    {
        var map = new Dictionary<string, DomainCacheSummary>(StringComparer.OrdinalIgnoreCase);

        void Add(string domain, int pages, long pageBytes, int images, long imageBytes)
        {
            if (map.TryGetValue(domain, out var existing))
            {
                map[domain] = existing with
                {
                    PageCount = existing.PageCount + pages,
                    PageBytes = existing.PageBytes + pageBytes,
                    ImageCount = existing.ImageCount + images,
                    ImageBytes = existing.ImageBytes + imageBytes
                };
            }
            else
            {
                map[domain] = new DomainCacheSummary(domain, pages, pageBytes, images, imageBytes);
            }
        }

        if (!Directory.Exists(_root))
        {
            return [];
        }

        foreach (var file in Directory.GetFiles(_root, "*.html"))
        {
            Add(LegacyDomain, 1, new FileInfo(file).Length, 0, 0);
        }

        foreach (var domainDir in Directory.GetDirectories(_root))
        {
            var domain = Path.GetFileName(domainDir);
            var pagesDir = Path.Combine(domainDir, PagesFolder);
            var imagesDir = Path.Combine(domainDir, ImagesFolder);
            var pageFiles = Directory.Exists(pagesDir) ? Directory.GetFiles(pagesDir) : [];
            var imageFiles = Directory.Exists(imagesDir) ? Directory.GetFiles(imagesDir) : [];
            if (pageFiles.Length == 0 && imageFiles.Length == 0)
            {
                continue;
            }

            Add(
                domain,
                pageFiles.Length,
                pageFiles.Sum(path => new FileInfo(path).Length),
                imageFiles.Length,
                imageFiles.Sum(path => new FileInfo(path).Length));
        }

        return map.Values
            .OrderBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public PageCacheInfo GetTotals()
    {
        var domains = GetDomainSummaries();
        return new PageCacheInfo(
            _root,
            domains.Sum(d => d.PageCount),
            domains.Sum(d => d.PageBytes),
            domains.Sum(d => d.ImageCount),
            domains.Sum(d => d.ImageBytes));
    }

    public async Task<int> ClearAsync(IReadOnlyCollection<string> domains, bool includeImages)
    {
        var removed = 0;
        var domainSet = domains.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (domainSet.Contains(LegacyDomain) && Directory.Exists(_root))
        {
            foreach (var file in Directory.GetFiles(_root, "*.html"))
            {
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch
                {
                    // Leave locked files.
                }
            }
        }

        foreach (var domain in domainSet.Where(d => !string.Equals(d, LegacyDomain, StringComparison.OrdinalIgnoreCase)))
        {
            var pagesDir = Path.Combine(DomainFolder(domain), PagesFolder);
            removed += DeleteFiles(pagesDir);
            if (includeImages)
            {
                removed += DeleteFiles(Path.Combine(DomainFolder(domain), ImagesFolder));
            }
        }

        await using var db = _database.CreateContext();
        var pages = await db.CachedWebPages
            .Where(p => domainSet.Contains(p.Domain))
            .ToListAsync();
        db.CachedWebPages.RemoveRange(pages);

        if (includeImages)
        {
            var images = await db.CachedWebImages
                .Where(i => domainSet.Contains(i.Domain))
                .ToListAsync();
            db.CachedWebImages.RemoveRange(images);
        }

        await db.SaveChangesAsync();
        return removed;
    }

    private IEnumerable<string> HtmlCandidatePaths(Uri pageUri, string cacheKey)
    {
        yield return PageFilePath(pageUri, cacheKey);
        yield return LegacyPagePath(cacheKey);
    }

    private static int DeleteFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.GetFiles(directory))
        {
            try
            {
                File.Delete(file);
                removed++;
            }
            catch
            {
                // Leave locked files.
            }
        }

        return removed;
    }

    public static string Host(Uri pageUri) =>
        string.IsNullOrWhiteSpace(pageUri.Host) ? "unknown" : pageUri.Host.ToLowerInvariant();

    private static string FileSlug(string hint, string uniqueKey)
    {
        var slug = Regex.Replace(hint, @"[^a-zA-Z0-9]+", "-").Trim('-').ToLowerInvariant();
        if (slug.Length > 72)
        {
            slug = slug[..72].TrimEnd('-');
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "item";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uniqueKey.ToLowerInvariant())))[..8]
            .ToLowerInvariant();
        return $"{slug}--{hash}";
    }

    private static string SanitizeSegment(string value)
    {
        var cleaned = Regex.Replace(value, @"[<>:""/\\|?*]", "-").Trim('.');
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }

    private static string Extension(string contentType, string imageUrl)
    {
        var type = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (type is "image/jpeg" or "image/jpg")
        {
            return ".jpg";
        }

        if (type == "image/png")
        {
            return ".png";
        }

        if (type == "image/webp")
        {
            return ".webp";
        }

        if (type == "image/gif")
        {
            return ".gif";
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath);
            if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")
            {
                return ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ext.ToLowerInvariant();
            }
        }

        return ".jpg";
    }
}
