namespace WorkCosts.Models;

public class CachedWebPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PageUrl { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long ByteSize { get; set; }
    public DateTime CachedAtUtc { get; set; }
}

public class CachedWebImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PageUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long ByteSize { get; set; }
    public DateTime CachedAtUtc { get; set; }
}
