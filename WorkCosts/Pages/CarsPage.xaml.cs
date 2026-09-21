using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using WorkCosts.Data;
using WorkCosts.Helpers;
using WorkCosts.Models;
using WorkCosts.Services;

namespace WorkCosts.Pages;

public sealed partial class CarsPage : Page, IUnsavedChangesSource
{
    private readonly List<CarRow> _rows = [];
    private readonly List<TypePick> _typePicks = [];
    private Car? _loadedCar;
    private byte[]? _detailReplacement;
    private string? _detailReplacementType;
    private byte[]? _addImage;
    private string? _addImageType;
    private bool _addOpen;
    private bool _compactDetail;
    private bool _suppressSelection;
    private bool _suppressFields;
    private bool _busy;
    private Guid? _selectedId;

    public CarsPage()
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
        if (_loadedCar is not null)
        {
            BindDetail(_loadedCar);
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
            await DialogHelper.ShowMessageAsync(XamlRoot, "Cars", ex.Message);
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
        await LoadTypePicksAsync(db);
        var cars = await CarCommands.ListActiveAsync(db);
        var rows = new List<CarRow>();
        foreach (var car in cars)
        {
            rows.Add(new CarRow
            {
                Id = car.Id,
                Name = car.Name,
                Detail = $"{car.Make} · {car.ModelNumber} · {car.Year} · {car.Vrm}",
                Thumbnail = await LoadBitmapAsync(car.ImageRelativePath),
            });
        }

        _rows.Clear();
        _rows.AddRange(rows);
        _suppressSelection = true;
        CarsList.ItemsSource = null;
        CarsList.ItemsSource = _rows;
        EmptyListText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        CarRow? target = null;
        var targetId = selectId ?? _selectedId;
        if (targetId is Guid id)
        {
            target = _rows.FirstOrDefault(row => row.Id == id);
        }

        CarsList.SelectedItem = target;
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

    private async void CarsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        var incoming = CarsList.SelectedItem as CarRow;
        if (_selectedId is Guid current && incoming?.Id != current)
        {
            RestoreSelection(current);
            if (!await UnsavedChangesLeave.TryLeaveAsync(this, XamlRoot, UnsavedPrompt.UserLeaveTimeout))
            {
                return;
            }

            _suppressSelection = true;
            CarsList.SelectedItem = incoming;
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

    private void RestoreSelection(Guid carId)
    {
        _suppressSelection = true;
        CarsList.SelectedItem = _rows.FirstOrDefault(row => row.Id == carId);
        _suppressSelection = false;
    }

    private void ShowEmpty()
    {
        _selectedId = null;
        _loadedCar = null;
        _detailReplacement = null;
        _detailReplacementType = null;
        DetailPanel.Visibility = Visibility.Collapsed;
        NoSelectionText.Visibility = Visibility.Visible;
        DetailSaveButton.IsEnabled = false;
        if (IsCompact)
        {
            _compactDetail = false;
            ApplyLayout();
        }
    }

    private async Task ShowDetailAsync(Guid carId)
    {
        await using var db = App.Database.CreateContext();
        var car = await CarCommands.GetAsync(db, carId);
        if (car is null || car.DeletedAt is not null)
        {
            ShowEmpty();
            return;
        }

        BindDetail(car);
    }

    private void BindDetail(Car car)
    {
        _selectedId = car.Id;
        _loadedCar = car;
        _detailReplacement = null;
        _detailReplacementType = null;
        NoSelectionText.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        DetailTitle.Text = car.Name;
        _suppressFields = true;
        DetailNameBox.Text = car.Name;
        DetailMakeBox.Text = car.Make;
        DetailModelBox.Text = car.Model;
        DetailModelNumberBox.Text = car.ModelNumber;
        DetailEngineBox.Text = car.EngineType;
        DetailVrmBox.Text = car.Vrm;
        DetailYearBox.Text = car.Year.ToString(CultureInfo.InvariantCulture);
        DetailVinBox.Text = car.Vin;
        SelectType(DetailTypeBox, car.CarDetailsId);
        _suppressFields = false;
        SetStatus(DetailStatus, null);
        UpdateDetailSave();
        _ = ShowPreviewAsync(DetailPreview, car.ImageRelativePath);
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
        if (_loadedCar is null)
        {
            return;
        }

        var yes = await DialogHelper.ConfirmYesNoAsync(
            XamlRoot,
            "Delete car",
            "This hides the car. Linked work stays, and the photo is kept.");
        if (!yes)
        {
            return;
        }

        await using var db = App.Database.CreateContext();
        var result = await CarCommands.TryDeleteAsync(db, _loadedCar.Id);
        if (result == CarDeleteResult.NotFound)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Delete car", "This car no longer exists.");
        }

        _loadedCar = null;
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

    private async void DetailSearch_Click(object sender, RoutedEventArgs e) =>
        await SearchIntoAsync(DetailMakeBox.Text, DetailModelNumberBox.Text, DetailStatus, applyToAdd: false);

    private async void AddSearch_Click(object sender, RoutedEventArgs e) =>
        await SearchIntoAsync(AddMakeBox.Text, AddModelNumberBox.Text, AddStatus, applyToAdd: true);

    private async void DetailPick_Click(object sender, RoutedEventArgs e) =>
        await PickFileAsync(applyToAdd: false, DetailStatus);

    private async void AddPick_Click(object sender, RoutedEventArgs e) =>
        await PickFileAsync(applyToAdd: true, AddStatus);

    private void DetailType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFields)
        {
            return;
        }

        UpdateDetailSave();
    }

