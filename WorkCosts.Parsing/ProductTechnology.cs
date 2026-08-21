namespace WorkCosts.Services;

public static class ProductTechnology
{
    public static IReadOnlyList<string> Tokens { get; } =
        ["Wet", "SMF", "AGM", "EFB", "Gel", "Lithium"];

    /// <summary>
    /// Maps a page phrase to a stored technology token. AGM/EFB/SMF/Gel/Lithium are
    /// matched before Wet. Unrecognised or blank input is null.
    /// </summary>
    public static string? Normalize(string? pageText)
    {
        if (string.IsNullOrWhiteSpace(pageText))
        {
            return null;
        }

        var text = pageText.Trim();
        if (Contains(text, "agm"))
        {
            return "AGM";
        }

        if (Contains(text, "efb"))
        {
            return "EFB";
        }

        if (Contains(text, "smf") || Contains(text, "sealed maintenance"))
        {
            return "SMF";
        }

        if (Contains(text, "gel"))
        {
            return "Gel";
        }

        if (Contains(text, "lithium") || Contains(text, "li-ion") || Contains(text, "liion"))
        {
            return "Lithium";
        }

        if (Contains(text, "wet") || Contains(text, "flooded"))
        {
            return "Wet";
        }

        return null;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
