using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

/// <summary>
/// Isolated HTML for each parser field. These are in-memory pages, not live Amazon.
/// </summary>
public class ProductPageFieldParserTests
{
    private const string AmazonUrl = "https://www.amazon.co.uk/dp/B00FIELD00";

    [Fact]
    public async Task Parses_name_and_strips_amazon_suffix()
    {
        var html = AmazonPage("""
            <span id="productTitle">Widget Prime | Extra marketing, more words</span>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal("Widget Prime", parsed.Name);
    }

    [Fact]
    public async Task Parses_unit_price()
    {
        var html = AmazonPage("""
            <div id="corePrice_feature_div">
              <span class="a-price"><span class="a-offscreen">£18.40</span></span>
            </div>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal(18.40m, parsed.UnitPrice);
    }

    [Fact]
    public async Task Parses_manufacturer()
    {
        var html = AmazonPage("""
            <table><tr><th>Manufacturer</th><td>Acme Ltd</td></tr></table>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal("Acme Ltd", parsed.Manufacturer);
    }

    [Fact]
    public async Task Parses_manufacturer_reference_not_asin()
    {
        var html = AmazonPage("""
            <table>
              <tr><th>ASIN</th><td>B00FIELD00</td></tr>
              <tr><th>Part Number</th><td>PN-77</td></tr>
            </table>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal("PN-77", parsed.ManufacturerReference);
        Assert.NotEqual("B00FIELD00", parsed.ManufacturerReference);
    }

    [Fact]
    public async Task Parses_vendor_from_seller()
    {
        var html = AmazonPage("""
            <a id="sellerProfileTriggerId">Tools Direct Ltd</a>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal("Tools Direct Ltd", parsed.Vendor);
    }

    [Fact]
    public async Task Parses_ean()
    {
        var html = AmazonPage("""
            <table>
              <tr><th>Global Trade Identification Number</th><td>05012345678901</td></tr>
            </table>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal("05012345678901", parsed.Ean);
    }

    [Fact]
    public async Task Parses_variation_from_option_picker_only()
    {
        var html = AmazonPage("""
            <span class="inline-twister-dim-title-value">10L</span>
            <table>
              <tr><th>Colour</th><td>Chrome</td></tr>
              <tr><th>Liquid Volume</th><td>10 Litres</td></tr>
            </table>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal("10L", parsed.Variation);
    }

    [Fact]
    public async Task Ignores_colour_when_there_is_no_option_picker()
    {
        var html = AmazonPage("""
            <div id="twister_feature_div"></div>
            <table><tr><th>Colour</th><td>Chrome</td></tr></table>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Null(parsed.Variation);
    }

    [Fact]
    public async Task Parses_oem_equivalent()
    {
        var html = AmazonPage("""
            <table>
              <tr><th>OEM Equivalent Part Number</th><td>VW 7H0129620</td></tr>
            </table>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);

        Assert.Equal("VW 7H0129620", parsed.OemEquivalent);
    }

    [Fact]
    public async Task Field_cases_cover_every_client_value()
    {
        var html = AmazonPage("""
            <span id="productTitle">Full Widget</span>
            <div id="corePrice_feature_div">
              <span class="a-price"><span class="a-offscreen">£1.00</span></span>
            </div>
            <a id="sellerProfileTriggerId">Seller Co</a>
            <span class="inline-twister-dim-title-value">Red</span>
            <table>
              <tr><th>Manufacturer</th><td>Maker</td></tr>
              <tr><th>Manufacturer Part Number</th><td>MP-1</td></tr>
              <tr><th>Global Trade Identification Number</th><td>4012345678901</td></tr>
              <tr><th>OEM Equivalent Part Number</th><td>OEM-1</td></tr>
            </table>
            """);

        var parsed = await ProductPageMetadataParser.ParseHtmlAsync(html, AmazonUrl);
        var client = ProductPageClientValues.From(parsed);

        Assert.Equal("Full Widget", client.Name);
        Assert.Equal("Maker", client.Manufacturer);
        Assert.Equal("MP-1", client.ManufacturerReference);
        Assert.Equal(1.00m, client.UnitPrice);
        Assert.Equal("Seller Co", client.Vendor);
        Assert.Equal("4012345678901", client.Ean);
        Assert.Equal("Red", client.Variation);
        Assert.Equal("OEM-1", client.OemEquivalent);
    }

    private static string AmazonPage(string body) =>
        $"<!DOCTYPE html><html><body>{body}</body></html>";
}
