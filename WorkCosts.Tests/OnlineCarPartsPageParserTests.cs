using WorkCosts.Helpers;
using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class OnlineCarPartsPageParserTests
{
    [Fact]
    public void Recognises_onlinecarparts_hosts()
    {
        Assert.True(ProductPageMetadataParser.IsOnlineCarPartsHost("www.onlinecarparts.co.uk"));
        Assert.True(ProductPageMetadataParser.IsOnlineCarPartsHost("onlinecarparts.co.uk"));
        Assert.False(ProductPageMetadataParser.IsOnlineCarPartsHost("www.autodoc.co.uk"));
        Assert.False(ProductPageMetadataParser.IsOnlineCarPartsHost("www.amazon.co.uk"));
        Assert.False(ProductPageMetadataParser.IsAutodocHost("www.onlinecarparts.co.uk"));
        Assert.False(ProductPageMetadataParser.IsAmazonHost("www.onlinecarparts.co.uk"));
        Assert.Equal(
            "Online Car Parts",
            ProductVendorHelper.InferSourceFromUrl("https://www.onlinecarparts.co.uk/ridex-8017007.html"));
    }

    public static TheoryData<OnlineCarPartsProductCase> Cases()
    {
        var data = new TheoryData<OnlineCarPartsProductCase>();
        foreach (var item in OnlineCarPartsProductCases.All)
        {
            data.Add(item);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Parses_expected_onlinecarparts_fields(OnlineCarPartsProductCase product)
    {
        var html = await File.ReadAllTextAsync(product.FixturePath);
        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, product.Url);

        Assert.Equal(product.Name, parsed.Name);
        Assert.Equal(product.UnitPrice, parsed.UnitPrice);
        Assert.Equal(product.Manufacturer, parsed.Manufacturer);
        Assert.Equal(product.ManufacturerReference, parsed.ManufacturerReference);
        Assert.Equal(product.Vendor, parsed.Vendor);
        Assert.Equal("Online Car Parts", parsed.Source);
        Assert.Equal(product.Ean, parsed.Ean);
        Assert.Null(parsed.Capacity);
        Assert.Null(parsed.LengthMm);
        Assert.Null(parsed.WidthMm);
        Assert.Null(parsed.HeightMm);
        Assert.Null(parsed.Cca);
        Assert.Null(parsed.Technology);
        Assert.Equal(product.Axle, ExtraKey(parsed, "axle"));
        Assert.Equal(product.Size, ExtraKey(parsed, "size"));
        Assert.Equal(product.Material, ExtraKey(parsed, "material"));
        Assert.Equal(product.Type, ExtraKey(parsed, "type"));
    }

    private static string? ExtraKey(ProductPageMetadata parsed, string key) =>
        parsed.ExtraUnknown is not null && parsed.ExtraUnknown.TryGetValue(key, out var value)
            ? value
            : null;
}

public sealed record OnlineCarPartsProductCase(
    string Url,
    string Name,
    decimal? UnitPrice,
    string? Manufacturer,
    string? ManufacturerReference,
    string? Vendor,
    string? Ean,
    string? Axle,
    string? Size,
    string? Material,
    string? Type,
    string FixturePath)
{
    public override string ToString() => Name;
}

public static class OnlineCarPartsProductCases
{
    public static IReadOnlyList<OnlineCarPartsProductCase> All { get; } =
    [
        new(
            "https://www.onlinecarparts.co.uk/ridex-8017007.html",
            "RIDEX 82B0779 Brake disc for BMW 7 Series, 5 Series, 6 Series Front Axle, 347,8x30mm, 5/6x120, Vented, Cast Iron",
            50.24m,
            "RIDEX",
            "82B0779",
            "Online Car Parts",
            "4059191128518",
            "Front Axle",
            "347,8x30mm",
            "Cast Iron",
            "Vented",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "onlinecarparts-8017007.snippet.html")),
        new(
            "https://www.onlinecarparts.co.uk/ridex-15793852.html",
            "RIDEX 219G0962 Tailgate strut for BMW E61 140N, 253 mm",
            10.24m,
            "RIDEX",
            "219G0962",
            "Online Car Parts",
            "4064138316101",
            null,
            "253 mm",
            "Steel",
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "onlinecarparts-15793852.snippet.html")),
        new(
            "https://www.onlinecarparts.co.uk/nty-18603255.html",
            "NTY NSP-BM-001 Clutch master cylinder for BMW 5 Series, 6 Series",
            25.72m,
            "NTY",
            "NSP-BM-001",
            "Online Car Parts",
            "5902048210371",
            null,
            null,
            null,
            null,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "onlinecarparts-18603255.snippet.html"))
    ];
}
