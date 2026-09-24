using System.Diagnostics;
using AgentBoard;
using Gio;
using Gtk;
using Application = Adw.Application;
using File = System.IO.File;

namespace AgentBoard.GtkApp;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var repo = Environment.GetEnvironmentVariable("WORKCOSTS_ROOT");
        if (string.IsNullOrWhiteSpace(repo))
        {
            repo = GitProcess.Run(Environment.CurrentDirectory, "rev-parse", "--show-toplevel").Trim();
        }

        if (string.IsNullOrWhiteSpace(repo) || !Directory.Exists(repo))
        {
            Console.Error.WriteLine("Run AgentBoard from the WorkCosts git repository (or set WORKCOSTS_ROOT).");
            return 1;
        }

        var application = Application.New("app.willidiy.agentboard", ApplicationFlags.FlagsNone);
        application.OnActivate += (_, _) =>
        {
            var window = new BoardWindow(application, repo);
            window.Present();
        };
        return application.RunWithSynchronizationContext(null);
    }
}

internal sealed class BoardWindow : Adw.ApplicationWindow
{
    private readonly string _repo;
    private readonly ScriptHost _scripts;
    private readonly SessionStore _sessions;
    private readonly AgentSendQueue _sends = new();
    private readonly string _sessionPath;
    private readonly Gtk.Button _nextButton;
    private readonly Gtk.ListBox _queueList = Gtk.ListBox.New();
    private readonly Gtk.ListBox _workingList = Gtk.ListBox.New();
    private readonly Gtk.TextView _storyView = Gtk.TextView.New();
    private readonly Gtk.TextView _chatLog = Gtk.TextView.New();
    private readonly Gtk.Entry _chatEntry = Gtk.Entry.New();
    private readonly Gtk.Label _chatHeader = Gtk.Label.New("Chat");
    private readonly Gtk.Label _status = Gtk.Label.New("");
    private Dictionary<string, FeatureStory> _stories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<QueueRow> _queueRows = [];
    private readonly List<WorkingRow> _workingRows = [];
    private FocusedPane _focus = FocusedPane.Queue;
    private string? _queueKebab;
    private string? _workingKebab;
    private bool _showDone;
    private string _inboxOriginal = "";
    private string _inboxDraft = "";

    public BoardWindow(Adw.Application app, string repo) : base()
    {
        Application = app;
        _repo = repo;
        _scripts = new ScriptHost(repo);
        _sessionPath = Path.Combine(repo, "tools", "agent-board", ".sessions.json");
        _sessions = File.Exists(_sessionPath) ? SessionStore.ParseJson(File.ReadAllText(_sessionPath)) : new SessionStore();

        SetDefaultSize(1100, 760);
        Title = "Agent board";

        var header = Adw.HeaderBar.New();
        header.SetTitleWidget(Gtk.Label.New("Agent board"));
        _nextButton = Gtk.Button.New();
        _nextButton.AddCssClass("suggested-action");
        _nextButton.OnClicked += (_, _) => ActivateNext();
        header.PackEnd(_nextButton);

        var grid = Gtk.Grid.New();
        grid.ColumnHomogeneous = true;
        grid.RowHomogeneous = true;
        grid.ColumnSpacing = 8;
        grid.RowSpacing = 8;
        grid.Attach(Pane("Queue", _queueList), 0, 0, 1, 1);
        grid.Attach(Pane("Working", _workingList), 1, 0, 1, 1);
        _storyView.Editable = false;
        _storyView.WrapMode = WrapMode.WordChar;
        grid.Attach(Labeled("Story", Scroll(_storyView)), 0, 1, 1, 1);
        grid.Attach(ChatPane(), 1, 1, 1, 1);

        var bin = Adw.BreakpointBin.New();
        bin.SetChild(grid);
        var condition = Adw.BreakpointCondition.Parse("max-width: 799px");
        if (condition is not null)
        {
            var bp = Adw.Breakpoint.New(condition);
            bin.AddBreakpoint(bp);
        }

        var box = Gtk.Box.New(Orientation.Vertical, 0);
        box.Append(header);
        box.Append(bin);
        box.Append(_status);
        SetContent(box);

        _queueList.OnRowSelected += (_, args) =>
        {
            _focus = FocusedPane.Queue;
            if (args.Row is not null)
            {
                var i = args.Row.GetIndex();
                if (i >= 0 && i < _queueRows.Count)
                {
                    _queueKebab = _queueRows[i].Kebab;
                }
            }

            RefreshNextAndStory();
        };
        _workingList.OnRowSelected += (_, args) =>
        {
            _focus = FocusedPane.Working;
            if (args.Row is not null)
            {
                var i = args.Row.GetIndex();
                if (i >= 0 && i < _workingRows.Count)
                {
                    _workingKebab = _workingRows[i].Kebab;
                }
            }

            RefreshNextAndStory();
        };

        _chatEntry.OnActivate += (_, _) => SendChat();

        var keys = Gtk.EventControllerKey.New();
        keys.OnKeyPressed += OnKey;
        AddController(keys);

        Refresh(fetch: true);
    }

