using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
    private string _addVehicleOrderJson = string.Empty;
    private string _detailVehicleOrderJson = string.Empty;
    private Guid? _detailTypeId;
    private Guid? _addTypeId;
    private bool _bindingTypeBox;
    private MdecoderScalars? _addPendingScalars;
    private MdecoderScalars? _detailPendingScalars;
    private CancellationTokenSource? _addLookupCts;
    private CancellationTokenSource? _detailLookupCts;
    private bool _addLookupRunning;
    private bool _detailLookupRunning;

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
        CancelLookup(add: true);
        CancelLookup(add: false);
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

        if (e.Key != Windows.System.VirtualKey.Enter
            || e.OriginalSource is not TextBox
            || IsInsideAutoSuggestBox(e.OriginalSource))
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
        CancelLookup(add: false);
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
        _detailVehicleOrderJson = car.VehicleOrderJson;
        HideApplyBanner(add: false);
        BindTypeBox(DetailTypeBox, car.CarDetailsId);
        _suppressFields = false;
        SetStatus(DetailStatus, null);
        UpdateDetailSave();
        UpdateLookupUi(add: false);
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

    private void DetailField_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressFields)
        {
            return;
        }

        UpdateDetailSave();
        UpdateLookupUi(add: false);
    }

    private void AddField_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateAddSave();
        UpdateLookupUi(add: true);
    }

    private async void DetailLookup_Click(object sender, RoutedEventArgs e) => await RunLookupAsync(add: false);

    private async void AddLookup_Click(object sender, RoutedEventArgs e) => await RunLookupAsync(add: true);

    private void DetailLookupCancel_Click(object sender, RoutedEventArgs e) => CancelLookup(add: false);

    private void AddLookupCancel_Click(object sender, RoutedEventArgs e) => CancelLookup(add: true);

    private void DetailApplyFields_Click(object sender, RoutedEventArgs e) => ApplyPendingScalars(add: false, overwrite: true);

    private void AddApplyFields_Click(object sender, RoutedEventArgs e) => ApplyPendingScalars(add: true, overwrite: true);

    private void DetailKeepFields_Click(object sender, RoutedEventArgs e) => HideApplyBanner(add: false);

    private void AddKeepFields_Click(object sender, RoutedEventArgs e) => HideApplyBanner(add: true);

    private async Task<bool> SaveDetailAsync()
    {
        if (_loadedCar is null || _busy)
        {
            return false;
        }

        if (!TryRead(DetailNameBox, DetailMakeBox, DetailModelBox, DetailModelNumberBox, DetailEngineBox, DetailVrmBox, DetailYearBox, DetailVinBox, _detailVehicleOrderJson, SelectedTypeId(DetailTypeBox), out var input, out var error))
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

        if (!TryRead(AddNameBox, AddMakeBox, AddModelBox, AddModelNumberBox, AddEngineBox, AddVrmBox, AddYearBox, AddVinBox, _addVehicleOrderJson, SelectedTypeId(AddTypeBox), out var input, out var error))
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
        CancelLookup(add: true);
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
        _addVehicleOrderJson = string.Empty;
        HideApplyBanner(add: true);
        BindTypeBox(AddTypeBox, null);
        SetStatus(AddStatus, null);
        UpdateAddSave();
        UpdateLookupUi(add: true);
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
        || !string.IsNullOrWhiteSpace(_addVehicleOrderJson)
        || _addTypeId is not null;

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
            || _detailVehicleOrderJson != _loadedCar.VehicleOrderJson
            || _detailTypeId != _loadedCar.CarDetailsId;
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

    private async Task RunLookupAsync(bool add)
    {
        var makeBox = add ? AddMakeBox : DetailMakeBox;
        var vinBox = add ? AddVinBox : DetailVinBox;
        var status = add ? AddStatus : DetailStatus;
        var make = makeBox.Text;
        var vin = vinBox.Text;
        var nickname = add ? AddNameBox.Text : DetailNameBox.Text;
        var json = add ? _addVehicleOrderJson : _detailVehicleOrderJson;
        if (!MdecoderVehicleLookup.HasVin(vin))
        {
            SetStatus(status, MdecoderVehicleLookup.EmptyVinStatus);
            return;
        }

        if (!MdecoderVehicleLookup.LooksLikeBmw(make, vin))
        {
            SetStatus(status, MdecoderVehicleLookup.NonBmwStatus);
            return;
        }

        CancelLookup(add);
        var cts = new CancellationTokenSource();
        if (add)
        {
            _addLookupCts = cts;
        }
        else
        {
            _detailLookupCts = cts;
        }

        SetLookupRunning(add, running: true);
        IBrowserPageSession? browser = null;
        try
        {
            var yearText = add ? AddYearBox.Text : DetailYearBox.Text;
            int? existingYear = int.TryParse(yearText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                ? year
                : null;
            var result = await MdecoderVehicleLookup.RunAsync(
                make,
                vin,
                json,
                nickname,
                fetch: async (uri, token) =>
                {
                    var html = await MdecoderVehicleLookup.FetchHttpAsync(uri, token);
                    if (!MdecoderVehicleLookup.NeedsChromium(html))
                    {
                        return html;
                    }

                    ReportStatus(status, "Opening mdecoder in Chromium…");
                    browser ??= await ChromiumPageLoader.CreateAsync(XamlRoot, token);
                    var load = await browser.LoadAsync(uri, token);
                    return load.Html;
                },
                delay: (span, token) => Task.Delay(span, token),
                now: () => DateTimeOffset.Now,
                status: message => ReportStatus(status, message),
                existingModel: add ? AddModelBox.Text : DetailModelBox.Text,
                existingModelNumber: add ? AddModelNumberBox.Text : DetailModelNumberBox.Text,
                existingEngineType: add ? AddEngineBox.Text : DetailEngineBox.Text,
                existingYear: existingYear,
                cancellationToken: cts.Token);

            if (cts.IsCancellationRequested)
            {
                SetStatus(status, MdecoderVehicleLookup.CancelledStatus);
                return;
            }

            ApplyLookupResult(add, result);
        }
        catch (OperationCanceledException)
        {
            SetStatus(status, MdecoderVehicleLookup.CancelledStatus);
        }
        catch (Exception ex)
        {
            SetStatus(status, ex.Message);
        }
        finally
        {
            if (ReferenceEquals(add ? _addLookupCts : _detailLookupCts, cts))
            {
                if (add)
                {
                    _addLookupCts = null;
                }
                else
                {
                    _detailLookupCts = null;
                }

                SetLookupRunning(add, running: false);
            }

            if (browser is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }
    }

    private void ApplyLookupResult(bool add, MdecoderLookupResult result)
    {
        var status = add ? AddStatus : DetailStatus;
        SetStatus(status, result.StatusMessage);
        if (result.Status != MdecoderLookupStatus.Ready || result.Scalars is null)
        {
            return;
        }

        if (add)
        {
            _addVehicleOrderJson = result.VehicleOrderJson;
            _addPendingScalars = result.Scalars;
        }
        else
        {
            _detailVehicleOrderJson = result.VehicleOrderJson;
            _detailPendingScalars = result.Scalars;
        }

        ApplyScalarsToBoxes(add, result.Scalars, overwrite: false);
        if (result.OverwriteFields.Count == 0)
        {
            HideApplyBanner(add);
            return;
        }

        var text = $"mdecoder found different {string.Join(", ", result.OverwriteFields)}. Apply these fields? Nickname stays as it is.";
        if (add)
        {
            AddApplyText.Text = text;
            AddApplyBanner.Visibility = Visibility.Visible;
        }
        else
        {
            DetailApplyText.Text = text;
            DetailApplyBanner.Visibility = Visibility.Visible;
        }
    }

    private void ApplyPendingScalars(bool add, bool overwrite)
    {
        var scalars = add ? _addPendingScalars : _detailPendingScalars;
        if (scalars is not null)
        {
            ApplyScalarsToBoxes(add, scalars, overwrite);
        }

        HideApplyBanner(add);
    }

    private void ApplyScalarsToBoxes(bool add, MdecoderScalars scalars, bool overwrite)
    {
        if (add)
        {
            FillBox(AddMakeBox, scalars.Make, overwrite);
            FillBox(AddModelBox, scalars.Model, overwrite);
            FillBox(AddModelNumberBox, scalars.ModelNumber, overwrite);
            FillBox(AddEngineBox, scalars.EngineType, overwrite);
            FillYear(AddYearBox, scalars.Year, overwrite);
            UpdateAddSave();
            return;
        }

        _suppressFields = true;
        FillBox(DetailMakeBox, scalars.Make, overwrite);
        FillBox(DetailModelBox, scalars.Model, overwrite);
        FillBox(DetailModelNumberBox, scalars.ModelNumber, overwrite);
        FillBox(DetailEngineBox, scalars.EngineType, overwrite);
        FillYear(DetailYearBox, scalars.Year, overwrite);
        _suppressFields = false;
        UpdateDetailSave();
    }

    private static void FillBox(TextBox box, string? incoming, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return;
        }

        if (overwrite || string.IsNullOrWhiteSpace(box.Text))
        {
            box.Text = incoming;
        }
    }

    private static void FillYear(TextBox box, int? incoming, bool overwrite)
    {
        if (incoming is not int year)
        {
            return;
        }

        if (overwrite || string.IsNullOrWhiteSpace(box.Text))
        {
            box.Text = year.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void HideApplyBanner(bool add)
    {
        if (add)
        {
            _addPendingScalars = null;
            AddApplyBanner.Visibility = Visibility.Collapsed;
            return;
        }

        _detailPendingScalars = null;
        DetailApplyBanner.Visibility = Visibility.Collapsed;
    }

    private void CancelLookup(bool add)
    {
        if (add)
        {
            _addLookupCts?.Cancel();
            return;
        }

        _detailLookupCts?.Cancel();
    }

    private void SetLookupRunning(bool add, bool running)
    {
        if (add)
        {
            _addLookupRunning = running;
            AddLookupCancelButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            UpdateLookupUi(add: true);
            return;
        }

        _detailLookupRunning = running;
        DetailLookupCancelButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        UpdateLookupUi(add: false);
    }

    private void UpdateLookupUi(bool add)
    {
        var running = add ? _addLookupRunning : _detailLookupRunning;
        var make = add ? AddMakeBox.Text : DetailMakeBox.Text;
        var vin = add ? AddVinBox.Text : DetailVinBox.Text;
        var button = add ? AddLookupButton : DetailLookupButton;
        button.IsEnabled = !running && MdecoderVehicleLookup.CanRequest(make, vin);
        if (running)
        {
            return;
        }

        var status = add ? AddStatus : DetailStatus;
        if (MdecoderVehicleLookup.HasVin(vin) && !MdecoderVehicleLookup.LooksLikeBmw(make, vin))
        {
            SetStatus(status, MdecoderVehicleLookup.NonBmwStatus);
            return;
        }

        if (status.Text == MdecoderVehicleLookup.NonBmwStatus)
        {
            SetStatus(status, null);
        }
    }

    private void ReportStatus(TextBlock status, string message)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            SetStatus(status, message);
            return;
        }

        DispatcherQueue.TryEnqueue(() => SetStatus(status, message));
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
        foreach (var type in types)
        {
            _typePicks.Add(new TypePick
            {
                Id = type.Id,
                Label = $"{type.Make} {type.ModelNumber} · {type.Year}{(type.EndYear is int end ? "–" + end : "–")}",
                Haystack = CarDetailsTypeLookup.Haystack(type.Make, type.Model, type.ModelNumber, type.Year, type.EndYear),
            });
        }
    }

    private void BindTypeBox(AutoSuggestBox box, Guid? typeId)
    {
        var pick = typeId is Guid id ? _typePicks.FirstOrDefault(row => row.Id == id) : null;
        ApplyTypePick(box, pick);
        if (pick is not null)
        {
            box.DispatcherQueue.TryEnqueue(() =>
            {
                if (SelectedTypeId(box) == pick.Id && box.Text != pick.Label)
                {
                    ApplyTypePick(box, pick);
                }
            });
        }
    }

    private void ApplyTypePick(AutoSuggestBox box, TypePick? pick)
    {
        _bindingTypeBox = true;
        try
        {
            SetTypeId(box, pick?.Id);
            box.ItemsSource = Array.Empty<TypePick>();
            box.Text = pick?.Label ?? string.Empty;
        }
        finally
        {
            _bindingTypeBox = false;
        }
    }

    private void TypeBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs e)
    {
        if (_bindingTypeBox || e.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var selected = TypePickFor(SelectedTypeId(sender));
        if (selected is not null && string.Equals(sender.Text, selected.Label, StringComparison.Ordinal))
        {
            return;
        }

        SetTypeId(sender, null);
        sender.ItemsSource = CarDetailsTypeLookup.Filter(_typePicks, pick => pick.Haystack, sender.Text);
        UpdateTypeSave(sender);
    }

    private void TypeBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs e)
    {
        if (e.SelectedItem is TypePick pick)
        {
            ApplyTypePick(sender, pick);
        }

        UpdateTypeSave(sender);
    }

    private void TypeBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs e)
    {
        if (e.ChosenSuggestion is TypePick chosen)
        {
            ApplyTypePick(sender, chosen);
            UpdateTypeSave(sender);
            return;
        }

        if (string.IsNullOrWhiteSpace(sender.Text))
        {
            ApplyTypePick(sender, null);
            UpdateTypeSave(sender);
            return;
        }

        var matches = CarDetailsTypeLookup.Filter(_typePicks, pick => pick.Haystack, sender.Text, max: 2);
        if (matches.Count == 1)
        {
            ApplyTypePick(sender, matches[0]);
        }

        UpdateTypeSave(sender);
    }

    private TypePick? TypePickFor(Guid? typeId) =>
        typeId is Guid id ? _typePicks.FirstOrDefault(row => row.Id == id) : null;

    private void SetTypeId(AutoSuggestBox box, Guid? typeId)
    {
        if (ReferenceEquals(box, DetailTypeBox))
        {
            _detailTypeId = typeId;
        }
        else
        {
            _addTypeId = typeId;
        }
    }

    private Guid? SelectedTypeId(AutoSuggestBox box) =>
        ReferenceEquals(box, DetailTypeBox) ? _detailTypeId : _addTypeId;

    private void UpdateTypeSave(AutoSuggestBox box)
    {
        if (_suppressFields)
        {
            return;
        }

        if (ReferenceEquals(box, DetailTypeBox))
        {
            UpdateDetailSave();
        }
        else
        {
            UpdateAddSave();
        }
    }

    private static bool IsInsideAutoSuggestBox(object source)
    {
        if (source is not DependencyObject node)
        {
            return false;
        }

        while (node is not null)
        {
            if (node is AutoSuggestBox)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private sealed class TypePick
    {
        public Guid Id { get; init; }
        public required string Label { get; init; }
        public required string Haystack { get; init; }

        public override string ToString() => Label;
    }

    private sealed class CarRow
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Detail { get; init; }
        public BitmapImage? Thumbnail { get; init; }
    }
}
