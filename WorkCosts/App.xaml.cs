using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkCosts.Helpers;
using WorkCosts.Services;

namespace WorkCosts;

public partial class App : Application
{
    private Window? _window;

    public static DatabaseService Database { get; } = new();

    public static Window? MainAppWindow { get; private set; }

    public App()
    {
        UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            StartupLog.Write("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            StartupLog.Write(
                "AppDomain.UnhandledException",
                e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown"));
        };

        StartupLog.Write($"App ctor starting. Log: {StartupLog.Path}");
        try
        {
            InitializeComponent();
            StartupLog.Write("App.InitializeComponent succeeded.");
        }
        catch (Exception ex)
        {
            StartupLog.Write("App.InitializeComponent failed (likely App.xaml resource names).", ex);
            throw;
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupLog.Write($"Application.UnhandledException (Handled was {e.Handled})", e.Exception);
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupLog.Write("OnLaunched: creating MainWindow.");
        try
        {
            _window = new MainWindow();
            MainAppWindow = _window;
            StartupLog.Write("OnLaunched: MainWindow created, activating.");
            _window.Activate();
            StartupLog.Write("OnLaunched: Activate() returned.");
        }
        catch (Exception ex)
        {
            StartupLog.Write("OnLaunched: MainWindow create/activate failed.", ex);
            throw;
        }

        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            StartupLog.Write("StartAsync: Database.InitializeAsync.");
            // Finish migrate/seed before any page opens a connection.
            await Database.InitializeAsync();
            StartupLog.Write("StartAsync: database ready, navigating home.");
            if (_window is MainWindow mainWindow)
            {
                mainWindow.NavigateToHome();
            }

            StartupLog.Write("StartAsync: home navigation requested.");
        }
        catch (Exception ex)
        {
            StartupLog.Write("StartAsync failed.", ex);
            if (_window?.Content?.XamlRoot is { } root)
            {
                var dialog = new ContentDialog
                {
                    Title = "Database startup failed",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = root
                };
                await dialog.ShowAsync();
            }
        }
    }
}
