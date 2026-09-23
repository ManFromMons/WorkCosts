using System.Globalization;

namespace WorkCosts.Data;

public static class CarDetailsTypeLookup
{
    public const int MaxSuggestions = 25;

    public static string[] Terms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string Haystack(string make, string model, string modelNumber, int year, int? endYear)
    {
        var yearText = year.ToString(CultureInfo.InvariantCulture);
        var endText = endYear is int end ? end.ToString(CultureInfo.InvariantCulture) : string.Empty;
        return string.Join(' ', make, model, modelNumber, yearText, endText);
    }

    public static bool Matches(string haystack, string? query)
    {
        var terms = Terms(query);
        if (terms.Length == 0)
        {
            return false;
        }

        foreach (var term in terms)
        {
            if (!haystack.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static List<T> Filter<T>(
        IEnumerable<T> items,
        Func<T, string> haystack,
        string? query,
        int max = MaxSuggestions)
    {
        var matches = new List<T>();
        foreach (var item in items)
        {
            if (!Matches(haystack(item), query))
            {
                continue;
            }

            matches.Add(item);
            if (matches.Count >= max)
            {
                break;
            }
        }

        return matches;
    }
}
