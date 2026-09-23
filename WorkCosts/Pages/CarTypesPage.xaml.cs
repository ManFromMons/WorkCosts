using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WorkCosts.Data;
using WorkCosts.Helpers;
using WorkCosts.Models;

namespace WorkCosts.Pages;

public sealed partial class CarTypesPage : Page, IUnsavedChangesSource
{
    private readonly List<TypeRow> _rows = [];
    private CarDetails? _loaded;
    private bool _addOpen;
    private bool _compactDetail;
    private bool _suppressSelection;
    private bool _suppressFields;
    private bool _busy;
    private Guid? _selectedId;

    public CarTypesPage()
    {
        InitializeComponent();
    }

    public bool HasUnsavedChanges => (_addOpen && IsAddDirty()) || IsDetailDirty();

    public Task FlushPendingAsync() => Task.CompletedTask;

    public async Task<bool> SaveUnsavedAsync()
    {
        if (_addOpen && IsAddDirty() && !await SaveAddAsync())
        {
            return false;
        }

        if (IsDetailDirty() && !await SaveDetailAsync())
        {
            return false;
        }

        return true;
    }

    public Task DiscardUnsavedAsync()
    {
        CloseAdd();
        if (_loaded is not null)
        {
            BindDetail(_loaded);
        }

        return Task.CompletedTask;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLayout();
        try
        {
            await LoadAsync(null);
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Car types", ex.Message);
        }
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayout();

    private async void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (DialogHelper.HasOpenDialog)
        {
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape && _addOpen)
        {
            e.Handled = true;
            await DismissAddAsync();
            return;
        }

        if (e.Key != Windows.System.VirtualKey.Enter || e.OriginalSource is not TextBox)
        {
            return;
        }

        if (_addOpen && AddSaveButton.IsEnabled)
        {
            e.Handled = true;
            await SaveAddAsync();
            return;
        }

        if (!_addOpen && DetailSaveButton.IsEnabled)
        {
            e.Handled = true;
            await SaveDetailAsync();
        }
    }

    private async Task LoadAsync(Guid? selectId)
    {
        await using var db = App.Database.CreateContext();
        var types = await CarDetailsCommands.ListAsync(db);
        _rows.Clear();
        _rows.AddRange(types.Select(type => new TypeRow
        {
            Id = type.Id,
            Title = $"{type.Make} {type.Model}",
            Detail = $"{type.Make} · {type.ModelNumber} · {FormatYears(type.Year, type.EndYear)}",
        }));

        _suppressSelection = true;
        TypesList.ItemsSource = null;
        TypesList.ItemsSource = _rows;
        EmptyListText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        TypeRow? target = null;
        var targetId = selectId ?? _selectedId;
        if (targetId is Guid id)
        {
            target = _rows.FirstOrDefault(row => row.Id == id);
        }

        TypesList.SelectedItem = target;
        _suppressSelection = false;
        if (target is null)
        {
            ShowEmpty();
        }
        else
        {
            await ShowDetailAsync(target.Id);
            if (selectId is not null && IsCompact)
            {
                _compactDetail = true;
            }
        }

        ApplyLayout();
    }

    private async void TypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        var incoming = TypesList.SelectedItem as TypeRow;
        if (_selectedId is Guid current && incoming?.Id != current)
        {
            RestoreSelection(current);
            if (!await UnsavedChangesLeave.TryLeaveAsync(this, XamlRoot, UnsavedPrompt.UserLeaveTimeout))
            {
                return;
            }

            _suppressSelection = true;
            TypesList.SelectedItem = incoming;
            _suppressSelection = false;
        }

        if (incoming is null)
        {
            ShowEmpty();
            return;
        }

