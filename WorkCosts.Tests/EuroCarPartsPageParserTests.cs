using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class EuroCarPartsPageParserTests
{
    [Fact]
    public void Recognises_eurocarparts_hosts()
    {
        Assert.True(ProductPageMetadataParser.IsEuroCarPartsHost("www.eurocarparts.com"));
        Assert.True(ProductPageMetadataParser.IsEuroCarPartsHost("eurocarparts.com"));
        Assert.True(ProductPageMetadataParser.IsEuroCarPartsHost("shop.eurocarparts.de"));
        Assert.False(ProductPageMetadataParser.IsEuroCarPartsHost("www.amazon.co.uk"));
        Assert.False(ProductPageMetadataParser.IsEuroCarPartsHost("www.halfords.com"));
        Assert.False(ProductPageMetadataParser.IsAmazonHost("www.eurocarparts.com"));
        Assert.False(ProductPageMetadataParser.IsAutodocHost("www.eurocarparts.com"));
    }

    public static TheoryData<EuroCarPartsProductCase> Cases()
    {
        var data = new TheoryData<EuroCarPartsProductCase>();
        foreach (var item in EuroCarPartsProductCases.All)
        {
            data.Add(item);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Parses_expected_eurocarparts_fields(EuroCarPartsProductCase product)
    {
        var html = await File.ReadAllTextAsync(product.FixturePath);
        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, product.Url);

        Assert.Equal(product.Name, parsed.Name);
        Assert.Equal(product.Manufacturer, parsed.Manufacturer);
        Assert.Equal(product.UnitPrice, parsed.UnitPrice);
        Assert.Equal(product.Vendor, parsed.Vendor);
        Assert.Equal("Euro Car Parts", parsed.Source);
        Assert.Null(parsed.ManufacturerReference);
        Assert.Equal(product.Capacity, parsed.Capacity);
        Assert.Equal(product.LengthMm, parsed.LengthMm);
        Assert.Equal(product.WidthMm, parsed.WidthMm);
        Assert.Equal(product.HeightMm, parsed.HeightMm);
        Assert.Equal(product.Cca, parsed.Cca);
        Assert.Equal(product.Technology, parsed.Technology);
    }
}

public sealed record EuroCarPartsProductCase(
    string Url,
    string Name,
    string? Manufacturer,
    decimal? UnitPrice,
    string? Vendor,
    int? Capacity,
    int? LengthMm,
    int? WidthMm,
    int? HeightMm,
    int? Cca,
    string? Technology,
    string FixturePath)
{
    public override string ToString() => Name;
}

public static class EuroCarPartsProductCases
{
    public static IReadOnlyList<EuroCarPartsProductCase> All { get; } =
    [
        new(
            "https://www.eurocarparts.com/p/crosland-air-filter-502110318",
            "Crosland Air Filter",
            "Crosland",
            22.49m,
            "Euro Car Parts",
            null,
            null,
            null,
            null,
            null,
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "eurocarparts-502110318.snippet.html")),
        new(
            "https://www.eurocarparts.com/p/bosch-s5a15-agm-stop-start-020-105ah-950cca-car-battery-3-year-guarantee-444779118",
            "Bosch S5A15 AGM Stop/Start 020 105AH 950CCA Car Battery - 3 Year Guarantee",
            "Bosch",
            346.49m,
            "Euro Car Parts",
            105,
            393,
            175,
            190,
            950,
            "AGM",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "eurocarparts-444779118.snippet.html")),
        new(
            "https://www.eurocarparts.com/p/eicher-premium-brake-disc-104110939",
            "Eicher Premium Brake Disc",
            "Eicher",
            45.89m,
            "Euro Car Parts",
            null,
            null,
            null,
            null,
            null,
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "eurocarparts-104110939.snippet.html"))
    ];
}
