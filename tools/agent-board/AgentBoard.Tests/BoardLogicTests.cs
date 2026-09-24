using AgentBoard;
using Xunit;

namespace AgentBoard.Tests;

public class BoardLogicTests
{
    private static FeatureStory Story(
        string kebab,
        int seq,
        string status,
        string? inbox = null,
        bool onMain = true,
        bool onDisk = true,
        IReadOnlyList<string>? deps = null,
        string? mainStatus = null) =>
        new(kebab, seq, status, mainStatus ?? (onMain ? status : null), onMain, onDisk, deps ?? [], kebab, inbox, $"docs/features/{kebab}.md");

    [Fact]
    public void QueueMembership_ReadyForAgentAndReview_HidesDoneUnlessToggled()
    {
        var stories = new Dictionary<string, FeatureStory>
        {
            ["a"] = Story("a", 1, "ready-for-agent"),
            ["b"] = Story("b", 2, "ready-for-review"),
            ["c"] = Story("c", 3, "done", inbox: "done")
        };
        var hidden = QueueMembership.Build(stories, showDone: false);
        Assert.DoesNotContain(hidden, r => r.Kebab == "c");
        Assert.Contains(hidden, r => r.Kebab == "a");
        Assert.Contains(hidden, r => r.Kebab == "b");
        var shown = QueueMembership.Build(stories, showDone: true);
        Assert.Contains(shown, r => r.Kebab == "c");
    }

    [Fact]
    public void QueueHides_GnomeSlices_AndJobConcept()
    {
        var stories = new Dictionary<string, FeatureStory>
        {
            ["gnome-scaffold"] = Story("gnome-scaffold", 1, "ready-for-agent"),
            ["job-concept"] = Story("job-concept", int.MaxValue, "draft"),
            ["paste-html"] = Story("paste-html", 1, "ready-for-agent")
        };
        var rows = QueueMembership.Build(stories, showDone: true);
        Assert.DoesNotContain(rows, r => r.Kebab == "gnome-scaffold");
        Assert.DoesNotContain(rows, r => r.Kebab == "job-concept");
        Assert.Contains(rows, r => r.Kebab == "paste-html");
        Assert.DoesNotContain(WorkingPipeline.Build(stories, _ => false), r => r.Kebab is "gnome-scaffold" or "job-concept");
    }

    [Fact]
    public void QueueIncludes_UncommittedDraftFeatureFiles()
    {
        var stories = new Dictionary<string, FeatureStory>
        {
            ["new-thing"] = Story("new-thing", 99, "draft", onMain: false, onDisk: true)
        };
        var rows = QueueMembership.Build(stories, showDone: false);
        Assert.Contains(rows, r => r.Kebab == "new-thing" && r.Status == "draft");
        var working = WorkingPipeline.Build(stories, _ => false);
        Assert.Contains(working, r => r.Kebab == "new-thing" && r.Kind == WorkingPipeline.KindDraft);
    }

    [Fact]
    public void WorkingSort_BlockedReviewResumeAgentDraftComplete()
    {
        var stories = new Dictionary<string, FeatureStory>
        {
            ["complete"] = Story("complete", 6, "ready-for-agent", "ready-to-complete"),
            ["draft"] = Story("draft", 5, "draft"),
            ["agent"] = Story("agent", 4, "ready-for-agent", "in-progress"),
            ["resume"] = Story("resume", 3, "ready-for-agent", "ready-to-resume"),
            ["review"] = Story("review", 2, "ready-for-review"),
            ["blocked"] = Story("blocked", 1, "ready-for-agent", "blocked"),
        };
        var rows = WorkingPipeline.Build(stories, _ => false);
        Assert.Equal(
            new[] { "blocked", "review", "resume", "agent-working", "draft", "complete" },
            rows.Select(r => r.Kind).ToArray());
    }

    [Fact]
    public void AgentWorking_TrueIfLiveProcessOrInboxInProgress()
    {
        Assert.True(WorkingPipeline.IsAgentWorking(true, null));
        Assert.True(WorkingPipeline.IsAgentWorking(false, "in-progress"));
        Assert.False(WorkingPipeline.IsAgentWorking(false, "ready-for-review"));
    }

