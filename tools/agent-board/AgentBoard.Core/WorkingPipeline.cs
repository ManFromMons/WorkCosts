namespace AgentBoard;

public static class WorkingPipeline
{
    public const string KindBlocked = "blocked";
    public const string KindReview = "review";
    public const string KindResume = "resume";
    public const string KindAgent = "agent-working";
    public const string KindDraft = "draft";
    public const string KindComplete = "complete";

    public static bool IsAgentWorking(bool liveProcess, string? inboxStatus) =>
        liveProcess || inboxStatus == "in-progress";

    public static IReadOnlyList<WorkingRow> Build(
        IReadOnlyDictionary<string, FeatureStory> stories,
        Func<string, bool> liveProcessForKebab)
    {
        var rows = new List<WorkingRow>();
        foreach (var story in stories.Values)
        {
            if (StoryMarkdown.IsHiddenOpsNoise(story.Kebab))
            {
                continue;
            }

            if (story.Status == "done" && QueueMembership.CloseOutFinished(story))
            {
                continue;
            }

            if (story.Status == "ready-for-agent" && string.IsNullOrEmpty(story.InboxStatus))
            {
                continue;
            }

            var kind = KindOf(story, liveProcessForKebab(story.Kebab));
            if (kind is null)
            {
                continue;
            }

            rows.Add(new WorkingRow(story.Kebab, story.Seq, story.Title, kind, SortOrder(kind)));
        }

        return rows
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Seq)
            .ThenBy(r => r.Kebab, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? KindOf(FeatureStory story, bool liveProcess)
    {
        if (story.InboxStatus == "blocked")
        {
            return KindBlocked;
        }

        if (story.InboxStatus == "ready-for-review" || story.Status == "ready-for-review")
        {
            return KindReview;
        }

        if (story.InboxStatus == "ready-to-resume")
        {
            return KindResume;
        }

        if (IsAgentWorking(liveProcess, story.InboxStatus))
        {
            return KindAgent;
        }

        if (story.Status == "draft")
        {
            return KindDraft;
        }

        if (story.InboxStatus == "ready-to-complete")
        {
            return KindComplete;
        }

        return null;
    }

    public static int SortOrder(string kind) => kind switch
    {
        KindBlocked => 0,
        KindReview => 1,
        KindResume => 2,
        KindAgent => 3,
        KindDraft => 4,
        KindComplete => 5,
        _ => 9
    };
}
