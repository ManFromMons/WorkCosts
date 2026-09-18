namespace WorkCosts.Helpers;

public interface IUnsavedChangesSource
{
    bool HasUnsavedChanges { get; }

    Task FlushPendingAsync();

    Task<bool> SaveUnsavedAsync();

    Task DiscardUnsavedAsync();
}