        await ShowDetailAsync(incoming.Id);
        if (IsCompact)
        {
            _compactDetail = true;
            ApplyLayout();
        }
    }

    private void RestoreSelection(Guid typeId)
    {
        _suppressSelection = true;
        TypesList.SelectedItem = _rows.FirstOrDefault(row => row.Id == typeId);
        _suppressSelection = false;
    }

    private void ShowEmpty()
    {
        _selectedId = null;
        _loaded = null;
        DetailPanel.Visibility = Visibility.Collapsed;
        NoSelectionText.Visibility = Visibility.Visible;
        DetailSaveButton.IsEnabled = false;
        if (IsCompact)
        {
            _compactDetail = false;
            ApplyLayout();
        }
    }

    private async Task ShowDetailAsync(Guid typeId)
    {
        await using var db = App.Database.CreateContext();
        var type = await CarDetailsCommands.GetAsync(db, typeId);
        if (type is null)
        {
            ShowEmpty();
            return;
        }

        BindDetail(type);
    }

    private void BindDetail(CarDetails type)
    {
        _selectedId = type.Id;
        _loaded = type;
        NoSelectionText.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        DetailTitle.Text = $"{type.Make} {type.ModelNumber}";
        _suppressFields = true;
        DetailMakeBox.Text = type.Make;
        DetailModelBox.Text = type.Model;
        DetailModelNumberBox.Text = type.ModelNumber;
        DetailYearBox.Text = type.Year.ToString(CultureInfo.InvariantCulture);
        DetailEndYearBox.Text = type.EndYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _suppressFields = false;
        SetStatus(DetailStatus, null);
        UpdateDetailSave();
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_addOpen)
        {
            return;
        }

        if (IsDetailDirty() && !await UnsavedChangesLeave.TryLeaveAsync(this, XamlRoot, UnsavedPrompt.UserLeaveTimeout))
        {
            return;
        }

        ClearAdd();
        _addOpen = true;
        AddOverlay.Visibility = Visibility.Visible;
    }

    private async void AddCancel_Click(object sender, RoutedEventArgs e) => await DismissAddAsync();

    private async void AddSave_Click(object sender, RoutedEventArgs e) => await SaveAddAsync();

    private async void DetailSave_Click(object sender, RoutedEventArgs e) => await SaveDetailAsync();

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_loaded is null)
        {
            return;
        }

        var yes = await DialogHelper.ConfirmYesNoAsync(
            XamlRoot,
            "Delete car type",
            "Delete this type? It cannot be removed if a car, job, garage job, or completion uses it.");
        if (!yes)
        {
            return;
        }

        await using var db = App.Database.CreateContext();
        var result = await CarDetailsCommands.TryDeleteAsync(db, _loaded.Id);
        if (result == CarDetailsDeleteResult.InUse)
        {
            await DialogHelper.ShowMessageAsync(
                XamlRoot,
                "Delete car type",
                "This type is in use. Remove it from cars, jobs, garage jobs, and completions first.");
            return;
        }

        if (result == CarDetailsDeleteResult.NotFound)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Delete car type", "This car type no longer exists.");
        }

        _loaded = null;
        _selectedId = null;
        _compactDetail = false;
        await LoadAsync(null);
    }

    private async void DetailBack_Click(object sender, RoutedEventArgs e)
    {
        if (IsDetailDirty() && !await UnsavedChangesLeave.TryLeaveAsync(this, XamlRoot, UnsavedPrompt.UserLeaveTimeout))
        {
            return;
        }

        _compactDetail = false;
        ApplyLayout();
    }

    private void DetailField_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressFields)
        {
            return;
        }

        UpdateDetailSave();
    }

    private void AddField_Changed(object sender, TextChangedEventArgs e) => UpdateAddSave();

    private async Task<bool> SaveDetailAsync()
    {
        if (_loaded is null || _busy)
        {
            return false;
        }

        if (!TryRead(DetailMakeBox, DetailModelBox, DetailModelNumberBox, DetailYearBox, DetailEndYearBox, out var input, out var error))
        {
            SetStatus(DetailStatus, error);
            return false;
        }

        SetBusy(true);
        try
        {
            await using var db = App.Database.CreateContext();
            var result = await CarDetailsCommands.UpdateAsync(db, _loaded.Id, input);
            if (!result.Saved)
            {
                SetStatus(DetailStatus, result.Detail);
                return false;
            }

            await LoadAsync(result.Type!.Id);
            return true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> SaveAddAsync()
    {
        if (_busy)
        {
            return false;
        }

        if (!TryRead(AddMakeBox, AddModelBox, AddModelNumberBox, AddYearBox, AddEndYearBox, out var input, out var error))
        {
            SetStatus(AddStatus, error);
            return false;
        }

        SetBusy(true);
        try
        {
            await using var db = App.Database.CreateContext();
            var result = await CarDetailsCommands.CreateAsync(db, input);
            if (!result.Saved)
            {
                SetStatus(AddStatus, result.Detail);
                return false;
            }

            CloseAdd();
            await LoadAsync(result.Type!.Id);
            if (IsCompact)
            {
                _compactDetail = true;
                ApplyLayout();
            }

            return true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DismissAddAsync()
    {
        if (!IsAddDirty())
        {
            CloseAdd();
            return;
        }

        var choice = await DialogHelper.ConfirmUnsavedWithTimeoutAsync(XamlRoot, UnsavedPrompt.UserLeaveTimeout);
        switch (choice.Result)
        {
            case UnsavedPromptResult.Discard:
                CloseAdd();
                break;
            case UnsavedPromptResult.Save:
                if (!await SaveAddAsync() && !choice.TimedOut)
                {
                    return;
                }

                if (choice.TimedOut)
                {
                    CloseAdd();
                }

                break;
        }
    }

    private void CloseAdd()
    {
        _addOpen = false;
        AddOverlay.Visibility = Visibility.Collapsed;
        ClearAdd();
    }

    private void ClearAdd()
    {
        AddMakeBox.Text = string.Empty;
        AddModelBox.Text = string.Empty;
        AddModelNumberBox.Text = string.Empty;
        AddYearBox.Text = string.Empty;
        AddEndYearBox.Text = string.Empty;
        SetStatus(AddStatus, null);
        UpdateAddSave();
    }

    private bool IsAddDirty() =>
        HasText(AddMakeBox)
        || HasText(AddModelBox)
        || HasText(AddModelNumberBox)
        || HasText(AddYearBox)
        || HasText(AddEndYearBox);

    private bool IsDetailDirty()
    {
        if (_loaded is null || DetailPanel.Visibility != Visibility.Visible)
        {
            return false;
        }

        return DetailMakeBox.Text.Trim() != _loaded.Make
            || DetailModelBox.Text.Trim() != _loaded.Model
            || DetailModelNumberBox.Text.Trim() != _loaded.ModelNumber
            || DetailYearBox.Text.Trim() != _loaded.Year.ToString(CultureInfo.InvariantCulture)
            || DetailEndYearBox.Text.Trim() != (_loaded.EndYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private void UpdateDetailSave() =>
        DetailSaveButton.IsEnabled = !_busy && _loaded is not null
            && TryRead(DetailMakeBox, DetailModelBox, DetailModelNumberBox, DetailYearBox, DetailEndYearBox, out _, out _);

    private void UpdateAddSave() =>
        AddSaveButton.IsEnabled = !_busy
            && TryRead(AddMakeBox, AddModelBox, AddModelNumberBox, AddYearBox, AddEndYearBox, out _, out _);

    private static bool TryRead(
        TextBox make,
        TextBox model,
        TextBox modelNumber,
        TextBox yearBox,
        TextBox endYearBox,
        out CarDetailsInput input,
        out string? error)
    {
        input = new CarDetailsInput(make.Text, model.Text, modelNumber.Text, 0);
        if (string.IsNullOrWhiteSpace(make.Text)
            || string.IsNullOrWhiteSpace(model.Text)
            || string.IsNullOrWhiteSpace(modelNumber.Text))
        {
            error = "Fill in make, model, model number, and year.";
            return false;
        }

        if (!int.TryParse(yearBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            error = "Enter a model year.";
            return false;
        }

        int? endYear = null;
        if (!string.IsNullOrWhiteSpace(endYearBox.Text)
            && !int.TryParse(endYearBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedEnd))
        {
            error = "Enter an end year, or leave it empty.";
            return false;
        }
        else if (!string.IsNullOrWhiteSpace(endYearBox.Text))
        {
            endYear = int.Parse(endYearBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        input = input with { Year = year, EndYear = endYear };
        if (year < CarDetailsCommands.MinModelYear || year > CarDetailsCommands.MaxModelYear(DateTimeOffset.Now))
        {
            error = $"Model year must be from {CarDetailsCommands.MinModelYear} to {CarDetailsCommands.MaxModelYear(DateTimeOffset.Now)}.";
            return false;
        }

        if (endYear is int end
            && (end < CarDetailsCommands.MinModelYear || end > CarDetailsCommands.MaxModelYear(DateTimeOffset.Now) || end < year))
        {
            error = "End year must be on or after the start year and in range.";
            return false;
        }

        error = null;
        return true;
    }

    private static string FormatYears(int year, int? endYear) =>
        endYear is int end ? $"{year}–{end}" : $"{year}–";

    private void SetBusy(bool busy)
    {
        _busy = busy;
        PageAddButton.IsEnabled = !busy;
        UpdateDetailSave();
        UpdateAddSave();
    }

    private static void SetStatus(TextBlock block, string? message)
    {
        block.Text = message ?? string.Empty;
        block.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static bool HasText(TextBox box) => !string.IsNullOrWhiteSpace(box.Text);

    private bool IsCompact => Root.ActualWidth > 0 && Root.ActualWidth < 720;

    private void ApplyLayout()
    {
        var compact = IsCompact;
        if (!compact)
        {
            ListColumn.Width = new GridLength(2, GridUnitType.Star);
            DetailColumn.Width = new GridLength(3, GridUnitType.Star);
            Grid.SetColumn(ListPane, 0);
            Grid.SetColumn(DetailPane, 1);
            ListPane.Visibility = Visibility.Visible;
            DetailPane.Visibility = Visibility.Visible;
            DetailBackButton.Visibility = Visibility.Collapsed;
            return;
        }

        ListColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailColumn.Width = new GridLength(0);
        Grid.SetColumn(ListPane, 0);
        Grid.SetColumn(DetailPane, 0);
        ListPane.Visibility = _compactDetail ? Visibility.Collapsed : Visibility.Visible;
        DetailPane.Visibility = _compactDetail ? Visibility.Visible : Visibility.Collapsed;
        DetailBackButton.Visibility = Visibility.Visible;
    }

    private sealed class TypeRow
    {
        public Guid Id { get; init; }
        public required string Title { get; init; }
        public required string Detail { get; init; }
    }
}
