using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace WorkCosts.Services;

public sealed record ProductPageMetadata(
    string? Name,
    string? Manufacturer,
    string? ManufacturerReference,
    decimal? UnitPrice,
    string? Vendor = null,
    string? Ean = null,
    string? Variation = null,
    string? OemEquivalent = null,
    string? Source = null)
{
    public static ProductPageMetadata Empty { get; } = new(null, null, null, null);
}

public static class ProductPageMetadataParser
{
    public static async Task<ProductPageMetadata> ParseHtmlAsync(string html, string pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri))
        {
            throw new ArgumentException("pageUrl must be an absolute URI.", nameof(pageUrl));
        }

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html).Address(pageUri.ToString()));
        return Parse(document, pageUri);
    }

    public static ProductPageMetadata Parse(IDocument document, Uri pageUri)
    {
        if (IsAmazonHost(pageUri.Host))
        {
            return ParseAmazon(document);
        }

        return ParseGeneric(document, pageUri);
    }

    public static bool IsAmazonHost(string host) =>
        host.Contains("amazon.", StringComparison.OrdinalIgnoreCase)
        || host.Contains("amzn.", StringComparison.OrdinalIgnoreCase);

    private static ProductPageMetadata ParseAmazon(IDocument document)
    {
        var name = StripAmazonTitleSuffix(
            Clean(document.QuerySelector("#productTitle")?.TextContent)
            ?? Clean(document.QuerySelector("#title")?.TextContent)
            ?? MetaContent(document, "og:title"));

        var manufacturer =
            DetailValue(document, "Manufacturer")
            ?? DetailValue(document, "Brand Name")
            ?? BrandFromByline(document)
            ?? DetailValue(document, "Brand")
            ?? MetaContent(document, "og:brand");

        var mfrRef =
            DetailValue(document, "Manufacturer Part Number")
            ?? DetailValue(document, "Item model number")
            ?? DetailValue(document, "Model Number")
            ?? DetailValue(document, "Part Number");

        var unitPrice = ParseAmazonPrice(document);
        var vendor = AmazonVendor(document);
        var ean = NormalizeGtin(
            DetailValue(document, "Global Trade Identification Number")
            ?? DetailValue(document, "EAN")
            ?? DetailValue(document, "GTIN"));
        var variation = AmazonVariation(document);
        var oem = DetailValue(document, "OEM Equivalent Part Number");

        return new ProductPageMetadata(name, manufacturer, mfrRef, unitPrice, vendor, ean, variation, oem, "Amazon");
    }

    private static ProductPageMetadata ParseGeneric(IDocument document, Uri pageUri)
    {
        var name = MetaContent(document, "og:title")
            ?? Clean(document.QuerySelector("h1")?.TextContent);

        var manufacturer = MetaContent(document, "product:brand")
            ?? MetaContent(document, "og:brand");

        var unitPrice = ParsePriceText(MetaContent(document, "product:price:amount")
            ?? MetaContent(document, "og:price:amount")
            ?? document.QuerySelector("meta[itemprop='price']")?.GetAttribute("content"));

        var source = InferGenericSource(pageUri);
        return new ProductPageMetadata(name, manufacturer, null, unitPrice, Source: source);
    }

    private static string? InferGenericSource(Uri pageUri)
    {
        if (IsAmazonHost(pageUri.Host))
        {
            return "Amazon";
        }

        return null;
    }

    private static decimal? ParseAmazonPrice(IDocument document)
    {
        string?[] candidates =
        [
            Clean(document.QuerySelector("#corePrice_feature_div span.a-price .a-offscreen")?.TextContent),
            Clean(document.QuerySelector("#corePriceDisplay_desktop_feature_div span.a-price .a-offscreen")?.TextContent),
            Clean(document.QuerySelector("#apex_desktop span.a-price .a-offscreen")?.TextContent),
            Clean(document.QuerySelector("span.a-price.aok-align-center .a-offscreen")?.TextContent),
            Clean(document.QuerySelector("#priceblock_ourprice")?.TextContent),
            Clean(document.QuerySelector("#priceblock_dealprice")?.TextContent),
            Clean(document.QuerySelector("#price_inside_buybox")?.TextContent),
            MetaContent(document, "og:price:amount"),
            document.QuerySelector("meta[itemprop='price']")?.GetAttribute("content")
        ];

        foreach (var candidate in candidates)
        {
            var price = ParsePriceText(candidate);
            if (price is not null)
            {
                return price;
            }
        }

        var whole = Clean(document.QuerySelector("#corePrice_feature_div span.a-price-whole")?.TextContent)
            ?? Clean(document.QuerySelector("span.a-price-whole")?.TextContent);
        var fraction = Clean(document.QuerySelector("#corePrice_feature_div span.a-price-fraction")?.TextContent)
            ?? Clean(document.QuerySelector("span.a-price-fraction")?.TextContent);
        if (!string.IsNullOrWhiteSpace(whole))
        {
            var combined = string.IsNullOrWhiteSpace(fraction)
                ? whole.TrimEnd('.', ',')
                : $"{whole.TrimEnd('.', ',')}.{fraction}";
            var price = ParsePriceText(combined);
            if (price is not null)
            {
                return price;
            }
        }

        return null;
    }

    private static decimal? ParsePriceText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = Regex.Replace(text, @"[^\d.,]", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            if (normalized.LastIndexOf(',') > normalized.LastIndexOf('.'))
            {
                normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
            }
            else
            {
                normalized = normalized.Replace(",", string.Empty);
            }
        }
        else if (normalized.Contains(',') && !normalized.Contains('.'))
        {
            var parts = normalized.Split(',');
            normalized = parts[^1].Length <= 2
                ? normalized.Replace(',', '.')
                : normalized.Replace(",", string.Empty);
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            && value >= 0
            && value < 1_000_000m)
        {
            return value;
        }

        return null;
    }

    private static string? BrandFromByline(IDocument document)
    {
        var byline = Clean(document.QuerySelector("#bylineInfo")?.TextContent);
        if (string.IsNullOrWhiteSpace(byline))
        {
            return null;
        }

        var visit = Regex.Match(byline, @"Visit\s+the\s+(.+?)\s+Store", RegexOptions.IgnoreCase);
        if (visit.Success)
        {
            return Clean(visit.Groups[1].Value);
        }

        var brand = Regex.Match(byline, @"Brand\s*:\s*(.+)$", RegexOptions.IgnoreCase);
        if (brand.Success)
        {
            return Clean(brand.Groups[1].Value);
        }

        return byline;
    }

    private static string? DetailValue(IDocument document, string label)
    {
        foreach (var row in document.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("th, td").ToList();
            if (cells.Count < 2)
            {
                continue;
            }

            var header = Clean(cells[0].TextContent);
            if (!LabelMatches(header, label))
            {
                continue;
            }

            var value = Clean(cells[1].TextContent);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        foreach (var item in document.QuerySelectorAll("#detailBullets_feature_div li, #detailBulletsWrapper_feature_div li"))
        {
            var text = Clean(item.TextContent);
            if (text is null)
            {
                continue;
            }

            var parts = text.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            if (LabelMatches(Clean(parts[0]), label))
            {
                return Clean(parts[1]);
            }
        }

        foreach (var row in document.QuerySelectorAll("#productDetails_techSpec_section_1 tr, #productDetails_detailBullets_sections1 tr"))
        {
            var header = Clean(row.QuerySelector("th")?.TextContent);
            if (!LabelMatches(header, label))
            {
                continue;
            }

            var value = Clean(row.QuerySelector("td")?.TextContent);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool LabelMatches(string? header, string label) =>
        !string.IsNullOrWhiteSpace(header)
        && header.Equals(label, StringComparison.OrdinalIgnoreCase);

    private static string? MetaContent(IDocument document, string property)
    {
        if (document.QuerySelector($"meta[property='{property}']") is IHtmlMetaElement byProperty
            && !string.IsNullOrWhiteSpace(byProperty.Content))
        {
            return Clean(byProperty.Content);
        }

        if (document.QuerySelector($"meta[name='{property}']") is IHtmlMetaElement byName
            && !string.IsNullOrWhiteSpace(byName.Content))
        {
            return Clean(byName.Content);
        }

        return null;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var cleaned = Regex.Replace(decoded, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static string? StripAmazonTitleSuffix(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var pipe = name.IndexOf(" | ", StringComparison.Ordinal);
        return pipe > 0 ? name[..pipe].Trim() : name;
    }

    private static string? NormalizeGtin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = Regex.Replace(value, @"\D", string.Empty);
        return digits.Length is >= 8 and <= 14 ? digits : Clean(value);
    }

    private static string? AmazonVendor(IDocument document)
    {
        string?[] candidates =
        [
            Clean(document.QuerySelector("[offer-display-feature-name='desktop-merchant-info'] .offer-display-feature-text-message")?.TextContent),
            Clean(document.QuerySelector("#sellerProfileTriggerId")?.TextContent),
            Clean(document.QuerySelector("#merchant-info")?.TextContent)
        ];

        foreach (var candidate in candidates)
        {
            var vendor = VendorFromText(candidate);
            if (vendor is not null)
            {
                return vendor;
            }
        }

        foreach (var root in document.QuerySelectorAll(
                     "#buybox, #desktop_buybox, #apex_desktop, #merchantInfoFeature, #merchant-info, #sfsb_accordion_head"))
        {
            foreach (var node in root.QuerySelectorAll("span, div"))
            {
                var text = Clean(node.TextContent);
                if (text is null || text.Length > 80)
                {
                    continue;
                }

                if (text.StartsWith("Sold by", StringComparison.OrdinalIgnoreCase))
                {
                    var vendor = VendorFromText(text);
                    if (vendor is not null)
                    {
                        return vendor;
                    }
                }
            }
        }

        return null;
    }

    private static string? VendorFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var sold = Regex.Match(text, @"^Sold by\s*:?\s*(.*)$", RegexOptions.IgnoreCase);
        if (sold.Success)
        {
            text = Clean(sold.Groups[1].Value);
        }

        if (string.IsNullOrWhiteSpace(text)
            || text.Contains("Dispatches from", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Ships from", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Sold by", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return text;
    }

    private static string? AmazonVariation(IDocument document)
    {
        var selected = document.QuerySelectorAll(".inline-twister-dim-title-value")
            .Select(n => Clean(n.TextContent))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return selected.Count == 0 ? null : string.Join(" / ", selected);
    }
}