    private void AddType_Changed(object sender, SelectionChangedEventArgs e) => UpdateAddSave();

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
        if (_loadedCar is null || _busy)
        {
            return false;
        }

        if (!TryRead(DetailNameBox, DetailMakeBox, DetailModelBox, DetailModelNumberBox, DetailEngineBox, DetailVrmBox, DetailYearBox, DetailVinBox, _loadedCar.VehicleOrderJson, SelectedTypeId(DetailTypeBox), out var input, out var error))
        {
            SetStatus(DetailStatus, error);
            return false;
        }

        SetBusy(true);
        try
        {
            await using var db = App.Database.CreateContext();
            CarWriteResult result;
            if (_detailReplacement is not null)
            {
                await using var image = new MemoryStream(_detailReplacement);
                result = await CarCommands.UpdateAsync(
                    db,
                    DataRoot,
                    _loadedCar.Id,
                    input,
                    image,
                    _detailReplacementType);
            }
            else
            {
                result = await CarCommands.UpdateAsync(
                    db,
                    DataRoot,
                    _loadedCar.Id,
                    input,
                    replacementImage: null,
                    replacementContentType: null);
            }

            if (!result.Saved)
            {
                SetStatus(DetailStatus, result.Detail);
                return false;
            }

            await LoadAsync(result.Car!.Id);
            return true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> SaveAddAsync()
    {
        if (_busy || _addImage is null || _addImageType is null)
        {
            SetStatus(AddStatus, "Choose a photo before saving.");
            return false;
        }

        if (!TryRead(AddNameBox, AddMakeBox, AddModelBox, AddModelNumberBox, AddEngineBox, AddVrmBox, AddYearBox, AddVinBox, string.Empty, SelectedTypeId(AddTypeBox), out var input, out var error))
        {
            SetStatus(AddStatus, error);
            return false;
        }

        SetBusy(true);
        try
        {
            await using var db = App.Database.CreateContext();
            await using var image = new MemoryStream(_addImage);
            var result = await CarCommands.CreateAsync(db, DataRoot, input, image, _addImageType);
            if (!result.Saved)
            {
                SetStatus(AddStatus, result.Detail);
                return false;
            }

            CloseAdd();
            await LoadAsync(result.Car!.Id);
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

    private async Task SearchIntoAsync(string make, string modelNumber, TextBlock status, bool applyToAdd)
    {
        if (string.IsNullOrWhiteSpace(make) || string.IsNullOrWhiteSpace(modelNumber))
        {
            SetStatus(status, "Enter make and model number to search.");
            return;
        }

        SetBusy(true);
        try
        {
            var found = await CarImageChooser.SearchAsync(
                XamlRoot,
                make,
                modelNumber,
                message => SetStatus(status, message));
            if (found.Error is not null)
            {
                SetStatus(status, found.Error);
                return;
            }

            if (found.Chosen is null)
            {
                SetStatus(status, found.Cancelled ? null : "No usable images. Try again or choose a file.");
                return;
            }

            await ApplyImageAsync(applyToAdd, found.Chosen.Bytes, found.Chosen.ContentType, status);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PickFileAsync(bool applyToAdd, TextBlock status)
    {
        Windows.Storage.StorageFile? file;
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            BindPickerToAppWindow(picker);
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".webp");
            file = await picker.PickSingleFileAsync();
        }
        catch (Exception ex)
        {
            SetStatus(status, ex.Message);
            return;
        }

        if (file is null)
        {
            return;
        }

        var bytes = await File.ReadAllBytesAsync(file.Path);
        if (!CarImageStore.TryDescribe(bytes, out var contentType))
        {
            SetStatus(status, "Choose a PNG, JPEG, or WebP photo up to 512 KB.");
            return;
        }

        await ApplyImageAsync(applyToAdd, bytes, contentType, status);
    }

    private async Task ApplyImageAsync(bool applyToAdd, byte[] bytes, string contentType, TextBlock status)
    {
        var bitmap = await ProductImagePicker.ToBitmapAsync(bytes);
        if (applyToAdd)
        {
            _addImage = bytes;
            _addImageType = contentType;
            AddPreview.Source = bitmap;
            UpdateAddSave();
        }
        else
        {
            _detailReplacement = bytes;
            _detailReplacementType = contentType;
            DetailPreview.Source = bitmap;
            UpdateDetailSave();
        }

        SetStatus(status, null);
    }

    private void CloseAdd()
    {
        _addOpen = false;
        AddOverlay.Visibility = Visibility.Collapsed;
        ClearAdd();
    }

    private void ClearAdd()
    {
        _addImage = null;
        _addImageType = null;
        AddPreview.Source = null;
        AddNameBox.Text = string.Empty;
        AddMakeBox.Text = string.Empty;
        AddModelBox.Text = string.Empty;
        AddModelNumberBox.Text = string.Empty;
        AddEngineBox.Text = string.Empty;
        AddVrmBox.Text = string.Empty;
        AddYearBox.Text = string.Empty;
        AddVinBox.Text = string.Empty;
        SelectType(AddTypeBox, null);
        SetStatus(AddStatus, null);
        UpdateAddSave();
    }

    private bool IsAddDirty() =>
        _addImage is not null
        || HasText(AddNameBox)
        || HasText(AddMakeBox)
        || HasText(AddModelBox)
        || HasText(AddModelNumberBox)
        || HasText(AddEngineBox)
        || HasText(AddVrmBox)
        || HasText(AddYearBox)
        || HasText(AddVinBox)
        || SelectedTypeId(AddTypeBox) is not null;

    private bool IsDetailDirty()
    {
        if (_loadedCar is null || DetailPanel.Visibility != Visibility.Visible)
        {
            return false;
        }

        return _detailReplacement is not null
            || DetailNameBox.Text.Trim() != _loadedCar.Name
            || DetailMakeBox.Text.Trim() != _loadedCar.Make
            || DetailModelBox.Text.Trim() != _loadedCar.Model
            || DetailModelNumberBox.Text.Trim() != _loadedCar.ModelNumber
            || DetailEngineBox.Text.Trim() != _loadedCar.EngineType
            || DetailVrmBox.Text.Trim() != _loadedCar.Vrm
            || DetailYearBox.Text.Trim() != _loadedCar.Year.ToString(CultureInfo.InvariantCulture)
            || DetailVinBox.Text.Trim() != _loadedCar.Vin
            || SelectedTypeId(DetailTypeBox) != _loadedCar.CarDetailsId;
    }

    private void UpdateDetailSave() =>
        DetailSaveButton.IsEnabled = !_busy && _loadedCar is not null && FieldsReady(
            DetailNameBox, DetailMakeBox, DetailModelBox, DetailModelNumberBox, DetailEngineBox, DetailVrmBox, DetailYearBox, DetailVinBox,
            hasImage: _detailReplacement is not null || !string.IsNullOrWhiteSpace(_loadedCar.ImageRelativePath));

    private void UpdateAddSave() =>
        AddSaveButton.IsEnabled = !_busy && FieldsReady(
            AddNameBox, AddMakeBox, AddModelBox, AddModelNumberBox, AddEngineBox, AddVrmBox, AddYearBox, AddVinBox,
            hasImage: _addImage is not null);

    private static bool FieldsReady(
        TextBox name,
        TextBox make,
        TextBox model,
        TextBox modelNumber,
        TextBox engine,
        TextBox vrm,
        TextBox year,
        TextBox vin,
        bool hasImage) =>
        TryRead(name, make, model, modelNumber, engine, vrm, year, vin, string.Empty, null, out _, out _) && hasImage;

    private static bool TryRead(
        TextBox name,
        TextBox make,
        TextBox model,
        TextBox modelNumber,
        TextBox engine,
        TextBox vrm,
        TextBox yearBox,
        TextBox vin,
        string vehicleOrderJson,
        Guid? carDetailsId,
        out CarInput input,
        out string? error)
    {
        input = new CarInput(
            name.Text,
            make.Text,
            model.Text,
            modelNumber.Text,
            engine.Text,
            vrm.Text,
            0,
            vin.Text,
            vehicleOrderJson,
            carDetailsId);
        if (string.IsNullOrWhiteSpace(name.Text)
            || string.IsNullOrWhiteSpace(make.Text)
            || string.IsNullOrWhiteSpace(model.Text)
            || string.IsNullOrWhiteSpace(modelNumber.Text)
            || string.IsNullOrWhiteSpace(engine.Text)
            || string.IsNullOrWhiteSpace(vrm.Text)
            || string.IsNullOrWhiteSpace(vin.Text))
        {
            error = "Fill in every field.";
            return false;
        }

        if (!int.TryParse(yearBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            error = "Enter a model year.";
            return false;
        }

        input = input with { Year = year };
        if (year < CarCommands.MinModelYear || year > CarCommands.MaxModelYear(DateTimeOffset.Now))
        {
            error = $"Model year must be from {CarCommands.MinModelYear} to {CarCommands.MaxModelYear(DateTimeOffset.Now)}.";
            return false;
        }

        error = null;
        return true;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        PageAddButton.IsEnabled = !busy;
        DetailSearchButton.IsEnabled = !busy;
        AddSearchButton.IsEnabled = !busy;
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

    private async Task ShowPreviewAsync(Image image, string relativePath)
    {
        image.Source = await LoadBitmapAsync(relativePath);
    }

    private static async Task<BitmapImage?> LoadBitmapAsync(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            var bytes = await CarImageStore.ReadAsync(DataRoot, relativePath);
            return await ProductImagePicker.ToBitmapAsync(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static string DataRoot =>
        Path.GetDirectoryName(Path.GetFullPath(App.Database.DatabasePath))
        ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static void BindPickerToAppWindow(object picker)
    {
        if (App.MainAppWindow is null)
        {
            throw new InvalidOperationException("Main window is not available.");
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async Task LoadTypePicksAsync(WorkCostsDbContext db)
    {
        var types = await CarDetailsCommands.ListAsync(db);
        _typePicks.Clear();
        _typePicks.Add(new TypePick { Id = null, Label = "None" });
        foreach (var type in types)
        {
            _typePicks.Add(new TypePick
            {
                Id = type.Id,
                Label = $"{type.Make} {type.ModelNumber} · {type.Year} · {type.EngineType}",
            });
        }

        DetailTypeBox.ItemsSource = _typePicks;
        AddTypeBox.ItemsSource = _typePicks;
    }

    private void SelectType(ComboBox box, Guid? typeId)
    {
        box.SelectedItem = _typePicks.FirstOrDefault(pick => pick.Id == typeId) ?? _typePicks[0];
    }

    private static Guid? SelectedTypeId(ComboBox box) => (box.SelectedItem as TypePick)?.Id;

    private sealed class TypePick
    {
        public Guid? Id { get; init; }
        public required string Label { get; init; }
    }

    private sealed class CarRow
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Detail { get; init; }
        public BitmapImage? Thumbnail { get; init; }
    }
}
