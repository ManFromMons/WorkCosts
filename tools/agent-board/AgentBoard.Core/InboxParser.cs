using System.Text;
using System.Text.RegularExpressions;

namespace AgentBoard;

public static class InboxParser
{
    public static readonly Regex HeadingRe = new(@"^##\s+((?:\d+-)?[a-z][a-z0-9-]*)\s*$", RegexOptions.Compiled);
    private static readonly Regex StatusRe = new(@"^-\s+\*\*Status:\*\*\s*(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex CheckboxRe = new(@"^-\s+\[([ xX])\]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex HttpsRe = new(@"https://[^\s\)\]]+", RegexOptions.Compiled);
    private static readonly HashSet<string> SkipHeadings = new(StringComparer.OrdinalIgnoreCase) { "entries" };

    public static ParsedInbox Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var headings = new List<InboxHeading>();
        var prefix = new List<string>();
        string? currentKebab = null;
        var current = new List<string>();

        void Flush()
        {
            if (currentKebab is null)
            {
                return;
            }

            headings.Add(ParseHeading(currentKebab, string.Join('\n', current).TrimEnd()));
            currentKebab = null;
            current.Clear();
        }

        foreach (var line in lines)
        {
            var heading = HeadingRe.Match(line.Trim());
            if (heading.Success && !SkipHeadings.Contains(heading.Groups[1].Value))
            {
                Flush();
                currentKebab = heading.Groups[1].Value;
                current.Add(line);
                continue;
            }

            if (currentKebab is not null)
            {
                current.Add(line);
            }
            else
            {
                prefix.Add(line);
            }
        }

        Flush();
        return new ParsedInbox(string.Join('\n', prefix).TrimEnd(), headings);
    }

    public static InboxHeading ParseHeading(string kebab, string block)
    {
        var statusMatch = StatusRe.Match(block);
        var status = statusMatch.Success ? FirstToken(statusMatch.Groups[1].Value) : "";
        var questions = ParseQuestions(Section(block, "Questions"));
        var deviations = ParseDeviations(Section(block, "Deviations to scan") ?? Section(block, "Deviations"));
        var verify = ParseVerify(Section(block, "Verify"));
        return new InboxHeading(kebab, block.TrimEnd(), status, questions, deviations, verify);
    }

    public static string Serialize(ParsedInbox parsed)
    {
        var parts = new List<string> { parsed.Prefix.TrimEnd() };
        foreach (var heading in parsed.Headings)
        {
            parts.Add(heading.Block.TrimEnd());
        }

        return string.Join("\n\n", parts).TrimEnd() + "\n";
    }

    public static InboxHeading? Find(ParsedInbox parsed, string kebab) =>
        parsed.Headings.FirstOrDefault(h => string.Equals(h.Kebab, kebab, StringComparison.OrdinalIgnoreCase));

    public static string SetStatus(string markdown, string kebab, string status)
    {
        var parsed = Parse(markdown);
        var heading = Find(parsed, kebab) ?? throw new InvalidOperationException($"No inbox heading {kebab}.");
        if (!StatusRe.IsMatch(heading.Block))
        {
            throw new InvalidOperationException($"Heading {kebab} has no Status line.");
        }

        var nextBlock = StatusRe.Replace(heading.Block, $"- **Status:** {status}", 1);
        return ReplaceHeading(parsed, kebab, ParseHeading(kebab, nextBlock));
    }

    public static string ApplyAutoStatus(string markdown, string kebab)
    {
        var parsed = Parse(markdown);
        var heading = Find(parsed, kebab) ?? throw new InvalidOperationException($"No inbox heading {kebab}.");
        var next = InboxAutoStatus.Compute(heading);
        return SetStatus(markdown, kebab, next);
    }

