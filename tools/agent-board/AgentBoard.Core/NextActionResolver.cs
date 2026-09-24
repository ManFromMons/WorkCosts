namespace AgentBoard;

public static class NextActionResolver
{
    public static NextAction ForSelection(
        FeatureStory? story,
        bool gitDirty,
        bool liveProcess,
        IReadOnlyDictionary<string, FeatureStory> stories)
    {
        if (story is null)
        {
            return new NextAction("No story selected.", "", null, "empty");
        }

        var inbox = story.InboxStatus;
        if (inbox == "blocked")
        {
            return new NextAction("Open Inbox — unanswered questions", "r", story.Kebab, "inbox");
        }

        if (inbox == "ready-for-review" || story.Status == "ready-for-review")
        {
            return new NextAction("Open Inbox / review PR", "r", story.Kebab, "inbox");
        }

        if (inbox == "ready-to-resume")
        {
            return new NextAction("resume-implementation", "", story.Kebab, "resume");
        }

        if (WorkingPipeline.IsAgentWorking(liveProcess, inbox))
        {
            return new NextAction("Attach Chat", "", story.Kebab, "attach");
        }

        if (story.Status == "draft")
        {
            return new NextAction("plan-feature", "p", story.Kebab, "plan");
        }

        if (inbox == "ready-to-complete")
        {
            return new NextAction("complete-review", "", story.Kebab, "complete");
        }

        if (story.Status == "ready-for-agent" && !story.OnMain)
        {
            var dirty = gitDirty ? " (tree dirty — refuse)" : "";
            return new NextAction($"merge-planning{dirty}", "m", story.Kebab, "merge-planning");
        }

        if (story.Status == "ready-for-agent" && story.OnMain && story.MainStatus == "ready-for-agent")
        {
            if (StoryMarkdown.DepsDoneOnMain(story, stories))
            {
                return new NextAction("start-implement", "i", story.Kebab, "implement");
            }

            var waiting = string.Join("; ", StoryMarkdown.WaitingOnMain(story, stories));
            return new NextAction($"Waiting on {waiting}", "", story.Kebab, "waiting");
        }

        return new NextAction("No story selected.", "", story.Kebab, "empty");
    }

    public static NextAction FollowsFocusedPane(
        FocusedPane pane,
        FeatureStory? queueSelection,
        FeatureStory? workingSelection,
        bool gitDirty,
        bool liveProcessQueue,
        bool liveProcessWorking,
        IReadOnlyDictionary<string, FeatureStory> stories)
    {
        return pane switch
        {
            FocusedPane.Working => ForSelection(workingSelection, gitDirty, liveProcessWorking, stories),
            _ => ForSelection(queueSelection, gitDirty, liveProcessQueue, stories)
        };
    }

    public static FocusedPane DefaultFocus(bool workingHasRows) =>
        workingHasRows ? FocusedPane.Working : FocusedPane.Queue;
}
