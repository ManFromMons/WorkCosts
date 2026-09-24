namespace AgentBoard;

public static class InboxAutoStatus
{
    public static string Compute(InboxHeading heading)
    {
        var questionsOpen = heading.Questions.Any(q => !q.Checked || string.IsNullOrWhiteSpace(q.Answer));
        var incompleteReject = heading.Deviations.Any(d =>
            d.Decision == DeviationDecision.Reject && string.IsNullOrWhiteSpace(d.Reason));
        if (questionsOpen || incompleteReject)
        {
            return "blocked";
        }

        var rejects = heading.Deviations.Where(d => d.Decision == DeviationDecision.Reject && !string.IsNullOrWhiteSpace(d.Reason)).ToList();
        if (rejects.Count > 0)
        {
            return "ready-to-resume";
        }

        var allAcceptOrNone = heading.Deviations.Count == 0
                              || heading.Deviations.All(d => d.Decision == DeviationDecision.Accept);
        if (heading.VerifyTicked && allAcceptOrNone)
        {
            return "ready-to-complete";
        }

        return "ready-to-resume";
    }
}
