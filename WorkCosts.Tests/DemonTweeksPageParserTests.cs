using WorkCosts.Helpers;
using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class DemonTweeksPageParserTests
{
    [Fact]
    public void Recognises_demon_tweeks_hosts()
    {
        Assert.True(ProductPageMetadataParser.IsDemonTweeksHost("www.demon-tweeks.com"));
        Assert.True(ProductPageMetadataParser.IsDemonTweeksHost("demon-tweeks.com"));
        Assert.False(ProductPageMetadataParser.IsDemonTweeksHost("www.amazon.co.uk"));
        Assert.False(ProductPageMetadataParser.IsDemonTweeksHost("www.autodoc.co.uk"));
        Assert.False(ProductPageMetadataParser.IsAmazonHost("www.demon-tweeks.com"));
        Assert.False(ProductPageMetadataParser.IsAutodocHost("www.demon-tweeks.com"));
        Assert.True(ProductPageMetadataParser.RequiresChromiumFetch("www.demon-tweeks.com"));
        Assert.Equal(
            "Demon Tweeks",
            ProductVendorHelper.InferSourceFromUrl(
                "https://www.demon-tweeks.com/uk/laser-tools-racing-karting-tool-kit-36pc-t-clas8058/"));
    }

    public static TheoryData<DemonTweeksProductCase> Cases()
    {
        var data = new TheoryData<DemonTweeksProductCase>();
        foreach (var item in DemonTweeksProductCases.All)
        {
            data.Add(item);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Parses_expected_demon_tweeks_fields(DemonTweeksProductCase product)
    {
        var html = await File.ReadAllTextAsync(product.FixturePath);
        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, product.Url);

        Assert.Equal(product.Name, parsed.Name);
        Assert.Equal(product.UnitPrice, parsed.UnitPrice);
        Assert.Equal(product.Manufacturer, parsed.Manufacturer);
        Assert.Equal(product.ManufacturerReference, parsed.ManufacturerReference);
        Assert.Equal(product.Vendor, parsed.Vendor);
        Assert.Equal("Demon Tweeks", parsed.Source);
    }
}

public sealed record DemonTweeksProductCase(
    string Url,
    string Name,
    decimal? UnitPrice,
    string? Manufacturer,
    string? ManufacturerReference,
    string? Vendor,
    string FixturePath)
{
    public override string ToString() => Name;
}

public static class DemonTweeksProductCases
{
    public static IReadOnlyList<DemonTweeksProductCase> All { get; } =
    [
        new(
            "https://www.demon-tweeks.com/uk/laser-tools-racing-karting-tool-kit-36pc-t-clas8058/",
            "Laser Tools Racing Karting Tool Kit 36pc",
            228.37m,
            "Laser Tools",
            "CLAS8058",
            "Demon Tweeks",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "demon-tweeks-clas8058.snippet.html")),
        new(
            "https://www.demon-tweeks.com/uk/pitking-products-fluid-oil-suction-syringe-500ml-pkpfs500ml/",
            "Pitking Products Fluid / Oil Suction Syringe - 500ml",
            13.14m,
            "Pitking Products",
            "FS500ML",
            "Demon Tweeks",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "demon-tweeks-fs500ml.snippet.html")),
        new(
            "https://www.demon-tweeks.com/uk/stahlbus-oil-drain-valve-245980/",
            "Stahlbus Oil Drain Valve",
            36.28m,
            "Stahlbus",
            null,
            "Demon Tweeks",
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "demon-tweeks-245980.snippet.html"))
    ];
}
