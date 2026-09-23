using System.Text.Json;
using WorkCosts.Data;

var skipWikidata = args.Contains("--skip-wikidata", StringComparer.OrdinalIgnoreCase);
var repo = FindRepo(AppContext.BaseDirectory) ?? Directory.GetCurrentDirectory();
var sourcePath = Path.Combine(repo, "docs", "data", "tiresaddict-gen.json");
var destPath = Path.Combine(repo, "WorkCosts.Core", "Data", "car-details.json");

if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine("Missing " + sourcePath);
    return 1;
}

var source = JsonSerializer.Deserialize<List<TiresaddictGenerationRow>>(
    await File.ReadAllTextAsync(sourcePath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

Func<string, string, string?>? lookup = null;
if (!skipWikidata)
{
    lookup = (brand, model) =>
        WikidataChassisLookup.TryGetChassisAsync(brand, model).GetAwaiter().GetResult();
}

var rows = CarDetailsCatalogueMapper.Map(source, lookup);
var json = JsonSerializer.Serialize(
    rows,
    new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    });
await File.WriteAllTextAsync(destPath, json);
Console.WriteLine($"Wrote {rows.Count} types to {destPath}");
return 0;

static string? FindRepo(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "WorkCosts.slnx")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return null;
}
