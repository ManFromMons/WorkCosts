using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class PasteHtmlParserTests
{
    public static TheoryData<string, string> AmazonAndAutodocFixtures()
    {
        var data = new TheoryData<string, string>();
        foreach (var product in AmazonProductCases.All)
        {
            data.Add(product.Url, product.FixturePath);
        }

        foreach (var product in AutodocProductCases.All)
        {
            data.Add(product.Url, product.FixturePath);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AmazonAndAutodocFixtures))]
    public async Task ParseHtmlAsync_from_string_equals_from_file(string url, string fixturePath)
    {
        var fromFile = await ProductPageMetadataParser.ParseHtmlAsync(
            await File.ReadAllTextAsync(fixturePath),
            url);
        var pasted = await File.ReadAllTextAsync(fixturePath);
        var fromString = await ProductPageMetadataParser.ParseHtmlAsync(pasted, url);

        Assert.Equal(fromFile, fromString);
    }

    [Fact]
    public async Task Host_still_selects_parser()
    {
        var amazon = AmazonProductCases.All[0];
        var autodoc = AutodocProductCases.All[0];
        var amazonHtml = await File.ReadAllTextAsync(amazon.FixturePath);
        var autodocHtml = await File.ReadAllTextAsync(autodoc.FixturePath);

        var amazonOnAmazonHost = await ProductPageMetadataParser.ParseHtmlAsync(amazonHtml, amazon.Url);
        var amazonOnAutodocHost = await ProductPageMetadataParser.ParseHtmlAsync(amazonHtml, autodoc.Url);
        var autodocOnAutodocHost = await ProductPageMetadataParser.ParseHtmlAsync(autodocHtml, autodoc.Url);
        var autodocOnAmazonHost = await ProductPageMetadataParser.ParseHtmlAsync(autodocHtml, amazon.Url);

        Assert.Equal("Amazon", amazonOnAmazonHost.Source);
        Assert.Equal(amazon.Name, amazonOnAmazonHost.Name);
        Assert.NotEqual(amazonOnAmazonHost.Source, amazonOnAutodocHost.Source);

        Assert.Equal("Autodoc", autodocOnAutodocHost.Source);
        Assert.Equal(autodoc.Name, autodocOnAutodocHost.Name);
        Assert.NotEqual(autodocOnAutodocHost.Source, autodocOnAmazonHost.Source);
    }
}
