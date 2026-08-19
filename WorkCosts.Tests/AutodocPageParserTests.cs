using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class AutodocPageParserTests
{
    private const string AutodocUrl = "https://www.autodoc.co.uk/connect/22259372";

    [Fact]
    public void Recognises_autodoc_hosts()
    {
        Assert.True(ProductPageMetadataParser.IsAutodocHost("www.autodoc.co.uk"));
        Assert.True(ProductPageMetadataParser.IsAutodocHost("www.autodoc.de"));
        Assert.False(ProductPageMetadataParser.IsAutodocHost("www.amazon.co.uk"));
        Assert.False(ProductPageMetadataParser.IsAutodocHost("www.halfords.com"));
    }

    public static TheoryData<AutodocProductCase> Cases()
    {
        var data = new TheoryData<AutodocProductCase>();
        foreach (var item in AutodocProductCases.All)
        {
            data.Add(item);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Parses_expected_autodoc_fields(AutodocProductCase product)
    {
        var html = await File.ReadAllTextAsync(product.FixturePath);
        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, product.Url);

        Assert.Equal(product.Name, parsed.Name);
        Assert.Equal(product.Manufacturer, parsed.Manufacturer);
        Assert.Equal(product.ManufacturerReference, parsed.ManufacturerReference);
        Assert.Equal(product.UnitPrice, parsed.UnitPrice);
        Assert.Equal(product.Vendor, parsed.Vendor);
        Assert.Equal(product.Ean, parsed.Ean);
        Assert.Equal(product.Variation, parsed.Variation);
        Assert.Equal(product.OemEquivalent, parsed.OemEquivalent);
        Assert.Equal("Autodoc", parsed.Source);
        var catalogId = product.Url.TrimEnd('/').Split('/')[^1];
        Assert.NotEqual(catalogId, parsed.ManufacturerReference);
    }

    [Fact]
    public async Task Parses_html_when_json_ld_is_missing()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Connect 35161 Seal Ring Kit</h1>
            <span class="product-block__article">Article number: 35161</span>
            <span class="product-block__article">EAN: 5018341351618</span>
            <div class="product-block__price-new-wrap">£29. 49</div>
            <p class="sold-by">Sold by AUTODOC</p>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("Connect 35161 Seal Ring Kit", parsed.Name);
        Assert.Equal("35161", parsed.ManufacturerReference);
        Assert.Equal(29.49m, parsed.UnitPrice);
        Assert.Equal("AUTODOC", parsed.Vendor);
        Assert.Equal("5018341351618", parsed.Ean);
        Assert.Equal("Autodoc", parsed.Source);
    }

    [Fact]
    public async Task Parses_manufacturer_from_description_list()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Seal Ring Kit</h1>
            <li class="product-description__item">
              <span class="product-description__item-title">Manufacturer:</span>
              <span class="product-description__item-value">Connect</span>
            </li>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("Connect", parsed.Manufacturer);
    }

    [Fact]
    public async Task Parses_price_from_data_attribute()
    {
        var html = """
            <!DOCTYPE html><html><body>
            <main class="product-page" data-product-page="" data-price="18.40">
              <h1 class="product-block__title">Widget</h1>
            </main>
            </body></html>
            """;

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal(18.40m, parsed.UnitPrice);
    }

    [Fact]
    public async Task Parses_marketplace_vendor()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Widget</h1>
            <p class="sold-by">Sold by PartsCo Ltd</p>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("PartsCo Ltd", parsed.Vendor);
        Assert.Equal("Autodoc", parsed.Source);
    }

    [Fact]
    public async Task Maps_json_ld_seller_url_to_autodoc()
    {
        var html = """
            <!DOCTYPE html><html><body>
            <script type="application/ld+json">
            {
              "@type": "Product",
              "name": "Widget",
              "offers": {
                "price": 1.00,
                "seller": { "name": "https://www.autodoc.co.uk" }
              }
            }
            </script>
            </body></html>
            """;

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("AUTODOC", parsed.Vendor);
    }

    [Fact]
    public async Task Parses_oem_numbers_from_description()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Air Filter</h1>
            <li class="product-description__item">
              <span class="product-description__item-title">OE numbers:</span>
              <span class="product-description__item-value">7H0129620, 7E0129620</span>
            </li>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("7H0129620, 7E0129620", parsed.OemEquivalent);
    }

    [Fact]
    public async Task Parses_oem_list_items()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Air Filter</h1>
            <span class="product-oem__item">7H0129620</span>
            <span class="product-oem__item">7E0129620</span>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("7H0129620, 7E0129620", parsed.OemEquivalent);
    }

    [Fact]
    public async Task Parses_oem_from_product_oem_links()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Air conditioning condenser</h1>
            <ul class="product-oem__list">
              <li><a class="product-oem__link">OE 6450 9122 825 — BMW</a></li>
              <li><a class="product-oem__link">OE 8379885 — BMW</a></li>
            </ul>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("OE 6450 9122 825 — BMW, OE 8379885 — BMW", parsed.OemEquivalent);
    }

    [Fact]
    public async Task Strips_seo_subtitle_from_heading()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">VAN WEZEL 06005267 Air conditioning condenser
              <span class="product-block__seo-subtitle">with dryer, Aluminium, 585mm</span>
            </h1>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("VAN WEZEL 06005267 Air conditioning condenser", parsed.Name);
    }

    [Fact]
    public async Task Does_not_treat_trade_numbers_as_oem()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Connect 35161 Seal Ring Kit</h1>
            <div class="product-block__seo-info-text">Trade numbers: CONNECT 35161</div>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Null(parsed.OemEquivalent);
    }

    [Fact]
    public async Task Strips_autodoc_title_suffix()
    {
        var html = AutodocPage("""
            <meta property="og:title" content="35161 Connect Seal Ring Kit | AUTODOC price and review" />
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);

        Assert.Equal("35161 Connect Seal Ring Kit", parsed.Name);
    }

    [Fact]
    public async Task Field_cases_cover_autodoc_client_values()
    {
        var html = AutodocPage("""
            <h1 class="product-block__title">Full Widget</h1>
            <span class="product-block__article">Article number: MP-1</span>
            <span class="product-block__article">EAN: 4012345678901</span>
            <div class="product-block__price-new-wrap">£1.00</div>
            <p class="sold-by">Sold by Seller Co</p>
            <li class="product-description__item">
              <span class="product-description__item-title">Manufacturer:</span>
              <span class="product-description__item-value">Maker</span>
            </li>
            <li class="product-description__item">
              <span class="product-description__item-title">OE numbers:</span>
              <span class="product-description__item-value">OEM-1</span>
            </li>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AutodocUrl);
        var client = ProductPageClientValues.From(parsed);

        Assert.Equal("Full Widget", client.Name);
        Assert.Equal("Maker", client.Manufacturer);
        Assert.Equal("MP-1", client.ManufacturerReference);
        Assert.Equal(1.00m, client.UnitPrice);
        Assert.Equal("Seller Co", client.Vendor);
        Assert.Equal("4012345678901", client.Ean);
        Assert.Null(client.Variation);
        Assert.Equal("OEM-1", client.OemEquivalent);
        Assert.Equal("Autodoc", client.Source);
    }

    [Fact]
    public async Task Listing_page_does_not_use_first_product_card()
    {
        var html = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "autodoc-engine-oil-12094-5w40.snippet.html"));
        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(
            html,
            "https://www.autodoc.co.uk/car-parts/engine-oil-12094/bmw/5er-reihe/5-e60/17294-545-i?criteria%5B1054%5D%5B%5D=5W-40");

        Assert.Equal("Engine oil for BMW E60 545 i 333 hp Petrol N62 B44 A", parsed.Name);
        Assert.Equal("5W-40", parsed.Variation);
        Assert.Equal("Autodoc", parsed.Source);
        Assert.Null(parsed.Manufacturer);
        Assert.Null(parsed.ManufacturerReference);
        Assert.Null(parsed.UnitPrice);
        Assert.Null(parsed.Vendor);
        Assert.Null(parsed.Ean);
        Assert.Null(parsed.OemEquivalent);
        Assert.NotEqual("1862O0007P", parsed.ManufacturerReference);
        Assert.NotEqual(27.49m, parsed.UnitPrice);
        Assert.DoesNotContain("RIDEX", parsed.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static string AutodocPage(string body) =>
        $"<!DOCTYPE html><html><body>{body}</body></html>";
}

