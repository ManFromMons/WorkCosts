namespace WorkCosts.Helpers;

public enum UnsavedPromptResult
{
    Save,
    Discard,
    Cancel
}

public readonly record struct UnsavedPromptChoice(UnsavedPromptResult Result, bool TimedOut);
