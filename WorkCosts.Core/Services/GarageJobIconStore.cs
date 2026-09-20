namespace WorkCosts.Services;

public static class GarageJobIconStore
{
    public const int MaxIconBytes = 512 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
    };

    public static string IconsDirectoryRelative => "icons/garage-jobs";

    public static bool IsAllowedContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType.Trim());

    public static string RelativePathFor(Guid garageJobId, string extension)
    {
        var ext = NormalizeExtension(extension);
        return $"{IconsDirectoryRelative}/{garageJobId:N}.{ext}";
    }

    public static string GetFullPath(string dataRoot, string relativePath) =>
        Path.GetFullPath(Path.Combine(dataRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static async Task WriteAsync(
        string dataRoot,
        Guid garageJobId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsAllowedContentType(contentType))
        {
            throw new ArgumentException("Unsupported icon content type.", nameof(contentType));
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is <= 0 or > MaxIconBytes)
        {
            throw new ArgumentException("Icon file exceeds size limits.", nameof(content));
        }

        var ext = ExtensionFromContentType(contentType);
        var relative = RelativePathFor(garageJobId, ext);
        var full = GetFullPath(dataRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        buffer.Position = 0;
        await using var file = File.Create(full);
        await buffer.CopyToAsync(file, cancellationToken);
    }

    public static void DeleteIfExists(string dataRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var full = GetFullPath(dataRoot, relativePath);
        if (File.Exists(full))
        {
            File.Delete(full);
        }
    }

    private static string NormalizeExtension(string extension)
    {
        var ext = extension.Trim().TrimStart('.').ToLowerInvariant();
        return string.IsNullOrEmpty(ext) ? "png" : ext;
    }

    private static string ExtensionFromContentType(string contentType) =>
        contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            _ => "png",
        };
}
