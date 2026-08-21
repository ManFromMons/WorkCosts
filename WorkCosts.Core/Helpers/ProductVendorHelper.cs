namespace WorkCosts.Helpers;

public static class ProductVendorHelper
{
    public static string FormatBreadcrumb(string? source, string? vendor)
    {
        var seller = vendor?.Trim() ?? string.Empty;
        var src = source?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(src))
        {
            return string.IsNullOrEmpty(seller) ? "—" : seller;
        }

        if (string.IsNullOrEmpty(seller))
        {
            return src;
        }

        return $"{src} › {seller}";
    }

    public static string? InferSourceFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (Services.ProductPageMetadataParser.IsAmazonHost(uri.Host))
        {
            return "Amazon";
        }

        if (Services.ProductPageMetadataParser.IsAutodocHost(uri.Host))
        {
            return "Autodoc";
        }

        if (Services.ProductPageMetadataParser.IsEuroCarPartsHost(uri.Host))
        {
            return "Euro Car Parts";
        }

        if (Services.ProductPageMetadataParser.IsCarBatteryMarketHost(uri.Host))
        {
            return "Car Battery Market";
        }

        if (Services.ProductPageMetadataParser.IsTaynaHost(uri.Host))
        {
            return "Tayna";
        }

        return null;
    }
}
