using Microsoft.UI.Xaml;

namespace WorkCosts.Helpers;

public static class UnsavedChangesLeave
{
    public static async Task<bool> TryLeaveAsync(
        IUnsavedChangesSource source,
        XamlRoot xamlRoot,
        TimeSpan timeout,
        bool waitForOpenDialog = false)
    {
        if (waitForOpenDialog)
        {
            while (DialogHelper.HasOpenDialog)
            {
                await Task.Delay(50);
            }
        }
        else if (DialogHelper.HasOpenDialog)
        {
            return false;
        }

        await source.FlushPendingAsync();
        if (!source.HasUnsavedChanges)
        {
            return true;
        }

        UnsavedPromptChoice choice;
        try
        {
            choice = await DialogHelper.ConfirmUnsavedWithTimeoutAsync(xamlRoot, timeout);
        }
        catch
        {
            await source.DiscardUnsavedAsync();
            return true;
        }

        switch (choice.Result)
        {
            case UnsavedPromptResult.Discard:
                await source.DiscardUnsavedAsync();
                return true;
            case UnsavedPromptResult.Save:
                if (await source.SaveUnsavedAsync())
                {
                    return true;
                }

                if (choice.TimedOut)
                {
                    await source.DiscardUnsavedAsync();
                    return true;
                }

                return false;
            default:
                return false;
        }
    }
}
