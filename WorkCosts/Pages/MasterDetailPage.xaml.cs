using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkCosts.Helpers;
using WorkCosts.Models;

namespace WorkCosts.Pages;

public sealed partial class MasterDetailPage : Page
{
    private readonly ObservableCollection<JobListItem> _jobs = [];
    public ObservableCollection<JobProductRow> JobProductRows { get; } = new();
    private Guid? _selectedId;
    private bool _suppressSelection;
    private bool _suppressFieldEvents;
    private bool _suppressProductEvents;
    private int _persistVersion;

    public MasterDetailPage()
    {
        try
        {
            InitializeComponent();
            JobsList.ItemsSource = _jobs;
            JobProductsList.ItemsSource = JobProductRows;
            NotesEditor.TextChanged += (_, _) => UpdateSaveButtonState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MasterDetailPage ctor failed: {ex}");
            throw;
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MasterDetailPage.OnNavigatedTo failed: {ex}");
            await DialogHelper.ShowMessageAsync(
                XamlRoot,
                "Jobs page failed",
                FormatPageException(ex));
        }
    }

    private static string FormatPageException(Exception ex)
    {
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

    private async Task LoadAsync(Guid? selectId = null)
    {
        await using var db = App.Database.CreateContext();
        var jobs = await db.Jobs.OrderBy(j => j.Name).ToListAsync();

        _suppressSelection = true;
        _jobs.Clear();
        foreach (var job in jobs)
        {
            _jobs.Add(new JobListItem(job));
        }

        EmptyListText.Visibility = _jobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var targetId = selectId ?? _selectedId;
        JobListItem? target = null;
        if (targetId is Guid id)
        {
            target = _jobs.FirstOrDefault(j => j.Id == id);
        }

        target ??= _jobs.FirstOrDefault();
        JobsList.SelectedItem = target;
        _suppressSelection = false;

        if (target is null)
        {
            ShowEmptyDetail();
        }
        else
        {
            ShowDetail(target);
        }
    }

    private JobListItem? SelectedItem =>
        _selectedId is Guid id ? _jobs.FirstOrDefault(j => j.Id == id) : null;

    private void JobsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        if (JobsList.SelectedItem is JobListItem item)
        {
            ShowDetail(item);
        }
        else
        {
            ShowEmptyDetail();
        }
    }

    private void ShowEmptyDetail()
    {
        _selectedId = null;
        DetailPanel.Visibility = Visibility.Collapsed;
        NoSelectionText.Visibility = Visibility.Visible;
        DetailTitle.Text = "Job details";
        JobProductRows.Clear();
        SaveButton.IsEnabled = false;
    }

    private void ShowDetail(JobListItem item)
    {
        _selectedId = item.Id;
        NoSelectionText.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        UpdateDetailTitle(item.Name);

        _suppressFieldEvents = true;
        NameBox.Text = item.Name;
        PriceBox.Value = (double)item.GaragePrice;
        DurationBox.Text = DurationHelper.ToDisplay(item.DurationMinutes);
        NotesEditor.Text = item.NotesMarkdown;
        _suppressFieldEvents = false;

        UpdateSaveButtonState();
        _ = LoadJobProductsAsync(item.Id);
    }

    private async Task LoadJobProductsAsync(Guid jobId)
    {
        await using var db = App.Database.CreateContext();
        var products = await db.Products
            .Include(p => p.Category)
            .Include(p => p.ProductJobs)
            .OrderBy(p => p.Name)
            .ToListAsync();

        _suppressProductEvents = true;
        JobProductRows.Clear();
        foreach (var product in products)
        {
            var isAssigned = product.IsAllJobs ||
                product.ProductJobs.Any(pj => pj.JobId == jobId);
            JobProductRows.Add(new JobProductRow
            {
                ProductId = product.Id,
                Name = product.Name,
                Detail = product.IsAllJobs
                    ? "All jobs"
                    : product.Category?.Name ?? "",
                IsAllJobs = product.IsAllJobs,
                IsAssigned = isAssigned
            });
        }

        _suppressProductEvents = false;
    }

