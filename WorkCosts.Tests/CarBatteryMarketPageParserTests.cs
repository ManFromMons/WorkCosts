using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class CarBatteryMarketPageParserTests
{
    [Fact]
    public void Recognises_carbatterymarket_hosts()
    {
        Assert.True(ProductPageMetadataParser.IsCarBatteryMarketHost("carbatterymarket.co.uk"));
        Assert.True(ProductPageMetadataParser.IsCarBatteryMarketHost("www.carbatterymarket.co.uk"));
        Assert.False(ProductPageMetadataParser.IsCarBatteryMarketHost("www.amazon.co.uk"));
        Assert.False(ProductPageMetadataParser.IsCarBatteryMarketHost("www.eurocarparts.com"));
        Assert.False(ProductPageMetadataParser.IsAmazonHost("carbatterymarket.co.uk"));
        Assert.False(ProductPageMetadataParser.IsAutodocHost("carbatterymarket.co.uk"));
        Assert.False(ProductPageMetadataParser.IsEuroCarPartsHost("carbatterymarket.co.uk"));
    }

    public static TheoryData<CarBatteryMarketProductCase> Cases()
    {
        var data = new TheoryData<CarBatteryMarketProductCase>();
        foreach (var item in CarBatteryMarketProductCases.All)
        {
            data.Add(item);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Parses_expected_carbatterymarket_fields(CarBatteryMarketProductCase product)
    {
        var html = await File.ReadAllTextAsync(product.FixturePath);
        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, product.Url);

        Assert.Equal(product.Name, parsed.Name);
        Assert.Equal(product.Manufacturer, parsed.Manufacturer);
        Assert.Equal(product.ManufacturerReference, parsed.ManufacturerReference);
        Assert.Equal(product.UnitPrice, parsed.UnitPrice);
        Assert.Equal(product.Vendor, parsed.Vendor);
        Assert.Equal("Car Battery Market", parsed.Source);
        Assert.Equal(product.Capacity, parsed.Capacity);
        Assert.Equal(product.LengthMm, parsed.LengthMm);
        Assert.Equal(product.WidthMm, parsed.WidthMm);
        Assert.Equal(product.HeightMm, parsed.HeightMm);
        Assert.Equal(product.Cca, parsed.Cca);
        Assert.Equal(product.Technology, parsed.Technology);
    }
}

public sealed record CarBatteryMarketProductCase(
    string Url,
    string Name,
    string? Manufacturer,
    string? ManufacturerReference,
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

public static class CarBatteryMarketProductCases
{
    public static IReadOnlyList<CarBatteryMarketProductCase> All { get; } =
    [
        new(
            "https://carbatterymarket.co.uk/yuasa/ybx5020",
            "Yuasa YBX5020 12V 110Ah 900A/EN Car Battery - Type 020",
            "Yuasa",
            "YBX5020",
            148.97m,
            "Car Battery Market",
            110,
            393,
            175,
            190,
            950,
            "Wet",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "carbatterymarket-ybx5020.snippet.html")),
        new(
            "https://carbatterymarket.co.uk/dynamp/de110",
            "Dynamp DE110 SMF 110Ah 850CCA 12V Car Battery (Type 020)",
            "Dynamp",
            "DE110",
            98.50m,
            "Car Battery Market",
            110,
            393,
            174,
            189,
            850,
            "SMF",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "carbatterymarket-de110.snippet.html")),
        new(
            "https://carbatterymarket.co.uk/bosch/s5-a13",
            "Bosch S5A13 Start-Stop AGM 95Ah 850A Type 019 12V Car Battery",
            "Bosch",
            "S5A13",
            167.52m,
            "Car Battery Market",
            95,
            353,
            175,
            190,
            850,
            "AGM",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "carbatterymarket-s5-a13.snippet.html"))
    ];
}
