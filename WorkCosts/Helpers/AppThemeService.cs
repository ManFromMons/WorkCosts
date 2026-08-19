using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace WorkCosts.Helpers;

public enum AppThemePreference
{
    Auto,
    Light,
    Dark
}

/// <summary>
/// Applies Auto / Light / Dark. Chrome colours and control styles live in
/// Styles/Light and Styles/Dark, merged through App.xaml ThemeDictionaries.
/// </summary>
public sealed class AppThemeService
{
    private const string ThemeSettingKey = "AppThemePreference";

    private readonly UISettings _uiSettings = new();
    private Window? _window;
    private bool _initialized;

    public static AppThemeService Instance { get; } = new();

    public AppThemePreference Preference { get; private set; } = AppThemePreference.Auto;

    public ElementTheme EffectiveTheme { get; private set; } = ElementTheme.Default;

    public event EventHandler? ThemeChanged;

    public void Initialize(Window window)
    {
        _window = window;
        Preference = LoadPreference();

        if (MicaController.IsSupported())
        {
            window.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }

        ApplyTheme();

        if (!_initialized)
        {
            _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
            _initialized = true;
        }
    }

    public void SetPreference(AppThemePreference preference)
    {
        Preference = preference;
        SavePreference(preference);
        ApplyTheme();
    }

    private void UiSettings_ColorValuesChanged(UISettings sender, object args)
    {
        if (Preference != AppThemePreference.Auto)
        {
            return;
        }

        var dispatcher = _window?.DispatcherQueue;
        if (dispatcher is null)
        {
            ApplyTheme();
            return;
        }

        dispatcher.TryEnqueue(ApplyTheme);
    }

    private void ApplyTheme()
    {
        var requested = Preference switch
        {
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        if (_window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = requested;
            EffectiveTheme = root.ActualTheme == ElementTheme.Default
                ? requested
                : root.ActualTheme;
        }
        else
        {
            EffectiveTheme = requested == ElementTheme.Default
                ? GetSystemTheme()
                : requested;
        }

        if (EffectiveTheme == ElementTheme.Default)
        {
            EffectiveTheme = GetSystemTheme();
        }

        StartupLog.Write($"ApplyTheme finished. Preference={Preference}, EffectiveTheme={EffectiveTheme}.");
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private ElementTheme GetSystemTheme()
    {
        try
        {
            if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
            {
                return ElementTheme.Dark;
            }

            if (Application.Current.RequestedTheme == ApplicationTheme.Light)
            {
                return ElementTheme.Light;
            }
        }
        catch
        {
            // Application may not be ready during very early startup.
        }

        var background = _uiSettings.GetColorValue(UIColorType.Background);
        var luminance = (0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B);
        return luminance < 128 ? ElementTheme.Dark : ElementTheme.Light;
    }

    private static AppThemePreference LoadPreference()
    {
        try
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(ThemeSettingKey, out var stored)
                && stored is string text
                && Enum.TryParse<AppThemePreference>(text, out var preference))
            {
                return preference;
            }
        }
        catch
        {
            // Unpackaged local settings may be unavailable during early startup.
        }

        return AppThemePreference.Auto;
    }

    private static void SavePreference(AppThemePreference preference)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] = preference.ToString();
        }
        catch
        {
            // Ignore persistence failures; in-memory preference still applies.
        }
    }
}