    private async void JobProductAssignment_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressProductEvents || _selectedId is not Guid jobId)
        {
            return;
        }

        if (sender is not CheckBox { DataContext: JobProductRow row } cb || row.IsAllJobs)
        {
            return;
        }

        var assigned = cb.IsChecked == true;
        if (assigned == row.IsAssigned)
        {
            return;
        }

        try
        {
            await using var db = App.Database.CreateContext();
            var entity = await db.Products
                .Include(p => p.ProductJobs)
                .FirstOrDefaultAsync(p => p.Id == row.ProductId);
            if (entity is null)
            {
                await DialogHelper.ShowMessageAsync(XamlRoot, "Not found", "This product no longer exists.");
                await LoadJobProductsAsync(jobId);
                return;
            }

            var link = entity.ProductJobs.FirstOrDefault(pj => pj.JobId == jobId);
            if (assigned)
            {
                if (link is null)
                {
                    db.ProductJobs.Add(new ProductJob
                    {
                        ProductId = entity.Id,
                        JobId = jobId
                    });
                }
            }
            else if (link is not null)
            {
                db.ProductJobs.Remove(link);
            }

            await db.SaveChangesAsync();
            row.IsAssigned = assigned;
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Update failed", ex.Message);
            _suppressProductEvents = true;
            row.IsAssigned = !assigned;
            cb.IsChecked = row.IsAssigned;
            _suppressProductEvents = false;
        }
    }

    private void UpdateDetailTitle(string name)
    {
        var trimmed = name.Trim();
        DetailTitle.Text = string.IsNullOrEmpty(trimmed)
            ? "Job details"
            : $"Job details - {trimmed}";
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFieldEvents || SelectedItem is not JobListItem item)
        {
            return;
        }

        var name = NameBox.Text.Trim();
        UpdateDetailTitle(name);
        if (string.IsNullOrWhiteSpace(name) || name == item.Name)
        {
            return;
        }

        item.Name = name;
        _ = PersistCoreFieldsAsync(item);
        UpdateSaveButtonState();
    }

    private void PriceBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressFieldEvents || SelectedItem is not JobListItem item)
        {
            return;
        }

        if (double.IsNaN(PriceBox.Value) || PriceBox.Value < 0)
        {
            return;
        }

        var price = (decimal)PriceBox.Value;
        if (price == item.GaragePrice)
        {
            return;
        }

        item.GaragePrice = price;
        _ = PersistCoreFieldsAsync(item);
        UpdateSaveButtonState();
    }

    private void DurationBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSaveButtonState();
        TryPersistDuration(showValidation: false);
    }

    private void DurationBox_LostFocus(object sender, RoutedEventArgs e)
    {
        TryPersistDuration(showValidation: true);
    }

    private void TryPersistDuration(bool showValidation)
    {
        if (_suppressFieldEvents || SelectedItem is not JobListItem item)
        {
            return;
        }

        if (!DurationHelper.TryParse(DurationBox.Text, out var minutes))
        {
            if (showValidation)
            {
                _ = DialogHelper.ShowMessageAsync(
                    XamlRoot,
                    "Validation",
                    "Duration must be hh:mm (minutes 0–59).");
            }

            return;
        }

        if (minutes == item.DurationMinutes)
        {
            return;
        }

        item.DurationMinutes = minutes;
        _ = PersistCoreFieldsAsync(item);
        UpdateSaveButtonState();
    }

    private void UpdateSaveButtonState()
    {
        SaveButton.IsEnabled = IsDirty();
    }

    private bool IsDirty()
    {
        if (SelectedItem is not JobListItem item)
        {
            return false;
        }

        if (NameBox.Text.Trim() != item.Name)
        {
            return true;
        }

        if (!double.IsNaN(PriceBox.Value) && (decimal)PriceBox.Value != item.GaragePrice)
        {
            return true;
        }

        if (DurationHelper.TryParse(DurationBox.Text, out var minutes))
        {
            if (minutes != item.DurationMinutes)
            {
                return true;
            }
        }
        else if (DurationBox.Text.Trim() != DurationHelper.ToDisplay(item.DurationMinutes))
        {
            return true;
        }

        return (NotesEditor.Text ?? string.Empty) != item.NotesMarkdown;
    }

    private async Task PersistCoreFieldsAsync(JobListItem item)
    {
        var version = ++_persistVersion;
        try
        {
            await using var db = App.Database.CreateContext();
            var entity = await db.Jobs.FindAsync(item.Id);
            if (entity is null)
            {
                if (version == _persistVersion)
                {
                    await DialogHelper.ShowMessageAsync(XamlRoot, "Not found", "This job no longer exists.");
                    await LoadAsync();
                }

                return;
            }

            // Apply the latest values from the view-model (may have changed while awaiting).
            entity.Name = item.Name;
            entity.GaragePrice = item.GaragePrice;
            entity.DurationMinutes = item.DurationMinutes;
            await db.SaveChangesAsync();
            UpdateSaveButtonState();
        }
        catch (Exception ex)
        {
            if (version == _persistVersion)
            {
                await DialogHelper.ShowMessageAsync(XamlRoot, "Save failed", ex.Message);
            }
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        await using var db = App.Database.CreateContext();
        var job = new Job
        {
            Name = "New job",
            GaragePrice = 0,
            DurationMinutes = 60,
            NotesMarkdown = string.Empty
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        await LoadAsync(job.Id);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId is not Guid id || SelectedItem is not JobListItem item)
        {
            return;
        }

        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Validation", "Name is required.");
            return;
        }

        if (!DurationHelper.TryParse(DurationBox.Text, out var minutes))
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Validation", "Duration must be hh:mm (minutes 0–59).");
            return;
        }

        if (double.IsNaN(PriceBox.Value) || PriceBox.Value < 0)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Validation", "Garage price must be zero or greater.");
            return;
        }

        await using var db = App.Database.CreateContext();
        var entity = await db.Jobs.FindAsync(id);
        if (entity is null)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Not found", "This job no longer exists.");
            await LoadAsync();
            return;
        }

        entity.Name = name;
        entity.GaragePrice = (decimal)PriceBox.Value;
        entity.DurationMinutes = minutes;
        entity.NotesMarkdown = NotesEditor.Text ?? string.Empty;
        await db.SaveChangesAsync();

        item.Name = name;
        item.GaragePrice = entity.GaragePrice;
        item.DurationMinutes = minutes;
        item.NotesMarkdown = entity.NotesMarkdown;
        UpdateDetailTitle(name);
        UpdateSaveButtonState();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId is not Guid id)
        {
            return;
        }

        await using var db = App.Database.CreateContext();
        if (await db.WorkJobs.AnyAsync(w => w.JobId == id))
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Cannot delete",
                "This job is used by one or more Work Jobs.");
            return;
        }

        var entity = await db.Jobs.FindAsync(id);
        if (entity is null)
        {
            await LoadAsync();
            return;
        }

        if (!await DialogHelper.ConfirmAsync(XamlRoot, "Delete job", $"Delete '{entity.Name}'?"))
        {
            return;
        }

        db.Jobs.Remove(entity);
        await db.SaveChangesAsync();
        _selectedId = null;
        await LoadAsync();
    }

    private sealed class JobListItem : INotifyPropertyChanged
    {
        private string _name;
        private decimal _garagePrice;
        private int _durationMinutes;
        private string _notesMarkdown;
        private string _summary;

        public JobListItem(Job job)
        {
            Id = job.Id;
            _name = job.Name;
            _garagePrice = job.GaragePrice;
            _durationMinutes = job.DurationMinutes;
            _notesMarkdown = job.NotesMarkdown;
            _summary = BuildSummary(_garagePrice, _durationMinutes);
        }

        public Guid Id { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                OnPropertyChanged();
            }
        }

        public decimal GaragePrice
        {
            get => _garagePrice;
            set
            {
                if (_garagePrice == value)
                {
                    return;
                }

                _garagePrice = value;
                OnPropertyChanged();
                RefreshSummary();
            }
        }

        public int DurationMinutes
        {
            get => _durationMinutes;
            set
            {
                if (_durationMinutes == value)
                {
                    return;
                }

                _durationMinutes = value;
                OnPropertyChanged();
                RefreshSummary();
            }
        }

        public string NotesMarkdown
        {
            get => _notesMarkdown;
            set
            {
                if (_notesMarkdown == value)
                {
                    return;
                }

                _notesMarkdown = value;
                OnPropertyChanged();
            }
        }

        public string Summary
        {
            get => _summary;
            private set
            {
                if (_summary == value)
                {
                    return;
                }

                _summary = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RefreshSummary() =>
            Summary = BuildSummary(_garagePrice, _durationMinutes);

        private static string BuildSummary(decimal price, int minutes) =>
            $"{price.ToString("C", CultureInfo.CurrentCulture)} · {DurationHelper.ToDisplay(minutes)} hrs";

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class JobProductRow : INotifyPropertyChanged
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool IsAllJobs { get; set; }
    public bool CanToggle => !IsAllJobs;

    private bool _isAssigned;
    public bool IsAssigned
    {
        get => _isAssigned;
        set
        {
            if (_isAssigned == value)
            {
                return;
            }

            _isAssigned = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
