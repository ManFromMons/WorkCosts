namespace AgentBoard;

public enum FocusedPane
{
    Queue,
    Working,
    Story,
    Chat
}

public enum DeviationDecision
{
    Unset,
    Accept,
    Reject
}

public sealed record FeatureStory(
    string Kebab,
    int Seq,
    string Status,
    string? MainStatus,
    bool OnMain,
    bool OnDisk,
    IReadOnlyList<string> DependsOn,
    string Title,
    string? InboxStatus,
    string RelativePath);

public sealed record QueueRow(
    string Kebab,
    int Seq,
    string Title,
    string Status,
    string? InboxStatus,
    string IndicatorSuffix,
    int Depth);

public sealed record WorkingRow(
    string Kebab,
    int Seq,
    string Title,
    string Kind,
    int SortOrder);

public sealed record InboxQuestion(int Index, string Text, string Answer, bool Checked);

public sealed record InboxDeviation(int Index, string Text, DeviationDecision Decision, string Reason);

public sealed record InboxHeading(
    string Kebab,
    string Block,
    string Status,
    IReadOnlyList<InboxQuestion> Questions,
    IReadOnlyList<InboxDeviation> Deviations,
    bool VerifyTicked);

public sealed record ParsedInbox(string Prefix, IReadOnlyList<InboxHeading> Headings);

public sealed record NextAction(string Label, string KeyHint, string? Kebab, string Kind);

public sealed record SessionMap(string? PlanningId, IReadOnlyDictionary<string, string> ImplementByKebab);

public sealed record AnsiSpan(string Text, bool Bold, bool Underline, bool Reverse, int? Fg, int? Bg);
