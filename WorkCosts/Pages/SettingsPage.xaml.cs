using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkCosts.Helpers;
using WorkCosts.Services;

namespace WorkCosts.Pages;

public sealed class DomainCacheRow : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string Domain { get; init; }
    public required string DisplayName { get; init; }
    public required string PagesText { get; init; }
    public required string ImagesText { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed partial class SettingsPage : Page
{
    private bool _suppressThemeSelection;
    private List<DomainCacheRow> _domainRows = [];

    public SettingsPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        DbPathText.Text = App.Database.DatabasePath;
        RefreshPageCacheInfo();
        SyncThemeSelection();
        AppThemeService.Instance.ThemeChanged += ThemeService_ThemeChanged;
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        AppThemeService.Instance.ThemeChanged -= ThemeService_ThemeChanged;
        base.OnNavigatedFrom(e);
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e) => SyncThemeSelection();

    private void SyncThemeSelection()
    {
        _suppressThemeSelection = true;
        var tag = AppThemeService.Instance.Preference.ToString();
        ThemeRadios.SelectedItem = ThemeRadios.Items
            .OfType<RadioButton>()
            .FirstOrDefault(r => r.Tag as string == tag);
        _suppressThemeSelection = false;
    }

    private void ThemeRadios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeSelection)
        {
            return;
        }

        if (ThemeRadios.SelectedItem is RadioButton { Tag: string tag }
            && Enum.TryParse<AppThemePreference>(tag, out var preference))
        {
            AppThemeService.Instance.SetPreference(preference);
        }
    }

    private async void ClearPageCache_Click(object sender, RoutedEventArgs e)
    {
        var service = new ProductImageService();
        var selected = _domainRows.Where(row => row.IsSelected).Select(row => row.Domain).ToList();

        if (selected.Count == 0)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Page cache",
                "Select one or more sites to clear.");
            return;
        }

        var includeImages = ClearImagesToggle.IsOn;
        var names = string.Join(", ", selected.Select(DisplayDomain));
        var imageNote = includeImages
            ? "HTML pages and cached chooser images will be deleted."
            : "Only HTML pages will be deleted. Cached chooser images will be kept.";
        var confirmed = await DialogHelper.ConfirmAsync(
            XamlRoot,
            "Clear page cache",
            $"Clear cache for {names}? {imageNote} Products and photos already in the library are not affected.",
            "Clear");
        if (!confirmed)
        {
            return;
        }

        await service.ClearSelectedCacheAsync(selected, includeImages);
        RefreshPageCacheInfo();
    }

    private void RefreshPageCacheInfo()
    {
        var service = new ProductImageService();
        var info = service.GetPageCacheInfo();
        PageCachePathText.Text = info.Directory;
        PageCacheStatsText.Text = info.FileCount == 0 && info.ImageCount == 0
            ? "No cached pages or images."
            : $"{info.FileCount} page file(s) · {FormatBytes(info.TotalBytes)}  ·  {info.ImageCount} image file(s) · {FormatBytes(info.ImageBytes)}";

        _domainRows = service.GetDomainCacheSummaries()
            .Select(summary => new DomainCacheRow
            {
                Domain = summary.Domain,
                DisplayName = DisplayDomain(summary.Domain),
                PagesText = $"{summary.PageCount} page(s) · {FormatBytes(summary.PageBytes)}",
                ImagesText = $"{summary.ImageCount} image(s) · {FormatBytes(summary.ImageBytes)}"
            })
            .ToList();
        DomainCacheList.ItemsSource = _domainRows;
        DomainCacheEmptyText.Visibility = _domainRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string DisplayDomain(string domain) =>
        string.Equals(domain, WebCacheStore.LegacyDomain, StringComparison.OrdinalIgnoreCase)
            ? "Previous layout"
            : domain;

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
