using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WorkCosts.Data;

public sealed class TiresaddictGenerationRow
{
    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("gen")]
    public string? Gen { get; set; }

    [JsonPropertyName("mod")]
    public string? Mod { get; set; }

    [JsonPropertyName("start_year")]
    public string? StartYear { get; set; }

    [JsonPropertyName("end_year")]
    public string? EndYear { get; set; }
}

public static class CarDetailsCatalogueMapper
{
    private static readonly Regex LastParens = new(@"\(([^()]*)\)\s*$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<CarDetailsJsonRow> Map(
        IReadOnlyList<TiresaddictGenerationRow> source,
        Func<string, string, string?>? chassisLookup = null,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.Now;
        var minYear = CarDetailsCommands.MinModelYear;
        var maxYear = CarDetailsCommands.MaxModelYear(now);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<CarDetailsJsonRow>();

        foreach (var item in source)
        {
            var brand = item.Brand?.Trim();
            var modelLine = item.Model?.Trim();
            var mod = item.Mod?.Trim();
            if (string.IsNullOrWhiteSpace(brand)
                || string.IsNullOrWhiteSpace(modelLine)
                || string.IsNullOrWhiteSpace(mod))
            {
                continue;
            }

            if (mod.Length > CarDetailsCommands.MaxModelLength)
            {
                continue;
            }

            if (!int.TryParse(item.StartYear, out var year) || year < minYear || year > maxYear)
            {
                continue;
            }

            int? endYear = null;
            if (!string.IsNullOrWhiteSpace(item.EndYear)
                && int.TryParse(item.EndYear, out var parsedEnd)
                && parsedEnd >= minYear
                && parsedEnd <= maxYear
                && parsedEnd >= year)
            {
                endYear = parsedEnd;
            }

            var chassis = ChassisFromMod(mod);
            if (string.IsNullOrWhiteSpace(chassis))
            {
                try
                {
                    chassis = chassisLookup?.Invoke(brand, modelLine);
                }
                catch
                {
                    chassis = null;
                }

                if (string.IsNullOrWhiteSpace(chassis))
                {
                    chassis = modelLine;
                }
            }

            chassis = TruncateChassis(chassis);
            if (string.IsNullOrWhiteSpace(chassis))
            {
                continue;
            }

            var typeKey = CarDetailsCommands.NormalizeTypeKey(brand, mod, chassis, year);
            if (!seen.Add(typeKey))
            {
                continue;
            }

            rows.Add(new CarDetailsJsonRow(
                GuidFromTypeKey(typeKey),
                brand,
                mod,
                chassis,
                year,
                endYear));
        }

        return rows;
    }

    public static string? ChassisFromMod(string mod)
    {
        var match = LastParens.Match(mod.Trim());
        if (!match.Success)
        {
            return null;
        }

        return TruncateChassis(match.Groups[1].Value.Replace('\\', ' ').Trim());
    }

    public static string TruncateChassis(string chassis)
    {
        var trimmed = chassis.Trim();
        if (trimmed.Length <= CarDetailsCommands.MaxModelNumberLength)
        {
            return trimmed;
        }

        var first = trimmed.Split(',', 2)[0].Trim();
        first = first.Split('/', 2)[0].Trim();
        if (first.Length > CarDetailsCommands.MaxModelNumberLength)
        {
            first = first[..CarDetailsCommands.MaxModelNumberLength];
        }

        return first;
    }

    public static Guid GuidFromTypeKey(string typeKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("willidiy.cardetails/" + typeKey));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16));
    }
}
