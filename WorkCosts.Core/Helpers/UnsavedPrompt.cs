namespace WorkCosts.Helpers;

public static class UnsavedPrompt
{
    public static readonly TimeSpan UserLeaveTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

    public static bool IsTimedOutSave(TimeSpan elapsed, TimeSpan timeout) => elapsed >= timeout;
}
