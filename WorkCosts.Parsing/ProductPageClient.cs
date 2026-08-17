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
    string? Source)
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
            BlankToNull(metadata.Source));
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
