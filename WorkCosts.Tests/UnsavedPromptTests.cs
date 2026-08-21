using WorkCosts.Helpers;
using Xunit;

namespace WorkCosts.Tests;

public class UnsavedPromptTests
{
    [Fact]
    public void UserLeaveTimeout_is_20_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(20), UnsavedPrompt.UserLeaveTimeout);
    }

    [Fact]
    public void ShutdownTimeout_is_10_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), UnsavedPrompt.ShutdownTimeout);
    }

    [Fact]
    public void Elapsed_under_timeout_is_not_timed_out_save()
    {
        Assert.False(UnsavedPrompt.IsTimedOutSave(TimeSpan.FromSeconds(19), UnsavedPrompt.UserLeaveTimeout));
        Assert.False(UnsavedPrompt.IsTimedOutSave(TimeSpan.FromSeconds(9), UnsavedPrompt.ShutdownTimeout));
        Assert.False(UnsavedPrompt.IsTimedOutSave(TimeSpan.Zero, UnsavedPrompt.UserLeaveTimeout));
    }

    [Fact]
    public void Elapsed_equal_or_over_timeout_is_timed_out_save()
    {
        Assert.True(UnsavedPrompt.IsTimedOutSave(UnsavedPrompt.UserLeaveTimeout, UnsavedPrompt.UserLeaveTimeout));
        Assert.True(UnsavedPrompt.IsTimedOutSave(TimeSpan.FromSeconds(21), UnsavedPrompt.UserLeaveTimeout));
        Assert.True(UnsavedPrompt.IsTimedOutSave(UnsavedPrompt.ShutdownTimeout, UnsavedPrompt.ShutdownTimeout));
        Assert.True(UnsavedPrompt.IsTimedOutSave(TimeSpan.FromSeconds(11), UnsavedPrompt.ShutdownTimeout));
    }
}
