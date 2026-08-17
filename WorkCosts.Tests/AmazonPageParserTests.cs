using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class AmazonPageParserTests
{
    [Fact]
    public void Recognises_amazon_hosts()
    {
        Assert.True(ProductPageMetadataParser.IsAmazonHost("www.amazon.co.uk"));
        Assert.True(ProductPageMetadataParser.IsAmazonHost("www.amazon.com"));
        Assert.False(ProductPageMetadataParser.IsAmazonHost("www.halfords.com"));
    }

    public static TheoryData<AmazonProductCase> Cases()
    {
        var data = new TheoryData<AmazonProductCase>();
        foreach (var item in AmazonProductCases.All)
        {
            data.Add(item);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Parses_expected_amazon_fields(AmazonProductCase product)
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
    }
}

public sealed record AmazonProductCase(
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

public static class AmazonProductCases
{
    public static IReadOnlyList<AmazonProductCase> All { get; } =
    [
        new(
            "https://www.amazon.co.uk/gp/product/B00S18BI9A/ref=ox_sc_act_title_1?smid=AF2BFX2260HYF&th=1",
            "Sealey DRP07 Oil/Coolant Fluid Drain Pan - Capture/Recycle/Reuse 10L",
            "Sealey",
            "DRP07",
            29.32m,
            null,
            "05024209489607",
            "10L",
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "amazon-B00S18BI9A.snippet.html")),
        new(
            "https://www.amazon.co.uk/gp/product/B00252B7HQ/ref=ox_sc_act_title_8?smid=A3P5ROKL5A1OLE&psc=1",
            "MANN-FILTER, C 32 191 Air Filter",
            "MANN & HUMMEL GMBH",
            "C 32 191",
            12.55m,
            "Amazon",
            "04011558351205",
            null,
            "VW GROUP (AUDI/SEAT/SKODA/VW) 7H0129620, 7E0129620",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "amazon-B00252B7HQ.snippet.html")),
        new(
            "https://www.amazon.co.uk/gp/product/B07DNDFJFX/ref=ox_sc_act_title_15?smid=A3P5ROKL5A1OLE&psc=1",
            "US PRO 1/2 Dr Quick Release Straight Ratchet 4158",
            "US PRO",
            "4158",
            10.90m,
            "Chawla Industries UK LTD",
            null,
            null,
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "amazon-B07DNDFJFX.snippet.html"))
    ];
}