    [Fact]
    public void NextFollowsFocusedPaneSelection()
    {
        var stories = new Dictionary<string, FeatureStory>
        {
            ["q"] = Story("q", 1, "draft"),
            ["w"] = Story("w", 2, "ready-for-agent", "blocked")
        };
        var fromQueue = NextActionResolver.FollowsFocusedPane(
            FocusedPane.Queue, stories["q"], stories["w"], false, false, false, stories);
        var fromWorking = NextActionResolver.FollowsFocusedPane(
            FocusedPane.Working, stories["q"], stories["w"], false, false, false, stories);
        Assert.Equal("plan", fromQueue.Kind);
        Assert.Equal("inbox", fromWorking.Kind);
        Assert.Equal(FocusedPane.Working, NextActionResolver.DefaultFocus(true));
        Assert.Equal(FocusedPane.Queue, NextActionResolver.DefaultFocus(false));
    }

    private const string InboxSample = """
# Feature implementation review

## Entries

## 11-cars

- **Feature:** [docs/features/11-cars.md](11-cars.md)
- **Status:** ready-for-review
- **Change set:** branch `feature/11-cars-Cars` — [https://github.com/ManFromMons/WorkCosts/pull/13](https://github.com/ManFromMons/WorkCosts/pull/13)

### Questions

- [ ] Q1. Need a name?

### Deviations to scan

- [ ] Used an existing helper.

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted

## in-flight

- **Status:** in-progress

### Questions

_(none)_
""";

    [Fact]
    public void InboxParse_SeqPrefixedHeading_11Cars()
    {
        var parsed = InboxParser.Parse(InboxSample);
        Assert.Contains(parsed.Headings, h => h.Kebab == "11-cars");
        Assert.Equal("ready-for-review", parsed.Headings.First(h => h.Kebab == "11-cars").Status);
        Assert.DoesNotContain(InboxParser.OverlayList(parsed), h => h.Kebab == "in-flight");
    }

    [Fact]
    public void InboxAutoStatus_OpenQuestion_Blocked()
    {
        var heading = InboxParser.Find(InboxParser.Parse(InboxSample), "11-cars")!;
        Assert.Equal("blocked", InboxAutoStatus.Compute(heading));
    }

    [Fact]
    public void InboxAutoStatus_RejectEmptyReason_Blocked()
    {
        var md = InboxParser.SetQuestionAnswer(InboxSample, "11-cars", 0, "yes", true);
        md = InboxParser.SetDeviation(md, "11-cars", 0, DeviationDecision.Reject, "");
        var heading = InboxParser.Find(InboxParser.Parse(md), "11-cars")!;
        Assert.Equal("blocked", InboxAutoStatus.Compute(heading));
    }

    [Fact]
    public void InboxAutoStatus_RejectWithReason_ReadyToResume()
    {
        var md = InboxParser.SetQuestionAnswer(InboxSample, "11-cars", 0, "yes", true);
        md = InboxParser.SetDeviation(md, "11-cars", 0, DeviationDecision.Reject, "follow spec");
        md = InboxParser.SetVerify(md, "11-cars", true);
        var heading = InboxParser.Find(InboxParser.Parse(md), "11-cars")!;
        Assert.Equal("ready-to-resume", InboxAutoStatus.Compute(heading));
    }

    [Fact]
    public void InboxAutoStatus_AnswersWithoutVerify_ReadyToResume()
    {
        var md = InboxParser.SetQuestionAnswer(InboxSample, "11-cars", 0, "yes", true);
        md = InboxParser.SetDeviation(md, "11-cars", 0, DeviationDecision.Accept, "");
        var heading = InboxParser.Find(InboxParser.Parse(md), "11-cars")!;
        Assert.False(heading.VerifyTicked);
        Assert.Equal("ready-to-resume", InboxAutoStatus.Compute(heading));
    }

    [Fact]
    public void InboxAutoStatus_VerifyAndAllAccept_ReadyToComplete()
    {
        var md = InboxParser.SetQuestionAnswer(InboxSample, "11-cars", 0, "yes", true);
        md = InboxParser.SetDeviation(md, "11-cars", 0, DeviationDecision.Accept, "");
        md = InboxParser.SetVerify(md, "11-cars", true);
        var heading = InboxParser.Find(InboxParser.Parse(md), "11-cars")!;
        Assert.Equal("ready-to-complete", InboxAutoStatus.Compute(heading));
    }

