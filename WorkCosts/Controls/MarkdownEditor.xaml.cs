using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Windows.System;
using Windows.UI.Core;

namespace WorkCosts.Controls;

public sealed partial class MarkdownEditor : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(MarkdownEditor),
            new PropertyMetadata(string.Empty, OnTextPropertyChanged));

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex HeadingPrefix = new(@"^#+\s*", RegexOptions.Compiled);
    private static readonly Regex ListPrefix = new(@"^-\s*", RegexOptions.Compiled);
    private static readonly Regex QuotePrefix = new(@"^>\s*", RegexOptions.Compiled);

    private const int MaxUndoLevels = 100;

    private readonly Stack<EditorSnapshot> _undoStack = new();
    private readonly Stack<EditorSnapshot> _redoStack = new();

    private bool _syncing;
    private bool _undoing;
    private bool _webViewReady;
    private bool _webViewFailed;
    private int _lastSelectionStart;
    private int _lastSelectionLength;

    public MarkdownEditor()
    {
        InitializeComponent();
        Loaded += MarkdownEditor_Loaded;
        ToolbarBorder.AddHandler(
            PointerPressedEvent,
            new PointerEventHandler(Toolbar_PointerPressed),
            handledEventsToo: true);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value ?? string.Empty);
    }

    public event EventHandler? TextChanged;

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownEditor editor)
        {
            editor.ApplyExternalText(e.NewValue as string ?? string.Empty);
        }
    }

    private async void MarkdownEditor_Loaded(object sender, RoutedEventArgs e)
    {
        CaptureSelection();
        await EnsureWebViewAsync();
        await RefreshPreviewAsync();
    }

    private void ApplyExternalText(string text)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        if (EditorBox.Text != text)
        {
            EditorBox.Text = text;
        }

        _syncing = false;
        ClearUndoHistory();
        CaptureSelection();
        _ = RefreshPreviewAsync();
    }

    private void EditorBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        if (_syncing || _undoing)
        {
            return;
        }

        PushUndoSnapshot();
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        Text = EditorBox.Text;
        _syncing = false;
        if (EditorBox.FocusState != FocusState.Unfocused)
        {
            CaptureSelection();
        }

        TextChanged?.Invoke(this, EventArgs.Empty);
        _ = RefreshPreviewAsync();
    }

    private void EditorBox_GotFocus(object sender, RoutedEventArgs e) => CaptureSelection();

    private void EditorBox_LosingFocus(object sender, RoutedEventArgs e) => CaptureSelection();

    private void EditorBox_SelectionActivity(object sender, RoutedEventArgs e) => CaptureSelection();

    private void EditorBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        // Focus moves to the toolbar before Click runs; ignore the reset selection.
        if (EditorBox.FocusState != FocusState.Unfocused)
        {
            CaptureSelection();
        }
    }

    private void Toolbar_PointerPressed(object sender, PointerRoutedEventArgs e) => StashCaretForToolbar();

    private void EditorBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsControlDown())
        {
            return;
        }

        if (e.Key == VirtualKey.Z)
        {
            if (IsShiftDown())
            {
                Redo();
            }
            else
            {
                Undo();
            }

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Y)
        {
            Redo();
            e.Handled = true;
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();

    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();

    private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = RefreshPreviewAsync();
    }

    private async Task EnsureWebViewAsync()
    {
        if (_webViewReady || _webViewFailed)
        {
            return;
        }

        try
        {
            await PreviewView.EnsureCoreWebView2Async();
            PreviewView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PreviewView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webViewReady = true;
        }
        catch (Exception)
        {
            _webViewFailed = true;
            PreviewView.Visibility = Visibility.Collapsed;
            PreviewFallback.Visibility = Visibility.Visible;
        }
    }

    private async Task RefreshPreviewAsync()
    {
        var markdown = EditorBox.Text ?? string.Empty;
        var bodyHtml = Markdown.ToHtml(markdown, Pipeline);

        if (_webViewFailed)
        {
            PreviewFallback.Text = WebUtility.HtmlDecode(
                Regex.Replace(bodyHtml, "<[^>]+>", " "));
            return;
        }

        await EnsureWebViewAsync();
        if (!_webViewReady)
        {
            return;
        }

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        html.Append("<style>");
        html.Append("html,body{margin:0;padding:8px;background:transparent;color:#1a1a1a;");
        html.Append("font-family:'Segoe UI',sans-serif;font-size:14px;line-height:1.45;}");
        html.Append("h1,h2,h3{margin:0.6em 0 0.35em;} code{font-family:Consolas,monospace;");
        html.Append("background:#f0f0f0;padding:0 4px;border-radius:3px;}");
        html.Append("pre{background:#f0f0f0;padding:8px;overflow:auto;border-radius:4px;}");
        html.Append("blockquote{margin:0.5em 0;padding-left:12px;border-left:3px solid #999;opacity:0.9;}");
        html.Append("a{color:#0b57d0;} ul,ol{padding-left:1.4em;}");
        html.Append("</style></head><body>");
        html.Append(bodyHtml);
        html.Append("</body></html>");

        PreviewView.NavigateToString(html.ToString());
    }

    private void Bold_Click(object sender, RoutedEventArgs e) => WrapSelection("**", "**");

    private void Italic_Click(object sender, RoutedEventArgs e) => WrapSelection("*", "*");

    private void Code_Click(object sender, RoutedEventArgs e) => WrapSelection("`", "`");

    private void Heading_Click(object sender, RoutedEventArgs e)
    {
        var level = sender is FrameworkElement { Tag: string tag } && int.TryParse(tag, out var n) ? n : 2;
        ApplyHeading(level);
    }

    private void List_Click(object sender, RoutedEventArgs e) => PrefixCurrentLine("- ");

    private void Quote_Click(object sender, RoutedEventArgs e) => PrefixCurrentLine("> ");

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        PushUndoSnapshot();
        var selected = GetSelectedText();
        var label = string.IsNullOrWhiteSpace(selected) ? "text" : selected;
        ReplaceSelection($"[{label}](https://)");
    }

    private void ApplyHeading(int level)
    {
        PushUndoSnapshot();

        var text = EditorBox.Text ?? string.Empty;
        var caret = GetCaretIndex();
        var lineStart = GetLineStart(text, caret);
        var lineEnd = GetLineEnd(text, caret);
        var line = text[lineStart..lineEnd];

        var body = HeadingPrefix.Replace(line, string.Empty);
        if (body.StartsWith("- ", StringComparison.Ordinal))
        {
            body = body[2..];
        }

        if (body.StartsWith("> ", StringComparison.Ordinal))
        {
            body = body[2..];
        }

        var prefix = new string('#', level) + " ";
        var newLine = prefix + body;
        var newText = text.Remove(lineStart, lineEnd - lineStart).Insert(lineStart, newLine);
        ApplyEditorText(newText, lineStart + prefix.Length, 0);
    }

    private void WrapSelection(string prefix, string suffix)
    {
        PushUndoSnapshot();

        var (start, length) = GetCaretSelection();
        var text = EditorBox.Text ?? string.Empty;
        start = Clamp(start, 0, text.Length);
        length = Clamp(length, 0, text.Length - start);

        var selected = length > 0 ? text.Substring(start, length) : "text";
        var replacement = prefix + selected + suffix;
        var newText = text.Remove(start, length).Insert(start, replacement);
        ApplyEditorText(newText, start + prefix.Length, selected.Length);
    }

    private void PrefixCurrentLine(string prefix)
    {
        PushUndoSnapshot();

        var text = EditorBox.Text ?? string.Empty;
        var caret = GetCaretIndex();
        var lineStart = GetLineStart(text, caret);
        var lineEnd = GetLineEnd(text, caret);
        var line = text[lineStart..lineEnd];

        string newLine;
        if (prefix.StartsWith("- ", StringComparison.Ordinal) && ListPrefix.IsMatch(line))
        {
            newLine = ListPrefix.Replace(line, string.Empty);
        }
        else if (prefix.StartsWith("> ", StringComparison.Ordinal) && QuotePrefix.IsMatch(line))
        {
            newLine = QuotePrefix.Replace(line, string.Empty);
        }
        else
        {
            newLine = prefix + line;
        }

        var newText = text.Remove(lineStart, lineEnd - lineStart).Insert(lineStart, newLine);
        var caretOffset = newLine.Length - line.Length;
        ApplyEditorText(newText, caret + caretOffset, 0);
    }

    private string GetSelectedText()
    {
        var text = EditorBox.Text ?? string.Empty;
        var (start, length) = GetCaretSelection();
        if (length <= 0 || start < 0 || start + length > text.Length)
        {
            return string.Empty;
        }

        return text.Substring(start, length);
    }

    private void ReplaceSelection(string replacement)
    {
        var (start, length) = GetCaretSelection();
        var text = EditorBox.Text ?? string.Empty;
        start = Clamp(start, 0, text.Length);
        length = Clamp(length, 0, text.Length - start);

        var newText = text.Remove(start, length).Insert(start, replacement);
        ApplyEditorText(newText, start + replacement.Length, 0);
    }

    private void ApplyEditorText(string text, int selectionStart, int selectionLength)
    {
        _syncing = true;
        EditorBox.Text = text;
        selectionStart = Clamp(selectionStart, 0, text.Length);
        selectionLength = Clamp(selectionLength, 0, text.Length - selectionStart);
        EditorBox.SelectionStart = selectionStart;
        EditorBox.SelectionLength = selectionLength;
        _lastSelectionStart = selectionStart;
        _lastSelectionLength = selectionLength;
        Text = text;
        _syncing = false;
        TextChanged?.Invoke(this, EventArgs.Empty);
        UpdateUndoRedoButtons();
        _ = RefreshPreviewAsync();
        EditorBox.Focus(FocusState.Programmatic);
    }

    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Push(CaptureSnapshot());
        ApplySnapshot(_undoStack.Pop());
    }

    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Push(CaptureSnapshot());
        ApplySnapshot(_redoStack.Pop());
    }

    private void ApplySnapshot(EditorSnapshot snapshot)
    {
        _undoing = true;
        _syncing = true;
        EditorBox.Text = snapshot.Text;
        var selectionStart = Clamp(snapshot.SelectionStart, 0, snapshot.Text.Length);
        var selectionLength = Clamp(snapshot.SelectionLength, 0, snapshot.Text.Length - selectionStart);
        EditorBox.SelectionStart = selectionStart;
        EditorBox.SelectionLength = selectionLength;
        _lastSelectionStart = selectionStart;
        _lastSelectionLength = selectionLength;
        Text = snapshot.Text;
        _syncing = false;
        _undoing = false;
        TextChanged?.Invoke(this, EventArgs.Empty);
        UpdateUndoRedoButtons();
        _ = RefreshPreviewAsync();
        EditorBox.Focus(FocusState.Programmatic);
    }

    private void PushUndoSnapshot()
    {
        if (_undoing)
        {
            return;
        }

        var snapshot = CaptureSnapshot();
        if (_undoStack.Count > 0 && _undoStack.Peek() == snapshot)
        {
            return;
        }

        _undoStack.Push(snapshot);
        TrimUndoStack();
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private EditorSnapshot CaptureSnapshot() =>
        new(EditorBox.Text ?? string.Empty, _lastSelectionStart, _lastSelectionLength);

    private void CaptureSelection()
    {
        _lastSelectionStart = EditorBox.SelectionStart;
        _lastSelectionLength = EditorBox.SelectionLength;
    }

    private void StashCaretForToolbar()
    {
        if (EditorBox.FocusState != FocusState.Unfocused)
        {
            CaptureSelection();
        }
    }

    private (int start, int length) GetCaretSelection() =>
        (_lastSelectionStart, _lastSelectionLength);

    private int GetCaretIndex() => _lastSelectionStart;

    private static int GetLineStart(string text, int caret)
    {
        if (caret <= 0)
        {
            return 0;
        }

        var index = text.LastIndexOf('\n', caret - 1);
        return index < 0 ? 0 : index + 1;
    }

    private static int GetLineEnd(string text, int caret)
    {
        var index = text.IndexOf('\n', caret);
        return index < 0 ? text.Length : index;
    }

    private void ClearUndoHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private void TrimUndoStack()
    {
        if (_undoStack.Count <= MaxUndoLevels)
        {
            return;
        }

        var items = _undoStack.ToArray();
        _undoStack.Clear();
        for (var i = MaxUndoLevels - 1; i >= 0; i--)
        {
            _undoStack.Push(items[i]);
        }
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _undoStack.Count > 0;
        RedoButton.IsEnabled = _redoStack.Count > 0;
    }

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;

    private static bool IsControlDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);

    private static bool IsShiftDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);

    private readonly record struct EditorSnapshot(string Text, int SelectionStart, int SelectionLength);
}