public sealed record AutodocProductCase(
    string Url,
    string Name,
    string? Manufacturer,
    string? ManufacturerReference,
    decimal? UnitPrice,
    string? Vendor,
    string? Ean,
    string? Variation,
    string? OemEquivalent,
    string FixturePath)
{
    public override string ToString() => Name;
}

public static class AutodocProductCases
{
    public static IReadOnlyList<AutodocProductCase> All { get; } =
    [
        new(
            "https://www.autodoc.co.uk/connect/22259372",
            "Connect 35161 Seal Ring Kit",
            "Connect",
            "35161",
            29.49m,
            "AUTODOC",
            "5018341351618",
            null,
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "autodoc-22259372.snippet.html")),
        new(
            "https://www.autodoc.co.uk/van-wezel/1277586",
            "VAN WEZEL 06005267 Air conditioning condenser",
            "VAN WEZEL",
            "06005267",
            91.49m,
            "AUTODOC",
            "5410909287085",
            null,
            "OE 6450 9122 825 — BMW, OE 6450 8379 885 — BMW, OE 6450 2282 939 — BMW, OE 8379885 — BMW, OE 2282939 — BMW, OE 9122825 — BMW, OE 837989 — BMW",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "autodoc-1277586.snippet.html")),
        new(
            "https://www.autodoc.co.uk/car-parts/engine-oil-12094/bmw/5er-reihe/5-e60/17294-545-i?criteria%5B1054%5D%5B%5D=5W-40",
            "Engine oil for BMW E60 545 i 333 hp Petrol N62 B44 A",
            null,
            null,
            null,
            null,
            null,
            "5W-40",
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "autodoc-engine-oil-12094-5w40.snippet.html"))
    ];
}