    private Gtk.Widget Pane(string title, Gtk.ListBox list)
    {
        var label = Gtk.Label.New(title);
        label.Xalign = 0;
        var box = Gtk.Box.New(Orientation.Vertical, 4);
        box.Append(label);
        box.Append(Scroll(list));
        return box;
    }

    private Gtk.Widget Labeled(string title, Gtk.Widget child)
    {
        var label = Gtk.Label.New(title);
        label.Xalign = 0;
        var box = Gtk.Box.New(Orientation.Vertical, 4);
        box.Append(label);
        box.Append(child);
        return box;
    }

    private static Gtk.ScrolledWindow Scroll(Gtk.Widget child)
    {
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(child);
        scroll.Vexpand = true;
        scroll.Hexpand = true;
        return scroll;
    }

    private Gtk.Widget ChatPane()
    {
        _chatLog.Editable = false;
        _chatLog.WrapMode = WrapMode.WordChar;
        var send = Gtk.Button.NewWithLabel("Send");
        send.OnClicked += (_, _) => SendChat();
        var prompt = Gtk.Box.New(Orientation.Horizontal, 6);
        _chatEntry.Hexpand = true;
        prompt.Append(_chatEntry);
        prompt.Append(send);
        var box = Gtk.Box.New(Orientation.Vertical, 4);
        _chatHeader.Xalign = 0;
        box.Append(_chatHeader);
        box.Append(Scroll(_chatLog));
        box.Append(prompt);
        return box;
    }

    private bool OnKey(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyPressedSignalArgs args)
    {
        var key = args.Keyval;
        if (key == Gdk.Constants.KEY_q)
        {
            TryQuit();
            return true;
        }

        if (key == Gdk.Constants.KEY_g)
        {
            Refresh(fetch: true);
            return true;
        }

        if (key == Gdk.Constants.KEY_r)
        {
            OpenInbox();
            return true;
        }

        if (key == Gdk.Constants.KEY_d)
        {
            _showDone = !_showDone;
            Refresh(fetch: false);
            return true;
        }

        if (key == Gdk.Constants.KEY_p)
        {
            PlanFeature();
            return true;
        }

        if (key == Gdk.Constants.KEY_i)
        {
            StartImplement();
            return true;
        }

        if (key == Gdk.Constants.KEY_n)
        {
            PickupNext();
            return true;
        }

        if (key == Gdk.Constants.KEY_m)
        {
            MergePlanning();
            return true;
        }

        if (key == Gdk.Constants.KEY_question)
        {
            ShowHelp();
            return true;
        }

        if (key == Gdk.Constants.KEY_Tab)
        {
            CycleFocus();
            return true;
        }

        return false;
    }

    private void CycleFocus()
    {
        _focus = _focus switch
        {
            FocusedPane.Queue => FocusedPane.Working,
            FocusedPane.Working => FocusedPane.Story,
            FocusedPane.Story => FocusedPane.Chat,
            _ => FocusedPane.Queue
        };
        if (_focus == FocusedPane.Chat)
        {
            _chatEntry.GrabFocus();
        }

        RefreshNextAndStory();
    }

