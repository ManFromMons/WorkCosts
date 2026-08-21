using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Globalization;
using System.Net;
using System.Text.Json;
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
    string? Source = null,
    int? Capacity = null,
    int? LengthMm = null,
    int? WidthMm = null,
    int? HeightMm = null,
    int? Cca = null,
    string? Technology = null)
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

    /// <summary>
    /// Absolute http(s) page URL from canonical, og:url, base href, or IE "saved from url" markup.
    /// </summary>
    public static async Task<string?> FindPageUrlAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var fromMarkup = FindSavedFromUrl(html);
        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html));
        return FirstHttpUrl(
            Attr(document, "link[rel='canonical']", "href"),
            Attr(document, "link[rel=canonical]", "href"),
            MetaContent(document, "og:url"),
            Attr(document, "base", "href"),
            fromMarkup);
    }

    private static string? Attr(IDocument document, string selector, string name)
    {
        var value = document.QuerySelector(selector)?.GetAttribute(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstHttpUrl(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var trimmed = candidate.Trim().Trim('"', '\'');
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                trimmed = "https:" + trimmed;
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.ToString();
            }
        }

        return null;
    }

    private static string? FindSavedFromUrl(string html)
    {
        var match = Regex.Match(
            html,
            @"saved from url=\(\d+\)(https?://\S+)",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.TrimEnd() : null;
    }

    public static ProductPageMetadata Parse(IDocument document, Uri pageUri)
    {
        if (IsAmazonHost(pageUri.Host))
        {
            return ParseAmazon(document);
        }

        if (IsAutodocHost(pageUri.Host))
        {
            return ParseAutodoc(document);
        }

        if (IsEuroCarPartsHost(pageUri.Host))
        {
            return ParseEuroCarParts(document);
        }

        if (IsCarBatteryMarketHost(pageUri.Host))
        {
            return ParseCarBatteryMarket(document);
        }

        if (IsTaynaHost(pageUri.Host))
        {
            return ParseTayna(document);
        }

        return ParseGeneric(document, pageUri);
    }

    public static bool IsAmazonHost(string host) =>
        host.Contains("amazon.", StringComparison.OrdinalIgnoreCase)
        || host.Contains("amzn.", StringComparison.OrdinalIgnoreCase);

    public static bool IsAutodocHost(string host) =>
        host.Contains("autodoc.", StringComparison.OrdinalIgnoreCase);

    public static bool IsEuroCarPartsHost(string host) =>
        host.Contains("eurocarparts.", StringComparison.OrdinalIgnoreCase);

    public static bool IsCarBatteryMarketHost(string host) =>
        host.Contains("carbatterymarket.", StringComparison.OrdinalIgnoreCase);

    public static bool IsTaynaHost(string host) =>
        host.Contains("tayna.", StringComparison.OrdinalIgnoreCase);

    private static ProductPageMetadata ParseAmazon(IDocument document)
    {
        var name = StripTitleAfterPipe(
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

    private static ProductPageMetadata ParseAutodoc(IDocument document)
    {
        if (IsAutodocListingPage(document))
        {
            return ParseAutodocListing(document);
        }

        var json = ReadAutodocJsonLd(document);

        var name = StripTitleAfterPipe(
            json?.Name
            ?? AutodocHeading(document)
            ?? MetaContent(document, "og:title"));

        var manufacturer =
            json?.Brand
            ?? json?.Manufacturer
            ?? AutodocDescriptionValue(document, "Manufacturer");

        var mfrRef =
            json?.Mpn
            ?? json?.Sku
            ?? AutodocLabeledArticle(document, "Article number")
            ?? AutodocLabeledArticle(document, "Item number")
            ?? AutodocDescriptionValue(document, "Item number")
            ?? AutodocDescriptionValue(document, "Article number");

        var unitPrice = json?.Price
            ?? ParsePriceText(document.QuerySelector("[data-product-page][data-price]")?.GetAttribute("data-price"))
            ?? ParsePriceText(Clean(document.QuerySelector(".product-block__price-new-wrap")?.TextContent))
            ?? ParsePriceText(AutodocDescriptionValue(document, "Our price"));

        var vendor = AutodocVendor(document) ?? AutodocSellerName(json?.Seller);

        var ean = NormalizeGtin(
            json?.Ean
            ?? AutodocLabeledArticle(document, "EAN")
            ?? AutodocDescriptionValue(document, "EAN number")
            ?? AutodocDescriptionValue(document, "EAN"));

        var oem = json?.Oem
            ?? AutodocDescriptionValue(document, "OE numbers")
            ?? AutodocDescriptionValue(document, "OEM numbers")
            ?? AutodocDescriptionValue(document, "OEM Equivalent Part Number")
            ?? AutodocOemList(document);

        return new ProductPageMetadata(name, manufacturer, mfrRef, unitPrice, vendor, ean, null, oem, "Autodoc");
    }

    private static bool IsAutodocListingPage(IDocument document) =>
        document.QuerySelector(".listing-page, h1.listing-title__name") is not null
        && document.QuerySelector("[data-product-page], h1.product-block__title") is null;

    private static ProductPageMetadata ParseAutodocListing(IDocument document)
    {
        var name = StripTitleAfterPipe(
            Clean(document.QuerySelector("h1.listing-title__name")?.TextContent)
            ?? AutodocHeading(document)
            ?? MetaContent(document, "og:title"));

        var variation = AutodocSelectedFilters(document);

        return new ProductPageMetadata(
            name,
            Manufacturer: null,
            ManufacturerReference: null,
            UnitPrice: null,
            Vendor: null,
            Ean: null,
            Variation: variation,
            OemEquivalent: null,
            Source: "Autodoc");
    }

    private static string? AutodocSelectedFilters(IDocument document)
    {
        var selected = document.QuerySelectorAll(".selected-filter__title")
            .Select(n => Clean(n.TextContent))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return selected.Count == 0 ? null : string.Join(" / ", selected);
    }

    private static string? InferGenericSource(Uri pageUri)
    {
        if (IsAmazonHost(pageUri.Host))
        {
            return "Amazon";
        }

        if (IsAutodocHost(pageUri.Host))
        {
            return "Autodoc";
        }

        if (IsEuroCarPartsHost(pageUri.Host))
        {
            return "Euro Car Parts";
        }

        if (IsCarBatteryMarketHost(pageUri.Host))
        {
            return "Car Battery Market";
        }

        if (IsTaynaHost(pageUri.Host))
        {
            return "Tayna";
        }

        return null;
    }

    private static ProductPageMetadata ParseEuroCarParts(IDocument document)
    {
        var name = Clean(document.QuerySelector("h1[data-testid='productName'], [data-testid='productName']")?.TextContent)
            ?? StripTitleAfterPipe(MetaContent(document, "og:title"));

        var manufacturer = EuroCarPartsManufacturer(document);
        var unitPrice = EuroCarPartsPrice(document);
        var extras = EuroCarPartsExtras(document, name);

        return new ProductPageMetadata(
            name,
            manufacturer,
            ManufacturerReference: null,
            unitPrice,
            Vendor: "Euro Car Parts",
            Ean: null,
            Variation: null,
            OemEquivalent: null,
            Source: "Euro Car Parts",
            extras.Capacity,
            extras.LengthMm,
            extras.WidthMm,
            extras.HeightMm,
            extras.Cca,
            extras.Technology);
    }

    private static ProductPageMetadata ParseCarBatteryMarket(IDocument document)
    {
        var name = Clean(document.QuerySelector("h1.product--title, h1[itemprop='name']")?.TextContent)
            ?? StripTitleAfterPipe(MetaContent(document, "og:title"));

        var manufacturer = CarBatteryMarketManufacturer(document, name);
        var mfrRef = CarBatteryMarketManufacturerReference(document, name, manufacturer);
        var unitPrice = CarBatteryMarketPrice(document);
        var extras = CarBatteryMarketExtras(document, name);

        return new ProductPageMetadata(
            name,
            manufacturer,
            mfrRef,
            unitPrice,
            Vendor: "Car Battery Market",
            Ean: null,
            Variation: null,
            OemEquivalent: null,
            Source: "Car Battery Market",
            extras.Capacity,
            extras.LengthMm,
            extras.WidthMm,
            extras.HeightMm,
            extras.Cca,
            extras.Technology);
    }

    private static ProductPageMetadata ParseTayna(IDocument document)
    {
        var heading = TaynaHeading(document);
        var name = string.IsNullOrWhiteSpace(heading) ? null : heading.ToUpperInvariant();
        var specs = TaynaSpecMap(document);
        var manufacturer = TaynaManufacturer(document, specs);
        var mfrRef = TaynaManufacturerReference(specs, heading, manufacturer);
        var unitPrice = TaynaPrice(document);
        var extras = TaynaExtras(specs);

        return new ProductPageMetadata(
            name,
            manufacturer,
            mfrRef,
            unitPrice,
            Vendor: "Tayna",
            Ean: TaynaEan(document, specs),
            Variation: null,
            OemEquivalent: null,
            Source: "Tayna",
            extras.Capacity,
            extras.LengthMm,
            extras.WidthMm,
            extras.HeightMm,
            extras.Cca,
            extras.Technology);
    }

    private static string? TaynaHeading(IDocument document) =>
        Clean(document.QuerySelector("h1 [itemprop='name']")?.TextContent)
        ?? Clean(document.QuerySelector("h1")?.TextContent);

    private static string? TaynaManufacturer(IDocument document, IReadOnlyDictionary<string, string> specs) =>
        MetaContent(document, "product:brand")
        ?? GetSpec(specs, "Brand");

    private static string? TaynaManufacturerReference(
        IReadOnlyDictionary<string, string> specs,
        string? heading,
        string? manufacturer)
    {
        var fromTable = GetSpec(specs, "Product Code");
        if (!string.IsNullOrWhiteSpace(fromTable))
        {
            return fromTable;
        }

        var tokens = SplitTokens(heading);
        if (tokens.Count < 2)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(manufacturer)
            && tokens[0].Equals(manufacturer, StringComparison.OrdinalIgnoreCase))
        {
            return tokens[1];
        }

        return null;
    }

    private static decimal? TaynaPrice(IDocument document) =>
        ParsePriceText(Clean(document.QuerySelector(".pricing-holder #prodprice")?.TextContent))
        ?? ParsePriceText(MetaContent(document, "product:price:amount"))
        ?? ParsePriceText(MetaContent(document, "twitter:data1"));

    private static string? TaynaEan(IDocument document, IReadOnlyDictionary<string, string> specs) =>
        NormalizeGtin(
            Clean(document.QuerySelector("#tech-spec [itemprop='gtin13']")?.TextContent)
            ?? GetSpec(specs, "Barcode (EAN)")
            ?? GetSpecContaining(specs, "EAN"));

    private static (int? Capacity, int? LengthMm, int? WidthMm, int? HeightMm, int? Cca, string? Technology)
        TaynaExtras(IReadOnlyDictionary<string, string> specs)
    {
        var capacity = ParseLeadingInt(GetSpec(specs, "Capacity (C20)"));
        var lengthMm = ParseLeadingInt(GetSpec(specs, "Length"));
        var widthMm = ParseLeadingInt(GetSpec(specs, "Width"));
        var heightMm = ParseLeadingInt(GetSpec(specs, "Height inc. terms"))
            ?? ParseLeadingInt(GetSpecContaining(specs, "Height inc"));
        var cca = ParseLeadingInt(GetSpec(specs, "CCA (EN)"));
        var technology = ProductTechnology.Normalize(GetSpec(specs, "Technology"));

        return (capacity, lengthMm, widthMm, heightMm, cca, technology);
    }

    private static Dictionary<string, string> TaynaSpecMap(IDocument document)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.QuerySelectorAll("#tech-spec table tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 2)
            {
                continue;
            }

            var label = Clean(cells[0].TextContent)?.TrimEnd(':');
            var value = Clean(cells[1].TextContent);
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            map[label] = value;
        }

        return map;
    }

    private static string? CarBatteryMarketManufacturer(IDocument document, string? name)
    {
        var fromMeta = Clean(document.QuerySelector("meta[itemprop='brand']")?.GetAttribute("content"));
        if (!string.IsNullOrWhiteSpace(fromMeta))
        {
            return fromMeta;
        }

        var fromTable = GetSpec(CarBatteryMarketSpecMap(document), "Brand");
        if (!string.IsNullOrWhiteSpace(fromTable))
        {
            return fromTable;
        }

        return FirstToken(name);
    }

    private static string? CarBatteryMarketManufacturerReference(
        IDocument document,
        string? name,
        string? manufacturer)
    {
        var fromList = CarBatteryMarketLabeledValue(document, "Model Code / MPN")
            ?? CarBatteryMarketLabeledValue(document, "Model");
        if (!string.IsNullOrWhiteSpace(fromList))
        {
            return fromList;
        }

        var tokens = SplitTokens(name);
        if (tokens.Count < 2)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(manufacturer)
            && tokens[0].Equals(manufacturer, StringComparison.OrdinalIgnoreCase))
        {
            return tokens[1];
        }

        return tokens.Count > 1 ? tokens[1] : null;
    }

    private static decimal? CarBatteryMarketPrice(IDocument document)
    {
        var meta = document.QuerySelector(".product--price.price--default meta[itemprop='price']");
        return ParsePriceText(meta?.GetAttribute("content"))
            ?? ParsePriceText(Clean(document.QuerySelector(".product--price.price--default .price--content.content--default")?.TextContent));
    }

    private static (int? Capacity, int? LengthMm, int? WidthMm, int? HeightMm, int? Cca, string? Technology)
        CarBatteryMarketExtras(IDocument document, string? name)
    {
        var specs = CarBatteryMarketSpecMap(document);

        var capacity = ParseLeadingInt(GetSpecContaining(specs, "Capacity AH"))
            ?? ParseLeadingInt(CarBatteryMarketLabeledValue(document, "Capacity (C20)"));
        var lengthMm = ParseLeadingInt(GetSpecContaining(specs, "Length (mm)"));
        var widthMm = ParseLeadingInt(GetSpecContaining(specs, "Width (mm)"));
        var heightMm = ParseLeadingInt(GetSpecContaining(specs, "Height (mm"));
        var cca = ParseLeadingInt(GetSpec(specs, "CCA"))
            ?? ParseLeadingInt(CarBatteryMarketLabeledValue(document, "Cold Cranking Amps (CCA)"))
            ?? ParseLeadingInt(CarBatteryMarketLabeledValue(document, "Cold Cranking Amps (EN)"));
        var technology = ProductTechnology.Normalize(name)
            ?? ProductTechnology.Normalize(CarBatteryMarketLabeledValue(document, "Technology"))
            ?? ProductTechnology.Normalize(GetSpec(specs, "Technology"));

        return (capacity, lengthMm, widthMm, heightMm, cca, technology);
    }

    private static Dictionary<string, string> CarBatteryMarketSpecMap(IDocument document)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.QuerySelectorAll("table.product--properties-table tr"))
        {
            var label = Clean(row.QuerySelector(".product--properties-label")?.TextContent)?.TrimEnd(':');
            var value = Clean(row.QuerySelector(".product--properties-value")?.TextContent);
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            map[label] = value;
        }

        return map;
    }

    private static string? GetSpecContaining(IReadOnlyDictionary<string, string> specs, string needle)
    {
        foreach (var pair in specs)
        {
            if (pair.Key.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string? CarBatteryMarketLabeledValue(IDocument document, string label)
    {
        foreach (var item in document.QuerySelectorAll("li"))
        {
            var heading = Clean(item.QuerySelector("strong")?.TextContent)?.TrimEnd(':');
            if (string.IsNullOrWhiteSpace(heading)
                || !heading.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var clone = item.Clone() as IElement;
            clone?.QuerySelector("strong")?.Remove();
            return Clean(clone?.TextContent);
        }

        return null;
    }

    private static string? FirstToken(string? text)
    {
        var tokens = SplitTokens(text);
        return tokens.Count == 0 ? null : tokens[0];
    }

    private static List<string> SplitTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static string? EuroCarPartsManufacturer(IDocument document)
    {
        var image = document.QuerySelector("img[data-testid='brandImage']");
        var brand = Clean(image?.GetAttribute("alt")) ?? Clean(image?.GetAttribute("title"));
        if (string.IsNullOrWhiteSpace(brand))
        {
            return null;
        }

        // Confirmed manufacturer is the maker (Eicher), not the range on the logo (Eicher Premium).
        var first = brand.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        return Clean(first);
    }

    private static decimal? EuroCarPartsPrice(IDocument document)
    {
        foreach (var node in document.QuerySelectorAll("[data-testid='pdpPrice']"))
        {
            if (IsEuroCarPartsAddonPrice(node))
            {
                continue;
            }

            var price = ParsePriceText(Clean(node.TextContent));
            if (price is not null)
            {
                return price;
            }
        }

        return null;
    }

    private static bool IsEuroCarPartsAddonPrice(IElement node)
    {
        for (var parent = node.ParentElement; parent is not null; parent = parent.ParentElement)
        {
            var testId = parent.GetAttribute("data-testid") ?? string.Empty;
            if (testId.Contains("bundle", StringComparison.OrdinalIgnoreCase)
                || testId.StartsWith("fbt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var className = parent.ClassName ?? string.Empty;
            if (className.Contains("FrequentlyBoughtTogether", StringComparison.OrdinalIgnoreCase)
                || className.Contains("FbtProductPrice", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static (int? Capacity, int? LengthMm, int? WidthMm, int? HeightMm, int? Cca, string? Technology)
        EuroCarPartsExtras(IDocument document, string? name)
    {
        var specs = EuroCarPartsSpecMap(document);

        var capacity = ParseLeadingInt(GetSpec(specs, "Capacity Ah"))
            ?? ParsePatternInt(name, @"(\d+)\s*AH\b");
        var lengthMm = ParseLeadingInt(GetSpec(specs, "Length mm"))
            ?? ParseLabeledMillimetres(document, "Length");
        var widthMm = ParseLeadingInt(GetSpec(specs, "Width mm"))
            ?? ParseLabeledMillimetres(document, "Width");
        var heightMm = ParseLeadingInt(GetSpec(specs, "Height mm"))
            ?? ParseLabeledMillimetres(document, "Height");
        // CCA from the product name / cranking line (950CCA). Do not use Cold Test Current ENA.
        var cca = ParsePatternInt(name, @"(\d+)\s*CCA\b")
            ?? ParseEuroCarPartsCrankingAmps(document);
        var technology = ProductTechnology.Normalize(name)
            ?? ProductTechnology.Normalize(GetSpec(specs, "Battery Type"));

        return (capacity, lengthMm, widthMm, heightMm, cca, technology);
    }

    private static Dictionary<string, string> EuroCarPartsSpecMap(IDocument document)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cells = document.QuerySelectorAll("[data-testid='productSpecificationGrid'] > *")
            .Select(n => Clean(n.TextContent))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
        for (var i = 0; i + 1 < cells.Count; i += 2)
        {
            map[cells[i]!] = cells[i + 1]!;
        }

        return map;
    }

    private static string? GetSpec(IReadOnlyDictionary<string, string> specs, string label) =>
        specs.TryGetValue(label, out var value) ? value : null;

    private static int? ParseLabeledMillimetres(IDocument document, string label)
    {
        foreach (var item in document.QuerySelectorAll("li"))
        {
            var text = Clean(item.TextContent);
            if (text is null || !text.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var mm = ParsePatternInt(text, @"(\d+)\s*mm\b");
            if (mm is not null)
            {
                return mm;
            }
        }

        return null;
    }

    private static int? ParseEuroCarPartsCrankingAmps(IDocument document)
    {
        foreach (var item in document.QuerySelectorAll("li"))
        {
            var text = Clean(item.TextContent);
            if (text is null
                || (!text.Contains("CCA", StringComparison.OrdinalIgnoreCase)
                    && !text.Contains("Cold Cranking", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var fromItem = ParsePatternInt(text, @"(\d+)\s*cca\b");
            if (fromItem is not null)
            {
                return fromItem;
            }

            var sibling = Clean(item.NextSibling?.TextContent);
            var fromSibling = ParsePatternInt(sibling, @"(\d+)\s*cca\b");
            if (fromSibling is not null)
            {
                return fromSibling;
            }
        }

        return null;
    }

    private static int? ParseLeadingInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"^\s*(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? ParsePatternInt(string? text, string pattern)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
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

    private static string? StripTitleAfterPipe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var pipe = name.IndexOf(" | ", StringComparison.Ordinal);
        return pipe > 0 ? name[..pipe].Trim() : name;
    }

    private sealed record AutodocJsonProduct(
        string? Name,
        string? Brand,
        string? Manufacturer,
        string? Mpn,
        string? Sku,
        decimal? Price,
        string? Seller,
        string? Ean,
        string? Oem);

    private static AutodocJsonProduct? ReadAutodocJsonLd(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var text = script.TextContent;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(text);
                if (TryReadAutodocJsonProduct(json.RootElement, out var product))
                {
                    return product;
                }
            }
            catch (JsonException)
            {
                // Ignore non-JSON or truncated ld+json blocks.
            }
        }

        return null;
    }

    private static bool TryReadAutodocJsonProduct(JsonElement element, out AutodocJsonProduct product)
    {
        product = null!;
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryReadAutodocJsonProduct(item, out product))
                {
                    return true;
                }
            }

            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (element.TryGetProperty("@graph", out var graph)
            && TryReadAutodocJsonProduct(graph, out product))
        {
            return true;
        }

        if (!JsonTypeIs(element, "Product"))
        {
            return false;
        }

        string? additionalManufacturer = null;
        string? additionalEan = null;
        string? additionalOem = null;
        string? additionalMpn = null;
        if (element.TryGetProperty("additionalProperty", out var properties)
            && properties.ValueKind == JsonValueKind.Array)
        {
            foreach (var property in properties.EnumerateArray())
            {
                var propertyName = JsonString(property, "name");
                var propertyValue = JsonString(property, "value");
                if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(propertyValue))
                {
                    continue;
                }

                if (propertyName.Equals("Manufacturer", StringComparison.OrdinalIgnoreCase))
                {
                    additionalManufacturer = propertyValue;
                }
                else if (propertyName.Equals("EAN number", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("EAN", StringComparison.OrdinalIgnoreCase))
                {
                    additionalEan = propertyValue;
                }
                else if (propertyName.Equals("Item number", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("Article number", StringComparison.OrdinalIgnoreCase))
                {
                    additionalMpn ??= propertyValue;
                }
                else if (propertyName.Equals("OE numbers", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("OEM numbers", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("OEM Equivalent Part Number", StringComparison.OrdinalIgnoreCase))
                {
                    additionalOem = propertyValue;
                }
            }
        }

        var brand = JsonName(element, "brand");
        var offers = element.TryGetProperty("offers", out var offersElement) ? offersElement : default;
        var price = JsonDecimal(offers, "price");
        var seller = AutodocSellerName(JsonName(offers, "seller"));

        product = new AutodocJsonProduct(
            JsonString(element, "name"),
            brand,
            additionalManufacturer,
            JsonString(element, "mpn") ?? additionalMpn,
            JsonString(element, "sku"),
            price,
            seller,
            additionalEan,
            additionalOem);
        return true;
    }

    private static bool JsonTypeIs(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var typeElement))
        {
            return false;
        }

        if (typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString()?.Equals(type, StringComparison.OrdinalIgnoreCase) == true;
        }

        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            return typeElement.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String
                && item.GetString()?.Equals(type, StringComparison.OrdinalIgnoreCase) == true);
        }

        return false;
    }

    private static string? JsonString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => Clean(value.GetString()),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static decimal? JsonDecimal(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return ParsePriceText(JsonString(element, name));
    }

    private static string? JsonName(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return Clean(value.GetString());
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return JsonString(value, "name");
        }

        return null;
    }

    private static string? AutodocLabeledArticle(IDocument document, string label)
    {
        foreach (var node in document.QuerySelectorAll(".product-block__article"))
        {
            var text = Clean(node.TextContent);
            if (text is null)
            {
                continue;
            }

            var parts = text.Split(':', 2);
            if (parts.Length != 2 || !LabelMatches(Clean(parts[0]), label))
            {
                continue;
            }

            var value = Clean(parts[1]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? AutodocDescriptionValue(IDocument document, string label)
    {
        foreach (var item in document.QuerySelectorAll(".product-description__item"))
        {
            var title = Clean(item.QuerySelector(".product-description__item-title")?.TextContent)?.TrimEnd(':').Trim();
            if (!LabelMatches(title, label))
            {
                continue;
            }

            var value = Clean(item.QuerySelector(".product-description__item-value")?.TextContent);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? AutodocVendor(IDocument document) =>
        VendorFromText(Clean(document.QuerySelector("p.sold-by, .sold-by")?.TextContent));

    private static string? AutodocSellerName(string? name)
    {
        var cleaned = Clean(name);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        if (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) && IsAutodocHost(uri.Host))
        {
            return "AUTODOC";
        }

        return cleaned;
    }

    private static string? AutodocHeading(IDocument document)
    {
        var heading = document.QuerySelector("h1.product-block__title") ?? document.QuerySelector("h1");
        if (heading is null)
        {
            return null;
        }

        var text = Clean(heading.TextContent);
        var subtitle = Clean(heading.QuerySelector(".product-block__seo-subtitle")?.TextContent);
        if (!string.IsNullOrWhiteSpace(text)
            && !string.IsNullOrWhiteSpace(subtitle)
            && text.EndsWith(subtitle, StringComparison.Ordinal))
        {
            text = Clean(text[..^subtitle.Length]);
        }

        return text;
    }

    private static string? AutodocOemList(IDocument document)
    {
        var values = document.QuerySelectorAll(".product-oem__link, .product-oem__item, [data-oe-number], [data-oem-number]")
            .Select(n => Clean(n.TextContent) ?? Clean(n.GetAttribute("data-oe-number")) ?? Clean(n.GetAttribute("data-oem-number")))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
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
