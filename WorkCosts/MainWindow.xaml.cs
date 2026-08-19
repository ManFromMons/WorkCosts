using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WorkCosts.Helpers;
using WorkCosts.Pages;

namespace WorkCosts;

public sealed partial class MainWindow : Window
{
    public Frame ContentFrame => NavFrame;

    private bool _suppressThemeToggle;

    public MainWindow()
    {
        StartupLog.Write("MainWindow ctor: InitializeComponent.");
        try
        {
            InitializeComponent();
            StartupLog.Write("MainWindow.InitializeComponent succeeded.");
        }
        catch (Exception ex)
        {
            StartupLog.Write("MainWindow.InitializeComponent failed (check x:Name / resource keys in MainWindow.xaml).", ex);
            throw;
        }

        try
        {
            AppThemeService.Instance.Initialize(this);
            StartupLog.Write($"AppThemeService.Initialize succeeded. Theme={AppThemeService.Instance.EffectiveTheme}.");
        }
        catch (Exception ex)
        {
            StartupLog.Write("AppThemeService.Initialize failed.", ex);
            throw;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));

        NavFrame.NavigationFailed += NavFrame_NavigationFailed;
        AppTitleBar.Loaded += (_, _) => ApplyTitleBarTitleFont();
        AppThemeService.Instance.ThemeChanged += OnThemeChanged;
        Closed += MainWindow_Closed;
        ApplyCaptionButtonColors();
        SyncTitleBarThemeToggle();
        StartupLog.Write("MainWindow ctor finished.");
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        AppThemeService.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyCaptionButtonColors();
        SyncTitleBarThemeToggle();
    }

    private void ApplyTitleBarTitleFont()
    {
        var paragraphSize = Application.Current.Resources.TryGetValue("AppContentFontSize", out var size) && size is double d
            ? d
            : 18;
        foreach (var text in FindDescendants<TextBlock>(AppTitleBar))
        {
            if (text.Name == "PART_TitleText")
            {
                text.FontSize = paragraphSize;
                return;
            }
        }
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private void ApplyCaptionButtonColors()
    {
        var dark = AppThemeService.Instance.EffectiveTheme == ElementTheme.Dark;
        var foreground = dark ? Colors.White : Colors.Black;
        var hover = dark ? ColorHelper.FromArgb(0xFF, 0xE0, 0xE0, 0xE0) : ColorHelper.FromArgb(0xFF, 0x32, 0x32, 0x32);
        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverForegroundColor = hover;
        AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = dark
            ? ColorHelper.FromArgb(0x33, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x33, 0x00, 0x00, 0x00);
        AppWindow.TitleBar.ButtonPressedBackgroundColor = dark
            ? ColorHelper.FromArgb(0x55, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x55, 0x00, 0x00, 0x00);
    }

    private async void NavFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        e.Handled = true;
        var ex = e.Exception;
        var message = FormatException(ex) ?? "Unknown navigation failure.";
        StartupLog.Write($"NavigationFailed -> {e.SourcePageType}: {message}", ex);
        try
        {
            await DialogHelper.ShowMessageAsync(
                Content.XamlRoot,
                "Navigation failed",
                $"{e.SourcePageType?.FullName}\n\n{message}");
        }
        catch (Exception dialogEx)
        {
            Debug.WriteLine($"NavigationFailed dialog failed: {dialogEx}");
        }
    }

    private static string FormatException(Exception? ex)
    {
        if (ex is null)
        {
            return "Unknown error.";
        }

        var parts = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            parts.Add($"Inner {inner.GetType().Name}: {inner.Message}");
        }

        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            parts.Add(ex.StackTrace);
        }

        return string.Join("\n\n", parts);
    }

    public void NavigateToHome()
    {
        StartupLog.Write("NavigateToHome.");
        if (NavView.MenuItems[0] is NavigationViewItem home)
        {
            NavView.SelectedItem = home;
        }

        NavigateTo(typeof(HomePage), clearBackStack: true);
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Home navigation happens after database init in App.StartAsync.
    }

    private void TitleBarThemeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressThemeToggle)
        {
            return;
        }

        var preference = TitleBarThemeSwitch.IsOn
            ? AppThemePreference.Dark
            : AppThemePreference.Light;
        AppThemeService.Instance.SetPreference(preference);
    }

    private void SyncTitleBarThemeToggle()
    {
        _suppressThemeToggle = true;
        TitleBarThemeSwitch.IsOn = AppThemeService.Instance.EffectiveTheme == ElementTheme.Dark;
        _suppressThemeToggle = false;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType == typeof(WorkJobDetailPage))
        {
            foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag as string == "home")
                {
                    NavView.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateTo(typeof(SettingsPage), clearBackStack: true);
            return;
        }

        // Parent "Stuff" has no Tag — expand/collapse only.
        if (args.InvokedItemContainer is NavigationViewItem { Tag: string tag })
        {
            NavigateToTag(tag);
        }
    }

    private void NavigateToTag(string tag)
    {
        var pageType = tag switch
        {
            "home" => typeof(HomePage),
            "work" => typeof(WorkPage),
            "categories" => typeof(CategoriesPage),
            "jobs" => typeof(MasterDetailPage),
            "products" => typeof(ProductsPage),
            _ => null
        };

        if (pageType is not null)
        {
            NavigateTo(pageType, clearBackStack: true);
        }
    }

    private async void NavigateTo(Type pageType, bool clearBackStack)
    {
        if (NavFrame.Content?.GetType() == pageType)
        {
            return;
        }

        try
        {
            StartupLog.Write($"NavigateTo {pageType.FullName}.");
            if (!NavFrame.Navigate(pageType))
            {
                StartupLog.Write($"Navigate returned false for {pageType.FullName}.");
                await DialogHelper.ShowMessageAsync(
                    Content.XamlRoot,
                    "Navigation failed",
                    $"Navigate returned false for {pageType.FullName}.");
                return;
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write($"Navigate threw for {pageType.FullName}.", ex);
            try
            {
                await DialogHelper.ShowMessageAsync(
                    Content.XamlRoot,
                    "Navigation failed",
                    $"{pageType.FullName}\n\n{FormatException(ex)}");
            }
            catch (Exception dialogEx)
            {
                Debug.WriteLine($"Navigate error dialog failed: {dialogEx}");
            }

            return;
        }

        if (clearBackStack)
        {
            NavFrame.BackStack.Clear();
        }
    }
}
