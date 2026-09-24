namespace AgentBoard;

public static class QueueMembership
{
    public static IReadOnlyList<QueueRow> Build(
        IReadOnlyDictionary<string, FeatureStory> stories,
        bool showDone)
    {
        var visible = stories.Values
            .Where(s => !StoryMarkdown.IsHiddenOpsNoise(s.Kebab))
            .Where(s => Include(s, showDone))
            .ToList();

        var byKebab = visible.ToDictionary(s => s.Kebab, StringComparer.OrdinalIgnoreCase);
        var children = new Dictionary<string, List<FeatureStory>>(StringComparer.OrdinalIgnoreCase);
        foreach (var story in visible)
        {
            var parent = PrimaryParent(story, byKebab);
            if (parent is null)
            {
                continue;
            }

            if (!children.TryGetValue(parent, out var list))
            {
                list = [];
                children[parent] = list;
            }

            list.Add(story);
        }

        var roots = visible
            .Where(s => PrimaryParent(s, byKebab) is null)
            .OrderBy(s => s.Seq)
            .ThenBy(s => s.Kebab, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<QueueRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            Walk(root, 0);
        }

        return rows;

        void Walk(FeatureStory story, int depth)
        {
            if (!seen.Add(story.Kebab))
            {
                return;
            }

            rows.Add(ToRow(story, depth));
            if (!children.TryGetValue(story.Kebab, out var kids))
            {
                return;
            }

            foreach (var kid in kids.OrderBy(s => s.Seq).ThenBy(s => s.Kebab, StringComparer.OrdinalIgnoreCase))
            {
                Walk(kid, depth + 1);
            }
        }
    }

    public static bool Include(FeatureStory story, bool showDone)
    {
        if (StoryMarkdown.IsHiddenOpsNoise(story.Kebab))
        {
            return false;
        }

        var done = story.Status == "done" && CloseOutFinished(story);
        if (done)
        {
            return showDone;
        }

        if (story.Status is "ready-for-agent" or "ready-for-review" or "draft")
        {
            return true;
        }

        if (story.InboxStatus is "ready-to-resume" or "ready-to-complete" or "ready-for-review" or "blocked")
        {
            return true;
        }

        return false;
    }

    public static bool CloseOutFinished(FeatureStory story) =>
        story.InboxStatus is null or "done";

    public static string IndicatorSuffix(FeatureStory story)
    {
        var bits = new List<string>();
        if (story.InboxStatus == "ready-to-resume")
        {
            bits.Add("ready-to-resume");
        }

        if (story.InboxStatus == "ready-to-complete")
        {
            bits.Add("ready-to-complete");
        }

        return bits.Count == 0 ? "" : " [" + string.Join("; ", bits) + "]";
    }

    private static QueueRow ToRow(FeatureStory story, int depth) =>
        new(story.Kebab, story.Seq, story.Title, story.Status, story.InboxStatus, IndicatorSuffix(story), depth);

    private static string? PrimaryParent(FeatureStory story, IReadOnlyDictionary<string, FeatureStory> stories)
    {
        var found = story.DependsOn
            .Where(stories.ContainsKey)
            .Select(k => stories[k])
            .OrderBy(s => s.Seq)
            .ThenBy(s => s.Kebab, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return found.Count == 0 ? null : found[0].Kebab;
    }
}
