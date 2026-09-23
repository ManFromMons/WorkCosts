using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkCosts.Data;

public static class WikidataChassisLookup
{
    public const string Endpoint = "https://query.wikidata.org/sparql";
    public const string UserAgent = "WillIDIY-CarDetailsSeed/1.0 (offline catalogue generator; not the shipping app)";

    private static readonly Regex Parens = new(@"\(([^()]*)\)", RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<string?> TryGetChassisAsync(
        string brand,
        string model,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"{brand.Trim()}|{model.Trim()}";
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        string? chassis = null;
        try
        {
            chassis = await QueryAsync(brand, model, client, cancellationToken);
        }
        catch
        {
            chassis = null;
        }

        lock (Cache)
        {
            Cache[key] = chassis;
        }

        return chassis;
    }

    private static async Task<string?> QueryAsync(
        string brand,
        string model,
        HttpClient? client,
        CancellationToken cancellationToken)
    {
        var owned = false;
        if (client is null)
        {
            client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            owned = true;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(brand, model));
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || !results.TryGetProperty("bindings", out var bindings))
            {
                return null;
            }

            foreach (var binding in bindings.EnumerateArray())
            {
                if (binding.TryGetProperty("itemLabel", out var labelNode)
                    && labelNode.TryGetProperty("value", out var value)
                    && value.GetString() is string label)
                {
                    var match = Parens.Match(label);
                    if (match.Success)
                    {
                        var code = CarDetailsCatalogueMapper.TruncateChassis(match.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            return code;
                        }
                    }
                }
            }

            return null;
        }
        finally
        {
            if (owned)
            {
                client.Dispose();
            }
        }
    }

    internal static string BuildUrl(string brand, string model)
    {
        var brandLit = EscapeSparql(brand);
        var modelLit = EscapeSparql(model);
        var sparql =
            "SELECT ?item ?itemLabel WHERE { " +
            "?item rdfs:label ?itemLabel . " +
            "FILTER(LANG(?itemLabel) = \"en\") " +
            "FILTER(CONTAINS(LCASE(?itemLabel), LCASE(\"" + brandLit + "\"))) " +
            "FILTER(CONTAINS(LCASE(?itemLabel), LCASE(\"" + modelLit + "\"))) " +
            "} LIMIT 5";
        return Endpoint + "?query=" + Uri.EscapeDataString(sparql);
    }

    private static string EscapeSparql(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
