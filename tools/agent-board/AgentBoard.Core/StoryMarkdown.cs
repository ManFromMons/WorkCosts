using System.Text.RegularExpressions;

namespace AgentBoard;

public static class StoryMarkdown
{
    public static readonly HashSet<string> HiddenKebabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "job-concept"
    };

    public static bool IsHiddenOpsNoise(string kebab) =>
        HiddenKebabs.Contains(kebab) || kebab.StartsWith("gnome-", StringComparison.OrdinalIgnoreCase);

    public static FeatureStory Parse(string markdown, string fileName, bool onMain, bool onDisk)
    {
        var kebab = KebabFromId(Header(markdown, "Id")) ?? Path.GetFileNameWithoutExtension(fileName);
        var seqRaw = Header(markdown, "Seq");
        var seq = int.TryParse(seqRaw, out var s) ? s : int.MaxValue;
        var status = FirstToken(Header(markdown, "Status")) ?? "unknown";
        var depends = DependsOn(Header(markdown, "Depends-on"));
        var title = Title(markdown) ?? kebab;
        return new FeatureStory(kebab, seq, status, onMain ? status : null, onMain, onDisk, depends, title, null, $"docs/features/{fileName}");
    }

    public static string? Header(string markdown, string label)
    {
        var match = Regex.Match(markdown, @"^-\s+\*\*" + Regex.Escape(label) + @":\*\*\s*(.+?)\s*$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static string? KebabFromId(string? idLine)
    {
        if (string.IsNullOrWhiteSpace(idLine))
        {
            return null;
        }

        var backticked = Regex.Match(idLine, @"`docs/features/([^`]+)\.md`");
        if (backticked.Success)
        {
            return backticked.Groups[1].Value;
        }

        var plain = Regex.Match(idLine, @"docs/features/([^\s]+)\.md");
        return plain.Success ? plain.Groups[1].Value : null;
    }

    public static IReadOnlyList<string> DependsOn(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "none" || raw == "\u2014")
        {
            return [];
        }

        var ids = new List<string>();
        foreach (var part in raw.Split(','))
        {
            var token = part.Trim().Trim('`');
            if (token.StartsWith("docs/features/", StringComparison.Ordinal))
            {
                var m = Regex.Match(token, @"docs/features/([^.\s]+)");
                if (m.Success)
                {
                    ids.Add(m.Groups[1].Value);
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(token) && token != "none")
            {
                ids.Add(token);
            }
        }

        return ids;
    }

    public static string? Title(string markdown)
    {
        var h1 = Regex.Match(markdown, @"^#\s+Feature:\s*(.+?)\s*$", RegexOptions.Multiline);
        if (h1.Success)
        {
            return h1.Groups[1].Value.Trim();
        }

        var any = Regex.Match(markdown, @"^#\s+(.+?)\s*$", RegexOptions.Multiline);
        return any.Success ? any.Groups[1].Value.Trim() : null;
    }

    public static string FirstToken(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "unknown";
        }

        return status.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    public static bool DepsDoneOnMain(FeatureStory story, IReadOnlyDictionary<string, FeatureStory> stories)
    {
        foreach (var dep in story.DependsOn)
        {
            if (!stories.TryGetValue(dep, out var other) || !other.OnMain || other.MainStatus != "done")
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<string> WaitingOnMain(FeatureStory story, IReadOnlyDictionary<string, FeatureStory> stories)
    {
        var waiting = new List<string>();
        foreach (var dep in story.DependsOn)
        {
            if (!stories.TryGetValue(dep, out var other))
            {
                waiting.Add($"{dep} (missing)");
                continue;
            }

            if (!other.OnMain)
            {
                waiting.Add($"{dep} (not on origin/main)");
                continue;
            }

            if (other.MainStatus != "done")
            {
                var seq = other.Seq == int.MaxValue ? "?" : other.Seq.ToString();
                waiting.Add($"{seq} {dep} ({other.MainStatus})");
            }
        }

        return waiting;
    }
}