    [Fact]
    public void InboxClose_Clean_NoGit()
    {
        var closer = new InboxCloser();
        var result = closer.Close(dirty: false, _ => throw new Exception("must not land"));
        Assert.Equal("dismiss", result);
        Assert.False(closer.LandCalled);
    }

    [Fact]
    public void InboxClose_Dirty_LandsOnlyToReviewOnMain()
    {
        var closer = new InboxCloser();
        string? script = null;
        var result = closer.Close(dirty: true, s => script = s);
        Assert.Equal("land", result);
        Assert.True(closer.LandCalled);
        Assert.Equal("update-to-review.sh", script);
    }

    [Fact]
    public void PrUrl_UsesInboxTextWhenHttpsPresent()
    {
        var heading = InboxParser.Find(InboxParser.Parse(InboxSample), "11-cars")!;
        Assert.Equal("https://github.com/ManFromMons/WorkCosts/pull/13", InboxParser.PrUrlFromInboxText(heading.Block));
        Assert.Null(InboxParser.PrUrlFromInboxText("- **Change set:** branch `feature/x`"));
    }

    [Fact]
    public void Attach_DoesNotStartSecondImplementAgentForKebab()
    {
        var store = new SessionStore();
        store.RememberImplement("paste-html", "sess-1");
        Assert.True(AttachPolicy.WouldStartSecondImplement(store, "paste-html"));
        Assert.Equal("sess-1", AttachPolicy.AttachImplement(store, "paste-html"));
        store.RememberImplement("paste-html", "sess-2");
        Assert.Equal("sess-1", store.ImplementByKebab["paste-html"]);
    }

    [Fact]
    public void PlanFeature_UsesSharedPlanningAgent()
    {
        var store = new SessionStore();
        store.RememberPlanning("plan-1");
        Assert.Equal("plan-1", AttachPolicy.AttachPlanning(store));
        store.RememberPlanning("plan-2");
        Assert.True(AttachPolicy.UsesSharedPlanning(store, "plan-1", "plan-1"));
        Assert.Equal("plan-1", store.PlanningId);
    }

    [Fact]
    public void Quit_Detaches_DoesNotWaitForIdle()
    {
        Assert.False(QuitPolicy.WaitForIdle);
        Assert.False(QuitPolicy.NeedsConfirm(0));
        Assert.True(QuitPolicy.NeedsConfirm(1));
        Assert.Contains("Stop 2", QuitPolicy.ConfirmMessage(2));
    }

    [Fact]
    public void ScriptHost_GitCatFile_DoesNotUseCmdExe()
    {
        var host = new ScriptHost("/tmp/repo");
        var start = host.StartGitCatFile("HEAD:docs/features/to-review.md");
        var argv = new List<string> { start.FileName };
        argv.AddRange(start.ArgumentList);
        Assert.True(ScriptHost.GitCatFileArgvIsSafe(argv));
        Assert.False(host.CommandLineUsesCmdExe(string.Join(' ', argv)));
    }

    [Fact]
    public void Cli_Send_UsesPrintForceResumeWorkspace()
    {
        var args = AgentCli.Send("/repo", "/start-implement paste-html", "abc");
        Assert.Equal(new[] { "-p", "--force", "--resume", "abc", "--workspace", "/repo", "--output-format", "text", "/start-implement paste-html" }, args.ToArray());
        Assert.True(AgentCli.UsesPrintForceResumeWorkspace(args, "/repo", "abc"));
        var create = AgentCli.Send("/repo", "/plan-feature cars", null);
        Assert.DoesNotContain("--resume", create);
        Assert.Contains("-p", create);
        Assert.Contains("--force", create);
    }

    [Fact]
    public void AnsiDecoder_KeepsColours()
    {
        var spans = AnsiDecoder.Decode("\u001b[31;1mred\u001b[0mplain");
        Assert.Equal(2, spans.Count);
        Assert.Equal("red", spans[0].Text);
        Assert.True(spans[0].Bold);
        Assert.Equal(1, spans[0].Fg);
        Assert.Equal("plain", spans[1].Text);
    }
}
