using Moq;
using System.Reflection;
using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

/// <summary>
/// Client-contract tests: the app only consumes <see cref="ProductPageClientValues"/>
/// produced from parser output. The parser is mocked so each returned field is asserted
/// without fetching Amazon.
/// </summary>
public class ProductPageClientContractTests
{
    private const string AnyHtml = "<html></html>";
    private const string AnyAmazonUrl = "https://www.amazon.co.uk/dp/B000000000";

    [Fact]
    public void Client_values_expose_every_parser_field()
    {
        var metadataFields = FieldNames(typeof(ProductPageMetadata), nameof(ProductPageMetadata.Empty));
        var clientFields = FieldNames(typeof(ProductPageClientValues));
        Assert.Equal(metadataFields, clientFields);
    }

    [Fact]
    public async Task Mocked_parser_maps_every_field_to_the_client()
    {
        var metadata = FullMetadata();
        var parser = MockParser(metadata);

        var parsed = await parser.ParseHtmlAsync(AnyHtml, AnyAmazonUrl);
        var client = ProductPageClientValues.From(parsed);

        Assert.Equal("Name X", client.Name);
        Assert.Equal("Mfr X", client.Manufacturer);
        Assert.Equal("Ref X", client.ManufacturerReference);
        Assert.Equal(12.34m, client.UnitPrice);
        Assert.Equal("Vendor X", client.Vendor);
        Assert.Equal("5012345678900", client.Ean);
        Assert.Equal("10L", client.Variation);
        Assert.Equal("OEM X", client.OemEquivalent);
        Assert.Equal("Amazon", client.Source);
        Assert.Equal(110, client.Capacity);
        Assert.Equal(393, client.LengthMm);
        Assert.Equal(175, client.WidthMm);
        Assert.Equal(190, client.HeightMm);
        Assert.Equal(950, client.Cca);
        Assert.Equal("Wet", client.Technology);
    }

    [Fact]
    public async Task Mocked_parser_blank_fields_do_not_overwrite_client_values()
    {
        var parser = MockParser(ProductPageMetadata.Empty);

        var parsed = await parser.ParseHtmlAsync(AnyHtml, AnyAmazonUrl);
        var client = ProductPageClientValues.From(parsed);

        Assert.Null(client.Name);
        Assert.Null(client.Manufacturer);
        Assert.Null(client.ManufacturerReference);
        Assert.Null(client.UnitPrice);
        Assert.Null(client.Vendor);
        Assert.Null(client.Ean);
        Assert.Null(client.Variation);
        Assert.Null(client.OemEquivalent);
        Assert.Null(client.Capacity);
        Assert.Null(client.LengthMm);
        Assert.Null(client.WidthMm);
        Assert.Null(client.HeightMm);
        Assert.Null(client.Cca);
        Assert.Null(client.Technology);
    }

    [Theory]
    [MemberData(nameof(SingleFieldCases))]
    public async Task Mocked_parser_returns_only_the_named_field(
        string field,
        ProductPageMetadata metadata,
        object? expected)
    {
        var parser = MockParser(metadata);

        var parsed = await parser.ParseHtmlAsync(AnyHtml, AnyAmazonUrl);
        var client = ProductPageClientValues.From(parsed);
        var actual = typeof(ProductPageClientValues).GetProperty(field)!.GetValue(client);

        Assert.Equal(expected, actual);
        AssertAllOtherFieldsNull(client, field);
    }

    [Fact]
    public void Whitespace_and_negative_price_are_treated_as_unset()
    {
        var metadata = new ProductPageMetadata(
            "  ",
            "\t",
            null,
            -1m,
            " ",
            "",
            null,
            "  OEM  ",
            Capacity: -4,
            Technology: "  ");

        var client = ProductPageClientValues.From(metadata);

        Assert.Null(client.Name);
        Assert.Null(client.Manufacturer);
        Assert.Null(client.ManufacturerReference);
        Assert.Null(client.UnitPrice);
        Assert.Null(client.Vendor);
        Assert.Null(client.Ean);
        Assert.Null(client.Variation);
        Assert.Equal("OEM", client.OemEquivalent);
        Assert.Null(client.Capacity);
        Assert.Null(client.Technology);
    }

