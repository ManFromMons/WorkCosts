using WorkCosts.Helpers;
using Xunit;

namespace WorkCosts.Tests;

public class ProductExtraTests
{
    private const string SampleYaml = """
        capacity: 110
        lengthMm: 393
        widthMm: 175
        heightMm: 190
        cca: 950
        technology: Wet
        """;

    [Fact]
    public void Round_trips_the_sample_block()
    {
        var extra = ProductExtra.Parse(SampleYaml);

        Assert.Equal(110, extra.Capacity);
        Assert.Equal(393, extra.LengthMm);
        Assert.Equal(175, extra.WidthMm);
        Assert.Equal(190, extra.HeightMm);
        Assert.Equal(950, extra.Cca);
        Assert.Equal("Wet", extra.Technology);

        var yaml = extra.ToYaml();
        var again = ProductExtra.Parse(yaml);
        Assert.Equal(extra, again);
        Assert.Contains("capacity: 110", yaml, StringComparison.Ordinal);
        Assert.Contains("technology: Wet", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Omits_null_keys()
    {
        var yaml = new ProductExtra { Capacity = 70, Cca = 640 }.ToYaml();

        Assert.Contains("capacity: 70", yaml, StringComparison.Ordinal);
        Assert.Contains("cca: 640", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("lengthMm", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("technology", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_extra_serialises_to_empty_string()
    {
        Assert.Equal(string.Empty, new ProductExtra().ToYaml());
        Assert.Equal(string.Empty, ProductExtra.Parse(null).ToYaml());
        Assert.Equal(string.Empty, ProductExtra.Parse("").ToYaml());
    }

    [Fact]
    public void Preserves_unknown_keys_across_load_and_save()
    {
        var extra = ProductExtra.Parse("capacity: 80\nfoo: bar\n");
        Assert.Equal(80, extra.Capacity);
        Assert.Equal("bar", Convert.ToString(extra.UnknownKeys["foo"]));

        var saved = extra.WithKnown(80, null, null, null, null, null).ToYaml();
        Assert.Contains("foo: bar", saved, StringComparison.Ordinal);
        Assert.Contains("capacity: 80", saved, StringComparison.Ordinal);

        var again = ProductExtra.Parse(saved);
        Assert.Equal("bar", Convert.ToString(again.UnknownKeys["foo"]));
        Assert.Equal(80, again.Capacity);
    }

    [Fact]
    public void Invalid_yaml_returns_empty_extra_without_throwing()
    {
        var extra = ProductExtra.Parse(":\n  - [");
        Assert.Equal(new ProductExtra(), extra);
        Assert.Equal(string.Empty, extra.ToYaml());
    }
}
