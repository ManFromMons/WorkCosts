using WorkCosts.Data;
using Xunit;

namespace WorkCosts.Tests;

public sealed class CarDetailsTypeLookupTests
{
    private const string BmwE60 = "BMW 5 series (E60) E60 2004 2010";
    private const string JaguarX350 = "Jaguar XJ III (X350/X358) X350/X358 2003 2009";

    [Fact]
    public void Terms_BmwE60_AreTwoSearchTerms()
    {
        Assert.Equal(["BMW", "E60"], CarDetailsTypeLookup.Terms("BMW E60"));
    }

    [Fact]
    public void Matches_TwoTerms_RequireBothWildcards()
    {
        Assert.True(CarDetailsTypeLookup.Matches(BmwE60, "BMW E60"));
        Assert.True(CarDetailsTypeLookup.Matches(BmwE60, "bmw e60"));
        Assert.False(CarDetailsTypeLookup.Matches(BmwE60, "BMW X5"));
        Assert.False(CarDetailsTypeLookup.Matches(JaguarX350, "BMW E60"));
    }

    [Fact]
    public void Matches_SingleTerm_IsWildcardContains()
    {
        Assert.True(CarDetailsTypeLookup.Matches(BmwE60, "60"));
        Assert.True(CarDetailsTypeLookup.Matches(BmwE60, "series"));
        Assert.False(CarDetailsTypeLookup.Matches(BmwE60, "E90"));
    }

    [Fact]
    public void Matches_EmptyQuery_IsFalse()
    {
        Assert.False(CarDetailsTypeLookup.Matches(BmwE60, ""));
        Assert.False(CarDetailsTypeLookup.Matches(BmwE60, "   "));
        Assert.Empty(CarDetailsTypeLookup.Terms(null));
    }

    [Fact]
    public void Filter_KeepsInputOrder_AndCapsSuggestions()
    {
        var items = Enumerable.Range(0, 40)
            .Select(i => $"BMW row {i} E60 2004")
            .ToList();

        var matches = CarDetailsTypeLookup.Filter(items, haystack => haystack, "BMW E60");
        Assert.Equal(CarDetailsTypeLookup.MaxSuggestions, matches.Count);
        Assert.Equal("BMW row 0 E60 2004", matches[0]);
        Assert.Equal("BMW row 24 E60 2004", matches[^1]);
    }

    [Fact]
    public void Haystack_IncludesMakeModelChassisAndYears()
    {
        var haystack = CarDetailsTypeLookup.Haystack("BMW", "5 series (E60)", "E60", 2004, 2010);
        Assert.True(CarDetailsTypeLookup.Matches(haystack, "BMW E60"));
        Assert.True(CarDetailsTypeLookup.Matches(haystack, "2004"));
        Assert.True(CarDetailsTypeLookup.Matches(haystack, "2010"));
    }
}
