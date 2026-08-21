using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkCosts.Helpers;

public sealed class ProductExtra : IEquatable<ProductExtra>
{
    public static IReadOnlyList<string> TechnologyTokens { get; } =
        ["Wet", "SMF", "AGM", "EFB", "Gel", "Lithium"];

    public int? Capacity { get; init; }
    public int? LengthMm { get; init; }
    public int? WidthMm { get; init; }
    public int? HeightMm { get; init; }
    public int? Cca { get; init; }
    public string? Technology { get; init; }
    public IReadOnlyDictionary<string, object?> UnknownKeys { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public static ProductExtra Parse(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new ProductExtra();
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var map = deserializer.Deserialize<Dictionary<object, object?>>(yaml);
            if (map is null || map.Count == 0)
            {
                return new ProductExtra();
            }

            var unknown = new Dictionary<string, object?>(StringComparer.Ordinal);
            int? capacity = null, lengthMm = null, widthMm = null, heightMm = null, cca = null;
            string? technology = null;

            foreach (var (rawKey, rawValue) in map)
            {
                var key = rawKey?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                switch (key)
                {
                    case "capacity":
                        capacity = ToInt(rawValue);
                        break;
                    case "lengthMm":
                        lengthMm = ToInt(rawValue);
                        break;
                    case "widthMm":
                        widthMm = ToInt(rawValue);
                        break;
                    case "heightMm":
                        heightMm = ToInt(rawValue);
                        break;
                    case "cca":
                        cca = ToInt(rawValue);
                        break;
                    case "technology":
                        technology = KnownToken(rawValue?.ToString());
                        break;
                    default:
                        unknown[key] = NormalizeUnknown(rawValue);
                        break;
                }
            }

            return new ProductExtra
            {
                Capacity = capacity,
                LengthMm = lengthMm,
                WidthMm = widthMm,
                HeightMm = heightMm,
                Cca = cca,
                Technology = technology,
                UnknownKeys = unknown
            };
        }
        catch
        {
            return new ProductExtra();
        }
    }

    public string ToYaml()
    {
        var map = new Dictionary<string, object>(StringComparer.Ordinal);
        if (Capacity is int capacity)
        {
            map["capacity"] = capacity;
        }

        if (LengthMm is int lengthMm)
        {
            map["lengthMm"] = lengthMm;
        }

        if (WidthMm is int widthMm)
        {
            map["widthMm"] = widthMm;
        }

        if (HeightMm is int heightMm)
        {
            map["heightMm"] = heightMm;
        }

        if (Cca is int cca)
        {
            map["cca"] = cca;
        }

        if (!string.IsNullOrWhiteSpace(Technology))
        {
            map["technology"] = Technology;
        }

        foreach (var (key, value) in UnknownKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || map.ContainsKey(key) || value is null)
            {
                continue;
            }

            map[key] = value;
        }

        if (map.Count == 0)
        {
            return string.Empty;
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
        return serializer.Serialize(map).TrimEnd();
    }

    public ProductExtra WithKnown(
        int? capacity,
        int? lengthMm,
        int? widthMm,
        int? heightMm,
        int? cca,
        string? technology) =>
        new()
        {
            Capacity = capacity,
            LengthMm = lengthMm,
            WidthMm = widthMm,
            HeightMm = heightMm,
            Cca = cca,
            Technology = KnownToken(technology),
            UnknownKeys = UnknownKeys
        };

    public bool Equals(ProductExtra? other)
    {
        if (other is null)
        {
            return false;
        }

        return Capacity == other.Capacity
            && LengthMm == other.LengthMm
            && WidthMm == other.WidthMm
            && HeightMm == other.HeightMm
            && Cca == other.Cca
            && string.Equals(Technology, other.Technology, StringComparison.Ordinal)
            && UnknownKeysEqual(UnknownKeys, other.UnknownKeys);
    }

    public override bool Equals(object? obj) => Equals(obj as ProductExtra);

    public override int GetHashCode() =>
        HashCode.Combine(Capacity, LengthMm, WidthMm, HeightMm, Cca, Technology);

    private static bool UnknownKeysEqual(
        IReadOnlyDictionary<string, object?> left,
        IReadOnlyDictionary<string, object?> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other)
                || !Equals(Convert.ToString(value), Convert.ToString(other)))
            {
                return false;
            }
        }

        return true;
    }

    private static int? ToInt(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case int i:
                return i < 0 ? null : i;
            case long l when l is >= 0 and <= int.MaxValue:
                return (int)l;
            case string s when int.TryParse(s.Trim(), out var parsed):
                return parsed < 0 ? null : parsed;
            default:
                if (value is IConvertible convertible)
                {
                    try
                    {
                        var n = convertible.ToInt32(null);
                        return n < 0 ? null : n;
                    }
                    catch
                    {
                        return null;
                    }
                }

                return null;
        }
    }

    private static string? KnownToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return TechnologyTokens.FirstOrDefault(t => t.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static object? NormalizeUnknown(object? value) =>
        value switch
        {
            null => null,
            string s => s,
            int or long or bool => value,
            _ => Convert.ToString(value)
        };
}
