using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;

namespace WorkCosts.Services;

public enum MdecoderPageKind
{
    Wait,
    Ready,
    Challenge,
    Failed,
}

public sealed record MdecoderOption(string Code, string Description);

public sealed record MdecoderDecode(
    string Vin,
    string? ProductionDate,
    string? Type,
    string? Model,
    string? Steering,
    string? Engine,
    string? Transmission,
    string? Color,
    string? Upholstery,
    IReadOnlyList<MdecoderOption> Options);

public static class MdecoderPageParser
{
    private static readonly Regex VinPattern = new(
        @"\b[A-HJ-NPR-Z0-9]{17}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static MdecoderPageKind Classify(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return MdecoderPageKind.Failed;
        }

        if (LooksLikeChallenge(html))
        {
            return MdecoderPageKind.Challenge;
        }

        if (LooksLikeLimitOrMissing(html) && !HasProductionDate(html))
        {
            return MdecoderPageKind.Failed;
        }

        if (HasProductionDate(html))
        {
            return MdecoderPageKind.Ready;
        }

        if (LooksLikeWait(html))
        {
            return MdecoderPageKind.Wait;
        }

        return MdecoderPageKind.Failed;
    }

    public static async Task<MdecoderDecode?> ParseReadyAsync(string html, CancellationToken cancellationToken = default)
    {
        if (Classify(html) != MdecoderPageKind.Ready)
        {
            return null;
        }

        var browsing = BrowsingContext.New(Configuration.Default);
        var document = await browsing.OpenAsync(req => req.Content(html), cancellationToken);
        var fields = ReadLabeledValues(document);
        var vin = First(fields, "vin")
            ?? VinPattern.Match(html).Value;
        if (string.IsNullOrWhiteSpace(vin))
        {
            return null;
        }

        return new MdecoderDecode(
            Vin: vin.Trim().ToUpperInvariant(),
            ProductionDate: First(fields, "production date", "production"),
            Type: First(fields, "type"),
            Model: First(fields, "model"),
            Steering: First(fields, "steering"),
            Engine: First(fields, "engine"),
            Transmission: First(fields, "transmission"),
            Color: First(fields, "color", "colour"),
            Upholstery: First(fields, "upholstery"),
            Options: ReadOptions(document));
    }

    public static bool LooksLikeChallenge(string html)
    {
        var prefix = html.Length > 24_000 ? html[..24_000] : html;
        return prefix.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains("cf-mitigated", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains("Enable JavaScript and cookies to continue", StringComparison.OrdinalIgnoreCase)
            || prefix.Contains("Performing security verification", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasProductionDate(string html) =>
        html.Contains("Production Date", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeWait(string html) =>
        html.Contains("please wait", StringComparison.OrdinalIgnoreCase)
        || html.Contains("being processed", StringComparison.OrdinalIgnoreCase)
        || html.Contains("wait a few seconds", StringComparison.OrdinalIgnoreCase)
        || html.Contains("your vin is processing", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeLimitOrMissing(string html) =>
        html.Contains("daily limit", StringComparison.OrdinalIgnoreCase)
        || html.Contains("vehicle not found", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ReadLabeledValues(IDocument document)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.QuerySelectorAll("tr"))
        {
            var cells = row.Children
                .Where(child => child.LocalName is "th" or "td")
                .Select(child => Clean(child.TextContent))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            if (cells.Count < 2)
            {
                continue;
            }

            AddField(fields, cells[0], cells[1]);
        }

        var terms = document.QuerySelectorAll("dt").ToList();
        var defs = document.QuerySelectorAll("dd").ToList();
        for (var i = 0; i < Math.Min(terms.Count, defs.Count); i++)
        {
            AddField(fields, Clean(terms[i].TextContent), Clean(defs[i].TextContent));
        }

        return fields;
    }

    private static IReadOnlyList<MdecoderOption> ReadOptions(IDocument document)
    {
        var options = new List<MdecoderOption>();
        IElement? heading = null;
        foreach (var node in document.QuerySelectorAll("h1, h2, h3, h4"))
        {
            if (Clean(node.TextContent).Equals("Options", StringComparison.OrdinalIgnoreCase))
            {
                heading = node;
                break;
            }
        }

        var table = heading?.NextElementSibling;
        while (table is not null && table.LocalName != "table")
        {
            table = table.NextElementSibling;
        }

        if (table is null)
        {
            return options;
        }

        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var cells = row.Children
                .Where(child => child.LocalName is "th" or "td")
                .Select(child => Clean(child.TextContent))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            if (cells.Count < 2)
            {
                continue;
            }

            options.Add(new MdecoderOption(cells[0], cells[1]));
        }

        return options;
    }

    private static void AddField(Dictionary<string, string> fields, string label, string value)
    {
        var key = label.Trim().TrimEnd(':');
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || fields.ContainsKey(key))
        {
            return;
        }

        fields[key] = value;
    }

    private static string? First(Dictionary<string, string> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            foreach (var pair in fields)
            {
                if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }
        }

        return null;
    }

    private static string Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
