namespace WorkCosts.Services;

public static class CarImageStore
{
    public const int MaxImageBytes = 512 * 1024;

    public static string ImagesDirectoryRelative => "images/cars";

    public static bool IsAllowedContentType(string? contentType) =>
        TryNormalizeContentType(contentType, out _);

    public static bool TryDescribe(ReadOnlySpan<byte> bytes, out string contentType)
    {
        contentType = string.Empty;
        if (bytes.Length is <= 0 or > MaxImageBytes)
        {
            return false;
        }

        if (IsPng(bytes))
        {
            contentType = "image/png";
            return true;
        }

        if (IsJpeg(bytes))
        {
            contentType = "image/jpeg";
            return true;
        }

        if (IsWebp(bytes))
        {
            contentType = "image/webp";
            return true;
        }

        return false;
    }

    public static string RelativePathFor(Guid carId, string contentType)
    {
        if (!TryNormalizeContentType(contentType, out var normalized))
        {
            throw new ArgumentException("Unsupported image content type.", nameof(contentType));
        }

        var ext = normalized switch
        {
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            _ => "png",
        };
        return $"{ImagesDirectoryRelative}/{carId:N}.{ext}";
    }

    public static string GetFullPath(string dataRoot, string relativePath) =>
        Path.GetFullPath(Path.Combine(dataRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static async Task WriteAsync(
        string dataRoot,
        Guid carId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeContentType(contentType, out var normalized))
        {
            throw new ArgumentException("Unsupported image content type.", nameof(contentType));
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (!TryDescribe(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), out var sniffed)
            || !string.Equals(sniffed, normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image file exceeds size limits or is an unsupported type.", nameof(content));
        }

        var relative = RelativePathFor(carId, normalized);
        var full = GetFullPath(dataRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        buffer.Position = 0;
        await using var file = File.Create(full);
        await buffer.CopyToAsync(file, cancellationToken);
    }

    public static async Task<byte[]> ReadAsync(
        string dataRoot,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var full = GetFullPath(dataRoot, relativePath);
        return await File.ReadAllBytesAsync(full, cancellationToken);
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

    private static bool TryNormalizeContentType(string? contentType, out string normalized)
    {
        normalized = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized == "image/jpg")
        {
            normalized = "image/jpeg";
        }

        return normalized is "image/png" or "image/jpeg" or "image/webp";
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8
        && bytes[0] == 0x89
        && bytes[1] == 0x50
        && bytes[2] == 0x4E
        && bytes[3] == 0x47
        && bytes[4] == 0x0D
        && bytes[5] == 0x0A
        && bytes[6] == 0x1A
        && bytes[7] == 0x0A;

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3
        && bytes[0] == 0xFF
        && bytes[1] == 0xD8
        && bytes[2] == 0xFF;

    private static bool IsWebp(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12
        && bytes[0] == (byte)'R'
        && bytes[1] == (byte)'I'
        && bytes[2] == (byte)'F'
        && bytes[3] == (byte)'F'
        && bytes[8] == (byte)'W'
        && bytes[9] == (byte)'E'
        && bytes[10] == (byte)'B'
        && bytes[11] == (byte)'P';
}
