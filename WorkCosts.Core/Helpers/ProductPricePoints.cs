namespace WorkCosts.Helpers;

public sealed record PricePointOption(string Value, string Label);

public static class ProductPricePoints
{
    public static IReadOnlyList<PricePointOption> Options { get; } =
    [
        new("Low", "Low"),
        new("Medium-low", "Medium-low"),
        new("Medium-high", "Medium-high"),
        new("OEM", "OEM"),
        new("OEM+", "OEM+"),
        new("High", "High")
    ];

    public static PricePointOption? Find(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Options.FirstOrDefault(o =>
            string.Equals(o.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
