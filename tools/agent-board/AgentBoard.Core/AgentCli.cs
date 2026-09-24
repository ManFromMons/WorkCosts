namespace AgentBoard;

public static class AgentCli
{
    public const string HandbookLogin = "docs/agent-handbook.md (Install and login)";

    public static IReadOnlyList<string> ListSessions() => ["ls"];

    public static IReadOnlyList<string> Send(string repoRoot, string prompt, string? sessionId)
    {
        var args = new List<string> { "-p", "--force" };
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            args.Add("--resume");
            args.Add(sessionId);
        }

        args.Add("--workspace");
        args.Add(repoRoot);
        args.Add("--output-format");
        args.Add("text");
        args.Add(prompt);
        return args;
    }

    public static bool UsesPrintForceResumeWorkspace(IReadOnlyList<string> args, string repoRoot, string sessionId)
    {
        return args.SequenceEqual(Send(repoRoot, args[^1], sessionId));
    }
}

public sealed class SessionStore
{
    public string? PlanningId { get; set; }
    public Dictionary<string, string> ImplementByKebab { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? IdFor(string? kebab, bool planning) =>
        planning ? PlanningId : kebab is null ? null : ImplementByKebab.GetValueOrDefault(kebab);

    public void RememberImplement(string kebab, string id)
    {
        if (ImplementByKebab.ContainsKey(kebab))
        {
            return;
        }

        ImplementByKebab[kebab] = id;
    }

    public void RememberPlanning(string id) => PlanningId ??= id;

    public static SessionStore ParseJson(string json)
    {
        var store = new SessionStore();
        var planning = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (planning.RootElement.TryGetProperty("planningId", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            store.PlanningId = p.GetString();
        }

        if (planning.RootElement.TryGetProperty("implement", out var impl) && impl.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var prop in impl.EnumerateObject())
            {
                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    store.ImplementByKebab[prop.Name] = prop.Value.GetString() ?? "";
                }
            }
        }

        return store;
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("planningId", PlanningId);
            writer.WriteStartObject("implement");
            foreach (var pair in ImplementByKebab)
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

public static class AttachPolicy
{
    public static bool WouldStartSecondImplement(SessionStore store, string kebab) =>
        store.ImplementByKebab.ContainsKey(kebab);

    public static string? AttachImplement(SessionStore store, string kebab) =>
        store.IdFor(kebab, planning: false);

    public static string? AttachPlanning(SessionStore store) => store.PlanningId;

    public static bool UsesSharedPlanning(SessionStore store, string firstSendId, string secondSendId) =>
        store.PlanningId == firstSendId && store.PlanningId == secondSendId;
}

public static class QuitPolicy
{
    public static bool WaitForIdle => false;

    public static bool NeedsConfirm(int inFlightSends) => inFlightSends > 0;

    public static string ConfirmMessage(int n) => $"Stop {n} send(s) vs Cancel quit";
}