    public static string SetQuestionAnswer(string markdown, string kebab, int questionIndex, string answer, bool answered)
    {
        var parsed = Parse(markdown);
        var heading = Find(parsed, kebab) ?? throw new InvalidOperationException($"No inbox heading {kebab}.");
        var blockLines = heading.Block.Replace("\r\n", "\n").Split('\n').ToList();
        var qIndexes = QuestionLineIndexes(blockLines);
        if (questionIndex < 0 || questionIndex >= qIndexes.Count)
        {
            throw new InvalidOperationException($"No question {questionIndex} on {kebab}.");
        }

        var i = qIndexes[questionIndex];
        var match = CheckboxRe.Match(blockLines[i]);
        var rest = StripAnswer(match.Groups[2].Value);
        var mark = answered ? "x" : " ";
        var suffix = answered && !string.IsNullOrWhiteSpace(answer) ? $" **Answer:** {answer.Trim()}" : "";
        blockLines[i] = $"- [{mark}] {rest}{suffix}";
        return ReplaceHeading(parsed, kebab, ParseHeading(kebab, string.Join('\n', blockLines)));
    }

    public static string SetDeviation(string markdown, string kebab, int deviationIndex, DeviationDecision decision, string reason)
    {
        var parsed = Parse(markdown);
        var heading = Find(parsed, kebab) ?? throw new InvalidOperationException($"No inbox heading {kebab}.");
        var blockLines = heading.Block.Replace("\r\n", "\n").Split('\n').ToList();
        var dIndexes = DeviationLineIndexes(blockLines);
        if (deviationIndex < 0 || deviationIndex >= dIndexes.Count)
        {
            throw new InvalidOperationException($"No deviation {deviationIndex} on {kebab}.");
        }

        var i = dIndexes[deviationIndex];
        var match = CheckboxRe.Match(blockLines[i]);
        var original = StripDeviationMarkup(match.Groups[2].Value);
        blockLines[i] = decision switch
        {
            DeviationDecision.Accept => $"- [x] Accept: {original}",
            DeviationDecision.Reject => $"- [x] Reject: {original} **Reason:** {reason.Trim()}",
            _ => $"- [ ] {original}"
        };
        return ReplaceHeading(parsed, kebab, ParseHeading(kebab, string.Join('\n', blockLines)));
    }

    public static string SetVerify(string markdown, string kebab, bool ticked)
    {
        var parsed = Parse(markdown);
        var heading = Find(parsed, kebab) ?? throw new InvalidOperationException($"No inbox heading {kebab}.");
        var blockLines = heading.Block.Replace("\r\n", "\n").Split('\n').ToList();
        var section = CurrentSection(blockLines);
        for (var i = 0; i < blockLines.Count; i++)
        {
            if (section[i] != "Verify")
            {
                continue;
            }

            var match = CheckboxRe.Match(blockLines[i]);
            if (!match.Success)
            {
                continue;
            }

            var mark = ticked ? "x" : " ";
            blockLines[i] = $"- [{mark}] {match.Groups[2].Value}";
        }

        return ReplaceHeading(parsed, kebab, ParseHeading(kebab, string.Join('\n', blockLines)));
    }

    public static string? PrUrlFromInboxText(string headingBlock)
    {
        foreach (var line in headingBlock.Replace("\r\n", "\n").Split('\n'))
        {
            if (!line.Contains("Change set", StringComparison.OrdinalIgnoreCase)
                && !Regex.IsMatch(line, @"^\-\s+\*\*PR:\*\*", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var match = HttpsRe.Match(line);
            if (match.Success)
            {
                return match.Value.TrimEnd('.');
            }
        }

        return null;
    }

    public static IReadOnlyList<InboxHeading> OverlayList(ParsedInbox parsed)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blocked", "ready-for-review", "ready-to-resume", "ready-to-complete"
        };
        return parsed.Headings.Where(h => keep.Contains(h.Status)).ToList();
    }

    private static string ReplaceHeading(ParsedInbox parsed, string kebab, InboxHeading next)
    {
        var headings = parsed.Headings.Select(h =>
            string.Equals(h.Kebab, kebab, StringComparison.OrdinalIgnoreCase) ? next : h).ToList();
        return Serialize(parsed with { Headings = headings });
    }

    private static string FirstToken(string status)
    {
        var token = status.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return token.Equals("resume", StringComparison.OrdinalIgnoreCase) ? "ready-to-resume" : token;
    }

