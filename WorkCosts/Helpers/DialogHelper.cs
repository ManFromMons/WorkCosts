using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WorkCosts.Helpers;

public static class DialogHelper
{
    private static int _openDialogs;

    public static bool HasOpenDialog => _openDialogs > 0;

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, XamlRoot xamlRoot)
    {
        dialog.XamlRoot = xamlRoot;
        return await WithOpenDialogAsync(async () => await dialog.ShowAsync());
    }

    public static async Task ShowMessageAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
        dialog.Content = CreateFilledMessage(message);
        await ShowAsync(dialog, xamlRoot);
    }

    public static async Task<bool> ConfirmAsync(
        XamlRoot xamlRoot,
        string title,
        string message,
        string primaryButtonText = "Delete",
        string closeButtonText = "Cancel")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
        dialog.Content = CreateFilledMessage(message);
        return await ShowAsync(dialog, xamlRoot) == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Yes / No. Enter is Yes, Esc is No.
    /// </summary>
    public static async Task<bool> ConfirmYesNoAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };
        return await ShowAsync(dialog, xamlRoot) == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Primary / Secondary / Close (Esc). Close is Cancel and is the default, so Esc ends the dialog.
    /// </summary>
    public static async Task<ContentDialogResult> ShowChoiceAsync(
        XamlRoot xamlRoot,
        string title,
        string message,
        string primaryButtonText,
        string secondaryButtonText,
        string closeButtonText = "Cancel")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
        dialog.Content = CreateFilledMessage(message);
        return await ShowAsync(dialog, xamlRoot);
    }

    private static async Task<T> WithOpenDialogAsync<T>(Func<Task<T>> show)
    {
        _openDialogs++;
        try
        {
            return await show();
        }
        finally
        {
            _openDialogs--;
        }
    }

    /// <summary>
    /// Read-only wrapping body. Do not bind width/height to the dialog — that causes a layout loop
    /// that freezes WinUI ContentDialogs (Add/Cancel/Esc stop working).
    /// </summary>
    public static FrameworkElement CreateFilledMessage(string message, ContentDialog? dialog = null, bool fillPanel = false)
    {
        _ = dialog;
        var box = new TextBox
        {
            Text = message,
            IsReadOnly = true,
            IsTabStop = false,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.WrapWholeWords,
            IsSpellCheckEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            MinHeight = fillPanel ? 120 : 0,
            MaxWidth = 520,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Disabled);
        return box;
    }
}