    public static TheoryData<string, ProductPageMetadata, object?> SingleFieldCases() =>
        new()
        {
            { nameof(ProductPageClientValues.Name), Only(name: "Only Name"), "Only Name" },
            { nameof(ProductPageClientValues.Manufacturer), Only(manufacturer: "Only Mfr"), "Only Mfr" },
            { nameof(ProductPageClientValues.ManufacturerReference), Only(mfrRef: "Only Ref"), "Only Ref" },
            { nameof(ProductPageClientValues.UnitPrice), Only(price: 9.99m), 9.99m },
            { nameof(ProductPageClientValues.Vendor), Only(vendor: "Only Vendor"), "Only Vendor" },
            { nameof(ProductPageClientValues.Ean), Only(ean: "5012345678900"), "5012345678900" },
            { nameof(ProductPageClientValues.Variation), Only(variation: "6L"), "6L" },
            { nameof(ProductPageClientValues.OemEquivalent), Only(oem: "Only OEM"), "Only OEM" },
            { nameof(ProductPageClientValues.Source), Only(source: "Only Source"), "Only Source" },
            { nameof(ProductPageClientValues.Capacity), Only(capacity: 70), 70 },
            { nameof(ProductPageClientValues.LengthMm), Only(lengthMm: 278), 278 },
            { nameof(ProductPageClientValues.WidthMm), Only(widthMm: 175), 175 },
            { nameof(ProductPageClientValues.HeightMm), Only(heightMm: 190), 190 },
            { nameof(ProductPageClientValues.Cca), Only(cca: 640), 640 },
            { nameof(ProductPageClientValues.Technology), Only(technology: "AGM"), "AGM" },
        };

    [Fact]
    public void Single_field_cases_cover_every_client_field()
    {
        var expected = FieldNames(typeof(ProductPageClientValues));
        var covered = SingleFieldCases()
            .Select(row => (string)row[0]!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, covered);
    }

    private static IProductPageParser MockParser(ProductPageMetadata metadata)
    {
        var mock = new Mock<IProductPageParser>(MockBehavior.Strict);
        mock.Setup(p => p.ParseHtmlAsync(AnyHtml, AnyAmazonUrl))
            .ReturnsAsync(metadata);
        return mock.Object;
    }

    private static ProductPageMetadata FullMetadata() =>
        new(
            "Name X",
            "Mfr X",
            "Ref X",
            12.34m,
            "Vendor X",
            "5012345678900",
            "10L",
            "OEM X",
            "Amazon",
            110,
            393,
            175,
            190,
            950,
            "Wet");

    private static ProductPageMetadata Only(
        string? name = null,
        string? manufacturer = null,
        string? mfrRef = null,
        decimal? price = null,
        string? vendor = null,
        string? ean = null,
        string? variation = null,
        string? oem = null,
        string? source = null,
        int? capacity = null,
        int? lengthMm = null,
        int? widthMm = null,
        int? heightMm = null,
        int? cca = null,
        string? technology = null) =>
        new(name, manufacturer, mfrRef, price, vendor, ean, variation, oem, source,
            capacity, lengthMm, widthMm, heightMm, cca, technology);

    private static void AssertAllOtherFieldsNull(ProductPageClientValues client, string except)
    {
        foreach (var property in typeof(ProductPageClientValues).GetProperties())
        {
            if (property.Name == except)
            {
                continue;
            }

            Assert.True(
                property.GetValue(client) is null,
                $"{property.Name} should stay unset when only {except} is returned.");
        }
    }

    private static string[] FieldNames(Type type, params string[] exclude) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => !exclude.Contains(n, StringComparer.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
}
