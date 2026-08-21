namespace WorkCosts.Services;

/// <summary>Client-facing entry point for turning product-page HTML into metadata.</summary>
public interface IProductPageParser
{
    Task<ProductPageMetadata> ParseHtmlAsync(string html, string pageUrl);
}

public sealed class ProductPageParser : IProductPageParser
{
    public Task<ProductPageMetadata> ParseHtmlAsync(string html, string pageUrl) =>
        ProductPageMetadataParser.ParseHtmlAsync(html, pageUrl);
}

/// <summary>
/// Values the app should copy onto a product. Null means leave the existing field unchanged.
/// </summary>
public sealed record ProductPageClientValues(
    string? Name,
    string? Manufacturer,
    string? ManufacturerReference,
    decimal? UnitPrice,
    string? Vendor,
    string? Ean,
    string? Variation,
    string? OemEquivalent,
    string? Source,
    int? Capacity = null,
    int? LengthMm = null,
    int? WidthMm = null,
    int? HeightMm = null,
    int? Cca = null,
    string? Technology = null,
    IReadOnlyDictionary<string, string>? ExtraUnknown = null)
{
    public static ProductPageClientValues From(ProductPageMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ProductPageClientValues(
            BlankToNull(metadata.Name),
            BlankToNull(metadata.Manufacturer),
            BlankToNull(metadata.ManufacturerReference),
            metadata.UnitPrice is decimal price && price >= 0 ? price : null,
            BlankToNull(metadata.Vendor),
            BlankToNull(metadata.Ean),
            BlankToNull(metadata.Variation),
            BlankToNull(metadata.OemEquivalent),
            BlankToNull(metadata.Source),
            NonNegative(metadata.Capacity),
            NonNegative(metadata.LengthMm),
            NonNegative(metadata.WidthMm),
            NonNegative(metadata.HeightMm),
            NonNegative(metadata.Cca),
            BlankToNull(metadata.Technology),
            ExtraUnknownFrom(metadata.ExtraUnknown));
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? NonNegative(int? value) =>
        value is int n && n >= 0 ? n : null;

    private static IReadOnlyDictionary<string, string>? ExtraUnknownFrom(
        IReadOnlyDictionary<string, string>? map)
    {
        if (map is null || map.Count == 0)
        {
            return null;
        }

        var cleaned = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in map)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            cleaned[key.Trim()] = value.Trim();
        }

        return cleaned.Count == 0 ? null : cleaned;
    }
}