    private static string? Section(string block, string title)
    {
        var lines = block.Replace("\r\n", "\n").Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals($"### {title}", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        var buf = new StringBuilder();
        for (var i = start; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("### ", StringComparison.Ordinal))
            {
                break;
            }

            buf.AppendLine(lines[i]);
        }

        return buf.ToString();
    }

    private static IReadOnlyList<InboxQuestion> ParseQuestions(string? section)
    {
        var list = new List<InboxQuestion>();
        if (string.IsNullOrWhiteSpace(section) || IsNone(section))
        {
            return list;
        }

        foreach (var line in section.Replace("\r\n", "\n").Split('\n'))
        {
            var match = CheckboxRe.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var body = match.Groups[2].Value;
            var answer = ExtractAnswer(body);
            var checkedBox = match.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
            list.Add(new InboxQuestion(list.Count, StripAnswer(body), answer, checkedBox));
        }

        return list;
    }

    private static IReadOnlyList<InboxDeviation> ParseDeviations(string? section)
    {
        var list = new List<InboxDeviation>();
        if (string.IsNullOrWhiteSpace(section) || IsNone(section))
        {
            return list;
        }

        foreach (var line in section.Replace("\r\n", "\n").Split('\n'))
        {
            var match = CheckboxRe.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var body = match.Groups[2].Value;
            var checkedBox = match.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
            var decision = DeviationDecision.Unset;
            var reason = "";
            if (Regex.IsMatch(body, @"^\s*Reject\b", RegexOptions.IgnoreCase))
            {
                decision = DeviationDecision.Reject;
                reason = ExtractReason(body);
            }
            else if (checkedBox || Regex.IsMatch(body, @"^\s*Accept\b", RegexOptions.IgnoreCase))
            {
                decision = DeviationDecision.Accept;
            }

            list.Add(new InboxDeviation(list.Count, StripDeviationMarkup(body), decision, reason));
        }

        return list;
    }

    private static bool ParseVerify(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return false;
        }

        var boxes = new List<bool>();
        foreach (var line in section.Replace("\r\n", "\n").Split('\n'))
        {
            var match = CheckboxRe.Match(line);
            if (!match.Success)
            {
                continue;
            }

            boxes.Add(match.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase));
        }

        return boxes.Count > 0 && boxes.All(x => x);
    }

    private static bool IsNone(string section)
    {
        var trimmed = section.Trim();
        return trimmed.Equals("_(none)_", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("*(none)*", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractAnswer(string body)
    {
        var m = Regex.Match(body, @"\*\*Answer:\*\*\s*(.*)$", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static string StripAnswer(string body) =>
        Regex.Replace(body, @"\s*\*\*Answer:\*\*\s*.*$", "", RegexOptions.IgnoreCase).Trim();

    private static string ExtractReason(string body)
    {
        var m = Regex.Match(body, @"\*\*Reason:\*\*\s*(.*)$", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static string StripDeviationMarkup(string body)
    {
        var t = Regex.Replace(body, @"^\s*(Accept|Reject)\s*:\s*", "", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\s*\*\*Reason:\*\*\s*.*$", "", RegexOptions.IgnoreCase);
        return t.Trim();
    }

    private static List<int> QuestionLineIndexes(List<string> lines)
    {
        var indexes = new List<int>();
        var section = CurrentSection(lines);
        for (var i = 0; i < lines.Count; i++)
        {
            if (section[i] == "Questions" && CheckboxRe.IsMatch(lines[i]))
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    private static List<int> DeviationLineIndexes(List<string> lines)
    {
        var indexes = new List<int>();
        var section = CurrentSection(lines);
        for (var i = 0; i < lines.Count; i++)
        {
            if ((section[i] == "Deviations to scan" || section[i] == "Deviations") && CheckboxRe.IsMatch(lines[i]))
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    private static string[] CurrentSection(List<string> lines)
    {
        var names = new string[lines.Count];
        var current = "";
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("### ", StringComparison.Ordinal))
            {
                current = lines[i][4..].Trim();
            }

            names[i] = current;
        }

        return names;
    }
}