    private void Refresh(bool fetch)
    {
        _stories = FeatureCatalog.Load(_repo, null, fetch);
        _queueRows.Clear();
        _queueRows.AddRange(QueueMembership.Build(_stories, _showDone));
        _workingRows.Clear();
        _workingRows.AddRange(WorkingPipeline.Build(_stories, Live));
        FillList(_queueList, _queueRows.Select(r => $"{Indent(r.Depth)}[{FmtSeq(r.Seq)}] {r.Kebab} - {r.Title}  ({r.Status}){r.IndicatorSuffix}"));
        FillList(_workingList, _workingRows.Select(r => $"{r.Kind}: {r.Kebab} - {r.Title}"));
        _focus = NextActionResolver.DefaultFocus(_workingRows.Count > 0);
        if (_workingRows.Count > 0)
        {
            _workingKebab ??= _workingRows[0].Kebab;
        }

        if (_queueRows.Count > 0)
        {
            _queueKebab ??= _queueRows[0].Kebab;
        }

        RefreshNextAndStory();
    }

    private static string Indent(int depth) => new string(' ', depth * 2);
    private static string FmtSeq(int seq) => seq == int.MaxValue ? "?" : seq.ToString();

    private bool Live(string kebab)
    {
        var id = _sessions.IdFor(kebab, planning: false);
        return _sends.InFlight > 0 && id is not null;
    }

    private static void FillList(Gtk.ListBox list, IEnumerable<string> lines)
    {
        Gtk.Widget? child;
        while ((child = list.GetFirstChild()) is not null)
        {
            list.Remove(child);
        }

        foreach (var line in lines)
        {
            var row = Gtk.ListBoxRow.New();
            var label = Gtk.Label.New(line);
            label.Xalign = 0;
            label.Wrap = true;
            row.SetChild(label);
            list.Append(row);
        }
    }

    private FeatureStory? Selected()
    {
        var kebab = _focus == FocusedPane.Working ? _workingKebab : _queueKebab;
        if (kebab is null)
        {
            return null;
        }

        return _stories.GetValueOrDefault(kebab);
    }

    private void RefreshNextAndStory()
    {
        var story = Selected();
        var dirty = GitProcess.IsDirty(_repo);
        var next = NextActionResolver.FollowsFocusedPane(
            _focus,
            _queueKebab is null ? null : _stories.GetValueOrDefault(_queueKebab),
            _workingKebab is null ? null : _stories.GetValueOrDefault(_workingKebab),
            dirty,
            _queueKebab is not null && Live(_queueKebab),
            _workingKebab is not null && Live(_workingKebab),
            _stories);
        var hint = string.IsNullOrEmpty(next.KeyHint) ? "" : $" · {next.KeyHint}";
        var kebab = next.Kebab is null ? "" : $" · {next.Kebab}";
        _nextButton.SetLabel($"Next: {next.Label}{hint}{kebab}");
        _nextButton.SetDataString("kind", next.Kind);

        if (story is null)
        {
            _storyView.Buffer!.SetText("No story selected.", -1);
        }
        else
        {
            var text = FeatureCatalog.StoryFileText(_repo, story);
            _storyView.Buffer!.SetText(string.IsNullOrWhiteSpace(text) ? "Story file missing." : text, -1);
        }

        var planning = story?.Status == "draft";
        var sid = _sessions.IdFor(story?.Kebab, planning);
        _chatHeader.SetText($"Chat · {story?.Kebab ?? "none"} · {_sends.Status} · {(sid ?? "no session")}");
        _status.SetText($"{GitProcess.CurrentBranch(_repo)}  dirty={(dirty ? "yes" : "no")}  focus={_focus}");
    }

    private void ActivateNext()
    {
        var kind = _nextButton.GetDataString("kind");
        switch (kind)
        {
            case "inbox":
                OpenInbox();
                break;
            case "resume":
                ResumeImplementation();
                break;
            case "complete":
                CompleteReview();
                break;
            case "plan":
                PlanFeature();
                break;
            case "merge-planning":
                MergePlanning();
                break;
            case "implement":
                StartImplement();
                break;
            case "attach":
                _chatEntry.GrabFocus();
                break;
            default:
                Toast("No story selected.");
                break;
        }
    }

    private void Toast(string message) => _status.SetText(message);

