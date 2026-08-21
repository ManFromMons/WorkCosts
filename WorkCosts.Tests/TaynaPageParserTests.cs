using WorkCosts.Helpers;
using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class TaynaPageParserTests
{
    [Fact]
    public void Recognises_tayna_hosts()
    {
        Assert.True(ProductPageMetadataParser.IsTaynaHost("www.tayna.co.uk"));
        Assert.True(ProductPageMetadataParser.IsTaynaHost("tayna.co.uk"));
        Assert.False(ProductPageMetadataParser.IsTaynaHost("www.amazon.co.uk"));
        Assert.False(ProductPageMetadataParser.IsTaynaHost("www.eurocarparts.com"));
        Assert.False(ProductPageMetadataParser.IsAmazonHost("tayna.co.uk"));
        Assert.False(ProductPageMetadataParser.IsAutodocHost("tayna.co.uk"));
        Assert.False(ProductPageMetadataParser.IsEuroCarPartsHost("tayna.co.uk"));
        Assert.False(ProductPageMetadataParser.IsCarBatteryMarketHost("tayna.co.uk"));
        Assert.Equal("Tayna", ProductVendorHelper.InferSourceFromUrl("https://www.tayna.co.uk/car-batteries/bosch/s5a11/"));
    }

    public static TheoryData<TaynaProductCase> Cases()
    {
        var data = new TheoryData<TaynaProductCase>();
        foreach (var item in TaynaProductCases.All)
        {
            data.Add(item);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Parses_expected_tayna_fields(TaynaProductCase product)
    {
        var html = await File.ReadAllTextAsync(product.FixturePath);
        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, product.Url);

        Assert.Equal(product.Name, parsed.Name);
        Assert.Equal(product.UnitPrice, parsed.UnitPrice);
        Assert.Equal(product.Manufacturer, parsed.Manufacturer);
        Assert.Equal(product.ManufacturerReference, parsed.ManufacturerReference);
        Assert.Equal(product.Vendor, parsed.Vendor);
        Assert.Equal("Tayna", parsed.Source);
        Assert.Equal(product.Ean, parsed.Ean);
        Assert.Equal(product.Capacity, parsed.Capacity);
        Assert.Equal(product.LengthMm, parsed.LengthMm);
        Assert.Equal(product.WidthMm, parsed.WidthMm);
        Assert.Equal(product.HeightMm, parsed.HeightMm);
        Assert.Equal(product.Cca, parsed.Cca);
        Assert.Equal(product.Technology, parsed.Technology);
    }
}

public sealed record TaynaProductCase(
    string Url,
    string Name,
    decimal? UnitPrice,
    string? Manufacturer,
    string? ManufacturerReference,
    string? Vendor,
    string? Ean,
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

public static class TaynaProductCases
{
    public static IReadOnlyList<TaynaProductCase> All { get; } =
    [
        new(
            "https://www.tayna.co.uk/motorcycle-batteries/exide/e60-n30l-b/",
            "EXIDE E60-N30L-B 12V CONVENTIONAL MOTORCYCLE BATTERY",
            91.73m,
            "Exide",
            "E60-N30L-B",
            "Tayna",
            "3661024033596",
            30,
            185,
            130,
            170,
            300,
            "Wet",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "tayna-e60-n30l-b.snippet.html")),
        new(
            "https://www.tayna.co.uk/car-batteries/bosch/s5a11/",
            "S5 A11 BOSCH AGM CAR BATTERY 12V 80AH TYPE 115 S5A11",
            136.88m,
            "Bosch",
            "S5 A11",
            "Tayna",
            "4047025244350",
            80,
            315,
            175,
            190,
            800,
            "AGM",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "tayna-s5a11.snippet.html")),
        new(
            "https://www.tayna.co.uk/car-batteries/bosch/s4013/",
            "S4 013 BOSCH CAR BATTERY 12V 95AH TYPE 019 S4013",
            97.78m,
            "Bosch",
            "S4 013",
            "Tayna",
            "4047023479471",
            95,
            353,
            175,
            190,
            800,
            "Wet",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "tayna-s4013.snippet.html"))
    ];
}
