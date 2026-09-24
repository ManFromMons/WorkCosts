using System.Diagnostics;
using System.Text;

namespace AgentBoard;

public static class GitProcess
{
    public static string Run(string repoRoot, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("git failed to start");
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return stdout;
    }

    public static bool IsDirty(string repoRoot)
    {
        var text = Run(repoRoot, "status", "--porcelain");
        return !string.IsNullOrWhiteSpace(text);
    }

    public static string CurrentBranch(string repoRoot) =>
        Run(repoRoot, "branch", "--show-current").Trim();
}

public static class FeatureCatalog
{
    public static Dictionary<string, FeatureStory> Load(string repoRoot, string? inboxMarkdown, bool fetch = false)
    {
        if (fetch)
        {
            try
            {
                GitProcess.Run(repoRoot, "fetch", "origin");
            }
            catch
            {
                // refresh is best-effort
            }
        }

        var stories = new Dictionary<string, FeatureStory>(StringComparer.OrdinalIgnoreCase);
        MergeTree(stories, repoRoot, "origin/main", fromMain: true);
        MergeDisk(stories, repoRoot);
        var inbox = InboxParser.Parse(inboxMarkdown ?? TryShow(repoRoot, "origin/main:docs/features/to-review.md"));
        foreach (var heading in inbox.Headings)
        {
            if (stories.TryGetValue(heading.Kebab, out var story))
            {
                stories[heading.Kebab] = story with { InboxStatus = heading.Status };
            }
        }

        return stories;
    }

    public static string StoryFileText(string repoRoot, FeatureStory story)
    {
        var path = Path.Combine(repoRoot, "docs", "features", Path.GetFileName(story.RelativePath));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static void MergeTree(Dictionary<string, FeatureStory> stories, string repoRoot, string treeRef, bool fromMain)
    {
        string list;
        try
        {
            list = GitProcess.Run(repoRoot, "ls-tree", "--name-only", treeRef, "docs/features/");
        }
        catch
        {
            return;
        }

        foreach (var path in list.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = Path.GetFileName(path);
            if (name is "to-review.md" || name.EndsWith("-delivery.md", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var md = TryShow(repoRoot, $"{treeRef}:{path}");
            if (string.IsNullOrWhiteSpace(md))
            {
                continue;
            }

            var parsed = StoryMarkdown.Parse(md, name, onMain: fromMain, onDisk: false);
            Upsert(stories, parsed, fromMain, fromDisk: false);
        }
    }

    private static void MergeDisk(Dictionary<string, FeatureStory> stories, string repoRoot)
    {
        var dir = Path.Combine(repoRoot, "docs", "features");
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            var name = Path.GetFileName(file);
            if (name is "to-review.md" || name.EndsWith("-delivery.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var md = File.ReadAllText(file);
            var parsed = StoryMarkdown.Parse(md, name, onMain: false, onDisk: true);
            Upsert(stories, parsed, fromMain: false, fromDisk: true);
        }
    }

    private static void Upsert(Dictionary<string, FeatureStory> stories, FeatureStory parsed, bool fromMain, bool fromDisk)
    {
        if (stories.TryGetValue(parsed.Kebab, out var existing))
        {
            if (fromDisk)
            {
                stories[parsed.Kebab] = parsed with
                {
                    OnMain = existing.OnMain,
                    MainStatus = existing.MainStatus,
                    InboxStatus = existing.InboxStatus,
                    OnDisk = true
                };
            }
            else if (fromMain)
            {
                stories[parsed.Kebab] = existing with { OnMain = true, MainStatus = parsed.Status };
            }

            return;
        }

        stories[parsed.Kebab] = parsed with
        {
            OnMain = fromMain,
            OnDisk = fromDisk,
            MainStatus = fromMain ? parsed.Status : null
        };
    }

    private static string TryShow(string repoRoot, string spec)
    {
        try
        {
            return GitProcess.Run(repoRoot, "show", spec);
        }
        catch
        {
            return "";
        }
    }

    public static string LoadInbox(string repoRoot) =>
        TryShow(repoRoot, "origin/main:docs/features/to-review.md");
}

public sealed class AgentSendQueue
{
    public int InFlight { get; private set; }
    public string LastLog { get; private set; } = "";
    public string Status { get; private set; } = "idle";

    public IReadOnlyList<string> BuildSend(string repoRoot, string prompt, SessionStore store, string? kebab, bool planning)
    {
        var id = store.IdFor(kebab, planning);
        return AgentCli.Send(repoRoot, prompt, id);
    }

    public void Begin()
    {
        InFlight++;
        Status = "running";
    }

    public void Complete(bool ok, string log)
    {
        InFlight = Math.Max(0, InFlight - 1);
        LastLog = log;
        Status = ok ? "idle" : "failed";
    }
}