    private void SendChat(string? overridePrompt = null)
    {
        var prompt = overridePrompt ?? (_chatEntry.GetText() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        if (overridePrompt is null)
        {
            _chatEntry.SetText("");
        }

        var story = Selected();
        var planning = story?.Status == "draft" || prompt.StartsWith("/plan-feature", StringComparison.Ordinal);
        RunAgent(prompt, story?.Kebab, planning);
    }

    private void RunAgent(string prompt, string? kebab, bool planning)
    {
        if (string.IsNullOrWhiteSpace(Which("agent")))
        {
            _sends.Complete(false, $"agent not on PATH. See {AgentCli.HandbookLogin}");
            AppendChat($"failed: agent not on PATH. See {AgentCli.HandbookLogin}\n");
            RefreshNextAndStory();
            return;
        }

        var args = _sends.BuildSend(_repo, prompt, _sessions, kebab, planning);
        _sends.Begin();
        RefreshNextAndStory();
        System.Threading.Tasks.Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "agent",
                WorkingDirectory = _repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            try
            {
                using var proc = Process.Start(psi) ?? throw new InvalidOperationException("agent failed to start");
                var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                GLib.Functions.IdleAdd(0, () =>
                {
                    _sends.Complete(proc.ExitCode == 0, output);
                    AppendChat(output + "\n");
                    RefreshSessionsFromLs();
                    if (planning && !string.IsNullOrEmpty(_sessions.PlanningId) == false)
                    {
                        // keep existing
                    }

                    if (planning)
                    {
                        var id = ParseNewestSession();
                        if (id is not null)
                        {
                            _sessions.RememberPlanning(id);
                        }
                    }
                    else if (kebab is not null)
                    {
                        var id = ParseNewestSession();
                        if (id is not null)
                        {
                            _sessions.RememberImplement(kebab, id);
                        }
                    }

                    SaveSessions();
                    RefreshNextAndStory();
                    return false;
                });
            }
            catch (Exception ex)
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    _sends.Complete(false, ex.Message);
                    AppendChat($"failed: {ex.Message}. See {AgentCli.HandbookLogin}\n");
                    RefreshNextAndStory();
                    return false;
                });
            }
        });
    }

    private void AppendChat(string text)
    {
        var buffer = _chatLog.Buffer!;
        foreach (var span in AnsiDecoder.Decode(text))
        {
            buffer.InsertAtCursor(span.Text, span.Text.Length);
        }
    }

    private void RefreshSessionsFromLs()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "agent",
                WorkingDirectory = _repo,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("ls");
            using var proc = Process.Start(psi);
            proc?.WaitForExit(4000);
        }
        catch
        {
            // ignore
        }
    }

    private string? ParseNewestSession() => _sessions.PlanningId ?? _sessions.ImplementByKebab.Values.LastOrDefault();

    private void SaveSessions()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath)!);
        File.WriteAllText(_sessionPath, _sessions.ToJson());
    }

    private static string? Which(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private void PlanFeature()
    {
        try
        {
            GitProcess.Run(_repo, "fetch", "origin");
            GitProcess.Run(_repo, "checkout", "Planning");
        }
        catch (Exception ex)
        {
            Toast(ex.Message);
        }

        var story = Selected();
        var name = story?.Kebab;
        if (string.IsNullOrWhiteSpace(name))
        {
            Toast("Select a story or type a name in Chat as /plan-feature <name>.");
            _chatEntry.GrabFocus();
            return;
        }

        RunAgent($"/plan-feature Continue {name}. Source of truth is the feature file if it exists.", name, planning: true);
    }

    private void StartImplement()
    {
        var story = Selected();
        if (story is null)
        {
            Toast("No story selected.");
            return;
        }

        RunAgent($"/start-implement {story.Kebab}", story.Kebab, planning: false);
    }

    private void PickupNext()
    {
        var startable = _stories.Values
            .Where(s => s.OnMain && s.MainStatus == "ready-for-agent" && StoryMarkdown.DepsDoneOnMain(s, _stories) && !StoryMarkdown.IsHiddenOpsNoise(s.Kebab))
            .OrderBy(s => s.Seq)
            .ThenBy(s => s.Kebab)
            .FirstOrDefault();
        if (startable is null)
        {
            Toast("QUEUE_EMPTY");
            return;
        }

        RunAgent("/pickup-next-feature", startable.Kebab, planning: false);
    }

    private void MergePlanning()
    {
        if (GitProcess.IsDirty(_repo))
        {
            Toast("Dirty tree — refuse merge-planning.");
            return;
        }

        var dialog = Adw.MessageDialog.New(this, "Merge Planning into main?", "Rebase + squash + fast-forward. Never squash-merges a product PR.");
        dialog.AddResponse("cancel", "Cancel");
        dialog.AddResponse("ok", "Merge");
        dialog.SetResponseAppearance("ok", Adw.ResponseAppearance.Suggested);
        dialog.OnResponse += (_, args) =>
        {
            if (args.Response != "ok")
            {
                return;
            }

            RunScript("merge-planning.sh");
        };
        dialog.Present();
    }

    private void ResumeImplementation()
    {
        var story = Selected();
        if (story is null)
        {
            return;
        }

        RunAgent("/resume-implementation", story.Kebab, planning: false);
    }

    private void CompleteReview()
    {
        var story = Selected();
        if (story is null)
        {
            return;
        }

        var inbox = FeatureCatalog.LoadInbox(_repo);
        var heading = InboxParser.Find(InboxParser.Parse(inbox), story.Kebab);
        var url = heading is null ? null : InboxParser.PrUrlFromInboxText(heading.Block);
        var body = url is null
            ? "No PR yet. Review and squash-merge on GitHub when a URL is in the inbox Change set. Continue fetches main, deletes the feature branch, writes the squash SHA. Send back returns ready-for-review. The board does not squash-merge."
            : $"PR: {url}\nReview and squash-merge on GitHub. Continue = fetch, checkout main, pull, delete feature branch, last note. Send back = ready-for-review. The board does not squash-merge.";
        var dialog = Adw.MessageDialog.New(this, "complete-review", body);
        dialog.AddResponse("cancel", "Cancel");
        dialog.AddResponse("back", "Send back");
        dialog.AddResponse("ok", "Continue");
        dialog.OnResponse += (_, args) =>
        {
            if (args.Response == "ok")
            {
                RunAgent("/complete-review Continue after squash-merge", story.Kebab, false);
            }
            else if (args.Response == "back")
            {
                RunAgent("/complete-review Send back to ready-for-review", story.Kebab, false);
            }
        };
        dialog.Present();
    }

    private void RunScript(string name)
    {
        var argv = _scripts.ArgvFor(name);
        var psi = new ProcessStartInfo
        {
            FileName = _scripts.BashExecutable,
            WorkingDirectory = _repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in argv)
        {
            psi.ArgumentList.Add(a);
        }

        try
        {
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("script failed to start");
            var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            Toast(proc.ExitCode == 0 ? output.Split('\n').LastOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "ok" : output);
            Refresh(fetch: true);
        }
        catch (Exception ex)
        {
            Toast(ex.Message);
        }
    }

    private void OpenInbox()
    {
        _inboxOriginal = FeatureCatalog.LoadInbox(_repo);
        _inboxDraft = _inboxOriginal;
        var dialog = Adw.Dialog.New();
        dialog.SetTitle("Inbox");
        dialog.ContentWidth = 900;
        dialog.ContentHeight = 560;

        var list = Gtk.ListBox.New();
        var parsed = InboxParser.Parse(_inboxDraft);
        var overlay = InboxParser.OverlayList(parsed).ToList();
        foreach (var h in overlay)
        {
            var row = Gtk.ListBoxRow.New();
            row.SetChild(Gtk.Label.New($"{h.Kebab}  ({h.Status})"));
            list.Append(row);
        }

        var detail = Gtk.Box.New(Orientation.Vertical, 8);
        var scrollDetail = Scroll(detail);
        list.OnRowSelected += (_, args) =>
        {
            while (detail.GetFirstChild() is { } c)
            {
                detail.Remove(c);
            }

            if (args.Row is null)
            {
                return;
            }

            var heading = overlay[args.Row.GetIndex()];
            BuildInboxDetail(detail, heading);
        };

        var paned = Gtk.Box.New(Orientation.Horizontal, 12);
        var left = Scroll(list);
        left.SetSizeRequest(240, -1);
        paned.Append(left);
        paned.Append(scrollDetail);

        dialog.SetChild(paned);
        dialog.OnClosed += (_, _) => CloseInbox();
        dialog.Present(this);
    }

    private void BuildInboxDetail(Gtk.Box detail, InboxHeading heading)
    {
        detail.Append(Gtk.Label.New($"{heading.Kebab} — auto-status {InboxAutoStatus.Compute(heading)}"));
        foreach (var q in heading.Questions)
        {
            var entry = Gtk.Entry.New();
            entry.SetText(q.Answer);
            entry.PlaceholderText = q.Text;
            var captured = q;
            var kebab = heading.Kebab;
            entry.OnActivate += (_, _) =>
            {
                var answered = !string.IsNullOrWhiteSpace(entry.GetText());
                _inboxDraft = InboxParser.SetQuestionAnswer(_inboxDraft, kebab, captured.Index, entry.GetText() ?? "", answered);
                _inboxDraft = InboxParser.ApplyAutoStatus(_inboxDraft, kebab);
            };
            detail.Append(Gtk.Label.New(q.Text));
            detail.Append(entry);
        }

        foreach (var d in heading.Deviations)
        {
            var kebab = heading.Kebab;
            var captured = d;
            var accept = Gtk.Button.NewWithLabel("Accept");
            var reject = Gtk.Button.NewWithLabel("Reject");
            var reason = Gtk.Entry.New();
            reason.PlaceholderText = "Reject reason";
            reason.SetText(d.Reason);
            accept.OnClicked += (_, _) =>
            {
                _inboxDraft = InboxParser.SetDeviation(_inboxDraft, kebab, captured.Index, DeviationDecision.Accept, "");
                _inboxDraft = InboxParser.ApplyAutoStatus(_inboxDraft, kebab);
            };
            reject.OnClicked += (_, _) =>
            {
                _inboxDraft = InboxParser.SetDeviation(_inboxDraft, kebab, captured.Index, DeviationDecision.Reject, reason.GetText() ?? "");
                _inboxDraft = InboxParser.ApplyAutoStatus(_inboxDraft, kebab);
            };
            detail.Append(Gtk.Label.New(d.Text));
            var row = Gtk.Box.New(Orientation.Horizontal, 6);
            row.Append(accept);
            row.Append(reject);
            row.Append(reason);
            detail.Append(row);
        }

        var verify = Gtk.CheckButton.NewWithLabel("Verify change set");
        verify.Active = heading.VerifyTicked;
        verify.OnToggled += (_, _) =>
        {
            _inboxDraft = InboxParser.SetVerify(_inboxDraft, heading.Kebab, verify.Active);
            _inboxDraft = InboxParser.ApplyAutoStatus(_inboxDraft, heading.Kebab);
        };
        detail.Append(verify);
    }

    private void CloseInbox()
    {
        var closer = new InboxCloser();
        var dirty = !string.Equals(_inboxDraft, _inboxOriginal, StringComparison.Ordinal);
        var result = closer.Close(dirty, _ =>
        {
            var path = Path.Combine(_repo, "docs", "features", "to-review.md");
            File.WriteAllText(path, _inboxDraft);
            RunScript("update-to-review.sh");
        });
        if (result == "dismiss")
        {
            Toast("Inbox closed.");
        }

        Refresh(fetch: true);
    }

    private void ShowHelp()
    {
        var d = Adw.Dialog.New();
        d.SetTitle("Help");
        var label = Gtk.Label.New("Tab panes · arrows in lists · Enter send · Esc overlay · q quit · g fetch · r inbox · d done · p plan · i implement · n pickup · m merge-planning · Next button runs the current action.");
        label.Wrap = true;
        d.SetChild(label);
        d.Present(this);
    }

    private void TryQuit()
    {
        if (QuitPolicy.NeedsConfirm(_sends.InFlight))
        {
            var dialog = Adw.MessageDialog.New(this, "Quit?", QuitPolicy.ConfirmMessage(_sends.InFlight));
            dialog.AddResponse("cancel", "Cancel quit");
            dialog.AddResponse("ok", "Stop sends");
            dialog.OnResponse += (_, args) =>
            {
                if (args.Response == "ok")
                {
                    Application?.Quit();
                }
            };
            dialog.Present();
            return;
        }

        Application?.Quit();
    }
}

internal static class WidgetData
{
    private static readonly Dictionary<Gtk.Button, string> Kind = new();

    public static void SetDataString(this Gtk.Button button, string _, string value) => Kind[button] = value;

    public static string GetDataString(this Gtk.Button button, string _) => Kind.GetValueOrDefault(button) ?? "";
}
