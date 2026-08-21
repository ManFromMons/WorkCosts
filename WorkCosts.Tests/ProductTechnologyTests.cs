using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public class ProductTechnologyTests
{
    [Theory]
    [InlineData("Standard Wet Battery", "Wet")]
    [InlineData("SMF", "SMF")]
    [InlineData("AGM", "AGM")]
    [InlineData("EFB Start-Stop", "EFB")]
    [InlineData("Gel battery", "Gel")]
    [InlineData("Li-ion pack", "Lithium")]
    [InlineData("sealed maintenance free", "SMF")]
    [InlineData("flooded lead acid", "Wet")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("NiMH", null)]
    [InlineData(null, null)]
    public void Normalises_page_phrases(string? page, string? expected)
    {
        Assert.Equal(expected, ProductTechnology.Normalize(page));
    }
}
