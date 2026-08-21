using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkCosts.Helpers;
using WorkCosts.Models;
using WorkCosts.Services;

namespace WorkCosts.Controls;

public sealed partial class ProductAddEditor : UserControl
{
    private readonly ObservableCollection<JobOption> _jobOptions = [];
    private readonly ProductImageService _images = new();
    private byte[]? _imageBlob;
    private string? _imageContentType;
    private string _source = string.Empty;
    private bool _readOnly;
    private bool _requiresProductImage;
    private bool _suppressDirty;
    private Guid _savedCategoryId;
    private string _savedPricePoint = string.Empty;
    private bool _savedIsAllJobs;
    private HashSet<Guid> _savedJobIds = [];
    private ProductExtra _extra = new();
    private IReadOnlyList<ProductImageCandidate> _loadedImages = [];
    private string _urlBeforeEdit = string.Empty;

    /// <summary>
    /// Return false to skip fetching (for example after loading an existing product).
    /// </summary>
    public Func<string, Task<bool>>? ShouldContinueLoadingUrlAsync { get; set; }

    public bool IsDirty { get; private set; }

    public bool RequiresProductImage => _requiresProductImage;

    public bool HasProductImage => _imageBlob is { Length: > 0 };

    public event EventHandler? DirtyChanged;

    public event EventHandler? ProductImageStateChanged;

    public ProductAddEditor()
    {
        InitializeComponent();
        JobChecks.ItemsSource = _jobOptions;
        PricePointRadios.ItemsSource = ProductPricePoints.Options;
        CategoryRadios.SelectionChanged += (_, _) => RecalcAssignmentsDirty();
        PricePointRadios.SelectionChanged += (_, _) => RecalcAssignmentsDirty();
        AllJobsToggle.Toggled += (_, _) => RecalcAssignmentsDirty();
        FillTechnologyBox(TechnologyBox);
        BindInputToolTips();
    }

    private void BindInputToolTips()
    {
        InputToolTip.Bind(UrlBox);
        InputToolTip.Bind(NameBox);
        InputToolTip.Bind(VendorBox);
        InputToolTip.Bind(ManufacturerBox);
        InputToolTip.Bind(MfrBox);
        InputToolTip.Bind(CostBox);
        InputToolTip.Bind(EanBox);
        InputToolTip.Bind(VariationBox);
        InputToolTip.Bind(OemBox);
        InputToolTip.Bind(CapacityBox);
        InputToolTip.Bind(LengthBox);
        InputToolTip.Bind(WidthBox);
        InputToolTip.Bind(HeightBox);
        InputToolTip.Bind(CcaBox);
        InputToolTip.Bind(TechnologyBox, () => TechnologyBox.SelectedItem as string);
        InputToolTip.Bind(CategoryRadios, () => (CategoryRadios.SelectedItem as Category)?.Name);
        InputToolTip.Bind(PricePointRadios, () => (PricePointRadios.SelectedItem as PricePointOption)?.Label);
        InputToolTip.Bind(AllJobsToggle, "All Jobs");
        RefreshUrlDisplay();
        UpdateVendorDisplay();
    }

    public void SetLookups(IReadOnlyList<Category> categories, IReadOnlyList<Job> jobs)
    {
        _suppressDirty = true;
        CategoryRadios.ItemsSource = categories;
        if (categories.Count > 0 && CategoryRadios.SelectedItem is null)
        {
            CategoryRadios.SelectedItem = categories[0];
        }

        foreach (var job in _jobOptions)
        {
            job.PropertyChanged -= JobOption_PropertyChanged;
        }

        _jobOptions.Clear();
        foreach (var job in jobs)
        {
            var option = new JobOption(job.Id, job.Name);
            option.PropertyChanged += JobOption_PropertyChanged;
            _jobOptions.Add(option);
        }

        NoJobsText.Visibility = jobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        JobChecks.Visibility = jobs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _suppressDirty = false;
    }

    public void LoadEmpty()
    {
        _suppressDirty = true;
        SetReadOnly(false);
        SetRequiresProductImage(false);
        ClearPageFields();
        ResetAssignments();
        UrlBox.Text = string.Empty;
        SetUrlEditMode(false);
        _suppressDirty = false;
        CaptureAssignmentBaseline();
        NotifyProductImageStateChanged();
    }

    public async Task LoadExistingAsync(Product product, IEnumerable<Guid> selectedJobIds)
    {
        _suppressDirty = true;
        UrlBox.Text = product.Url ?? string.Empty;
        SetUrlEditMode(false);
        RefreshUrlDisplay();
        NameBox.Text = product.Name;
        _source = product.Source ?? string.Empty;
        VendorBox.Text = product.Vendor;
        UpdateVendorDisplay();
        ManufacturerBox.Text = product.Manufacturer;
        MfrBox.Text = product.ManufacturerReference;
        EanBox.Text = product.Ean;
        VariationBox.Text = product.Variation;
        OemBox.Text = product.OemEquivalent;
        LoadExtraFields(product.ExtraYaml);
        CostBox.Value = (double)product.UnitCost;

        if (CategoryRadios.ItemsSource is IEnumerable<Category> source)
        {
            var cats = source.ToList();
            CategoryRadios.SelectedItem = cats.FirstOrDefault(c => c.Id == product.CategoryId)
                ?? cats.FirstOrDefault();
        }

        SelectPricePoint(product.PricePoint);
        AllJobsToggle.IsOn = product.IsAllJobs;
        var selected = selectedJobIds.ToHashSet();
        foreach (var job in _jobOptions)
        {
            job.IsSelected = selected.Contains(job.Id);
            job.IsEnabled = true;
        }

        _imageBlob = product.ImageBlob;
        _imageContentType = product.ImageContentType;
        PreviewImage.Source = await ProductImagePicker.ToBitmapAsync(_imageBlob);
        SetRequiresProductImage(false);
        SetReadOnly(true);
        _suppressDirty = false;
        CaptureAssignmentBaseline();
        NotifyProductImageStateChanged();
    }

    public void MarkClean() => CaptureAssignmentBaseline();

    private void CaptureAssignmentBaseline()
    {
        _savedCategoryId = CategoryRadios.SelectedItem is Category category ? category.Id : Guid.Empty;
        _savedPricePoint = GetSelectedPricePoint();
        _savedIsAllJobs = AllJobsToggle.IsOn;
        _savedJobIds = _jobOptions.Where(job => job.IsSelected).Select(job => job.Id).ToHashSet();
        SetDirty(false);
    }

    private void JobOption_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(JobOption.IsSelected) or null)
        {
            RecalcAssignmentsDirty();
        }
    }

    private void RecalcAssignmentsDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        var categoryId = CategoryRadios.SelectedItem is Category category ? category.Id : Guid.Empty;
        var jobs = _jobOptions.Where(job => job.IsSelected).Select(job => job.Id).ToHashSet();
        var dirty = categoryId != _savedCategoryId
            || !string.Equals(_savedPricePoint, GetSelectedPricePoint(), StringComparison.Ordinal)
            || _savedIsAllJobs != AllJobsToggle.IsOn
            || !_savedJobIds.SetEquals(jobs);
        SetDirty(dirty);
    }

    private void SetDirty(bool dirty)
    {
        if (IsDirty == dirty)
        {
            return;
        }

        IsDirty = dirty;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetReadOnly(bool readOnly)
    {
        _readOnly = readOnly;
        NameBox.IsReadOnly = readOnly;
        VendorBox.IsReadOnly = readOnly;
        ManufacturerBox.IsReadOnly = readOnly;
        MfrBox.IsReadOnly = readOnly;
        EanBox.IsReadOnly = readOnly;
        VariationBox.IsReadOnly = readOnly;
        OemBox.IsReadOnly = readOnly;
        CostBox.IsEnabled = !readOnly;
        CapacityBox.IsEnabled = !readOnly;
        LengthBox.IsEnabled = !readOnly;
        WidthBox.IsEnabled = !readOnly;
        HeightBox.IsEnabled = !readOnly;
        CcaBox.IsEnabled = !readOnly;
        TechnologyBox.IsEnabled = !readOnly;
        FetchImagesButton.IsEnabled = !readOnly;
        ChooseImageFileButton.IsEnabled = !readOnly;
        ClearImageButton.IsEnabled = !readOnly;
        UrlDisplayButton.IsEnabled = !readOnly;
        UrlBox.IsReadOnly = readOnly;
        CategoryRadios.IsEnabled = true;
        PricePointRadios.IsEnabled = true;
        AllJobsToggle.IsEnabled = true;
        JobChecks.IsEnabled = true;
        foreach (var job in _jobOptions)
        {
            job.IsEnabled = true;
        }

        if (readOnly)
        {
            SetUrlEditMode(false);
        }
    }

    /// <summary>Starts the details stage with a URL from the entry panel and loads the page.</summary>
    public async Task BeginWithUrlAsync(string url)
    {
        SetReadOnly(false);
        SetRequiresProductImage(false);
        UrlBox.Text = url.Trim();
        SetUrlEditMode(false);
        RefreshUrlDisplay();
        await LoadFromUrlAsync(resetAssignmentsIfUncached: true, checkExisting: false, throwOnFailure: true);
        NotifyProductImageStateChanged();
    }

    /// <summary>Starts the details stage from pasted or file HTML. Never starts Chromium.</summary>
    public async Task BeginWithHtmlAsync(string url, string html, Action<string?>? overlayStatus = null)
    {
        StartupLog.Write($"BeginWithHtmlAsync url={url} htmlChars={html.Length}");
        SetReadOnly(false);
        SetRequiresProductImage(true);
        UrlBox.Text = url.Trim();
        SetUrlEditMode(false);
        RefreshUrlDisplay();
        ClearPageFields();
        ResetAssignments();

        SetFetchBusy(true);
        SetFetchStatus("Reading pasted HTML…");
        overlayStatus?.Invoke("Reading pasted HTML…");
        try
        {
            var page = await _images.LoadFromHtmlAsync(
                url,
                html,
                status: message =>
                {
                    SetFetchStatus(message);
                    overlayStatus?.Invoke(message);
                });
            ApplyPageMetadata(page.Metadata);
            _loadedImages = page.Images;

            var inferredSource = ProductVendorHelper.InferSourceFromUrl(url);
            if (!string.IsNullOrWhiteSpace(inferredSource))
            {
                _source = inferredSource;
                UpdateVendorDisplay();
            }

            RefreshUrlDisplay();
            StartupLog.Write($"BeginWithHtmlAsync parsed images={page.Images.Count}");
            SetFetchBusy(false);
            overlayStatus?.Invoke(null);
            await ApplyLoadedImagesAsync(page.Images);
        }
        catch (Exception ex)
        {
            StartupLog.Write("BeginWithHtmlAsync failed", ex);
            SetFetchStatus(ex.Message);
            throw;
        }
        finally
        {
            SetFetchBusy(false);
            overlayStatus?.Invoke(null);
            NotifyProductImageStateChanged();
        }
    }

    public bool TryRead(out ProductEditorValues values, out string? error)
    {
        values = default!;
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Name is required.";
            return false;
        }

        if (CategoryRadios.SelectedItem is not Category category)
        {
            error = "Category is required.";
            return false;
        }

        if (double.IsNaN(CostBox.Value) || CostBox.Value < 0)
        {
            error = "Unit cost must be zero or greater.";
            return false;
        }

        var isAll = AllJobsToggle.IsOn;
        var chosenJobs = _jobOptions.Where(j => j.IsSelected).Select(j => j.Id).ToList();
        if (!isAll && chosenJobs.Count == 0)
        {
            error = "Select at least one job, or enable 'Available for all jobs'.";
            return false;
        }

        if (_requiresProductImage && (_imageBlob is null || _imageBlob.Length == 0))
        {
            error = "A product image is required.";
            return false;
        }

        values = new ProductEditorValues(
            name,
            VendorBox.Text.Trim(),
            _source.Trim(),
            ManufacturerBox.Text.Trim(),
            MfrBox.Text.Trim(),
            EanBox.Text.Trim(),
            VariationBox.Text.Trim(),
            OemBox.Text.Trim(),
            UrlBox.Text.Trim(),
            (decimal)CostBox.Value,
            category.Id,
            category.Name,
            isAll,
            GetSelectedPricePoint(),
            chosenJobs,
            _imageBlob,
            _imageContentType,
            ReadExtraYaml());
        error = null;
        return true;
    }

    private void ResetAssignments()
    {
        if (CategoryRadios.Items.Count > 0)
        {
            CategoryRadios.SelectedIndex = 0;
        }

        AllJobsToggle.IsOn = false;
        SelectPricePoint(null);
        foreach (var job in _jobOptions)
        {
            job.IsSelected = false;
            job.IsEnabled = true;
        }
    }

    private void ClearPageFields()
    {
        NameBox.Text = string.Empty;
        _source = string.Empty;
        VendorBox.Text = string.Empty;
        UpdateVendorDisplay();
        ManufacturerBox.Text = string.Empty;
        MfrBox.Text = string.Empty;
        EanBox.Text = string.Empty;
        VariationBox.Text = string.Empty;
        OemBox.Text = string.Empty;
        CostBox.Value = 0;
        ClearExtraFields();
        _loadedImages = [];
        _imageBlob = null;
        _imageContentType = null;
        PreviewImage.Source = null;
    }

    private void UrlDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (_readOnly)
        {
            return;
        }

        SetUrlEditMode(true);
    }

    /// <summary>
    /// Cancels URL edit without fetching. Returns true when the URL field was in edit mode.
    /// </summary>
    public bool TryCancelUrlEdit()
    {
        if (UrlEditPanel.Visibility != Visibility.Visible)
        {
            return false;
        }

        UrlBox.Text = _urlBeforeEdit;
        SetUrlEditMode(false);
        return true;
    }

    private void UrlBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshUrlDisplay();

    private async void UrlAccept_Click(object sender, RoutedEventArgs e)
    {
        SetUrlEditMode(false);
        await LoadFromUrlAsync(resetAssignmentsIfUncached: true, checkExisting: true);
    }

    private async void FetchImages_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedImages.Count > 0)
        {
            await ChooseFromLoadedImagesAsync();
            return;
        }

        await LoadFromUrlAsync(resetAssignmentsIfUncached: true, checkExisting: true);
    }

    private async Task LoadFromUrlAsync(bool resetAssignmentsIfUncached, bool checkExisting, bool throwOnFailure = false)
    {
        if (_readOnly)
        {
            return;
        }

        var url = UrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            SetUrlEditMode(true);
            await DialogHelper.ShowMessageAsync(XamlRoot, "URL required",
                "Enter a product page URL, then Accept to load details.");
            return;
        }

        if (checkExisting && ShouldContinueLoadingUrlAsync is not null
            && !await ShouldContinueLoadingUrlAsync(url))
        {
            return;
        }

        var cached = _images.IsCached(url);
        if (resetAssignmentsIfUncached && !cached)
        {
            ClearPageFields();
            ResetAssignments();
        }

        SetFetchBusy(true);
        SetFetchStatus("Loading product page…");
        try
        {
            var page = await ProductImagePicker.FetchPageAsync(
                XamlRoot,
                url,
                SetFetchStatus);
            ApplyPageMetadata(page.Metadata);
            _loadedImages = page.Images;
            SetFetchBusy(false);
            await ApplyLoadedImagesAsync(page.Images);

            var inferredSource = ProductVendorHelper.InferSourceFromUrl(url);
            if (!string.IsNullOrWhiteSpace(inferredSource))
            {
                _source = inferredSource;
                UpdateVendorDisplay();
            }

            RefreshUrlDisplay();
            NotifyProductImageStateChanged();
        }
        catch (Exception ex)
        {
            StartupLog.Write("LoadFromUrlAsync failed", ex);
            SetFetchStatus(ex.Message);
            if (throwOnFailure)
            {
                throw;
            }
        }
        finally
        {
            SetFetchBusy(false);
        }
    }

    private async Task ApplyLoadedImagesAsync(IReadOnlyList<ProductImageCandidate> images)
    {
        if (images.Count == 0)
        {
            StartupLog.Write("ApplyLoadedImagesAsync: no images captured.");
            SetFetchStatus("Page loaded, but no product images were captured. Load images from the product URL or choose an image file.");
            return;
        }

        var chosen = images[0];
        if (images.Count > 1 && XamlRoot is not null)
        {
            StartupLog.Write($"ApplyLoadedImagesAsync: opening image chooser for {images.Count} images.");
            try
            {
                var picked = await ProductImagePicker.ChooseFromCandidatesAsync(XamlRoot, images);
                if (picked is not null)
                {
                    chosen = picked;
                }
            }
            catch (Exception ex)
            {
                StartupLog.Write("ApplyLoadedImagesAsync: image chooser failed; using the first image.", ex);
                SetFetchStatus("Could not open the image picker. Using the first downloaded image.");
            }
        }

        await ApplyChosenImageAsync(chosen);
        SetFetchStatus(null);
    }

    private async Task ChooseFromLoadedImagesAsync()
    {
        if (_readOnly || _loadedImages.Count == 0)
        {
            StartupLog.Write($"ChooseFromLoadedImagesAsync skipped readOnly={_readOnly} count={_loadedImages.Count}");
            return;
        }

        StartupLog.Write($"ChooseFromLoadedImagesAsync opening chooser count={_loadedImages.Count}");
        var picked = await ProductImagePicker.ChooseFromCandidatesAsync(XamlRoot, _loadedImages);
        if (picked is null)
        {
            return;
        }

        await ApplyChosenImageAsync(picked);
        SetFetchStatus(null);
        NotifyProductImageStateChanged();
    }

    private async Task ApplyChosenImageAsync(ProductImageCandidate chosen)
    {
        _imageBlob = chosen.Bytes;
        _imageContentType = chosen.ContentType;
        PreviewImage.Source = await ProductImagePicker.ToBitmapAsync(_imageBlob);
    }

    private void ApplyPageMetadata(ProductPageMetadata metadata)
    {
        var values = ProductPageClientValues.From(metadata);
        if (values.Name is not null)
        {
            NameBox.Text = values.Name;
        }

        if (values.Manufacturer is not null)
        {
            ManufacturerBox.Text = values.Manufacturer;
        }

        if (values.ManufacturerReference is not null)
        {
            MfrBox.Text = values.ManufacturerReference;
        }

        if (values.Vendor is not null)
        {
            VendorBox.Text = values.Vendor;
        }

        if (values.Source is not null)
        {
            _source = values.Source;
        }

        UpdateVendorDisplay();

        if (values.Ean is not null)
        {
            EanBox.Text = values.Ean;
        }

        if (values.Variation is not null)
        {
            VariationBox.Text = values.Variation;
        }

        if (values.OemEquivalent is not null)
        {
            OemBox.Text = values.OemEquivalent;
        }

        if (values.UnitPrice is decimal price)
        {
            CostBox.Value = (double)price;
        }

        ApplyExtraClient(values);
    }

    private void SetFetchBusy(bool busy)
    {
        FetchImagesButton.IsEnabled = !busy && !_readOnly;
        ChooseImageFileButton.IsEnabled = !busy && !_readOnly;
        ClearImageButton.IsEnabled = !busy && !_readOnly;
        FetchImagesButton.Opacity = busy ? 0.4 : 1;
        FetchImagesIcon.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        FetchImagesRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        FetchImagesRing.IsActive = busy;
        ToolTipService.SetToolTip(FetchImagesButton, busy ? "Loading…" : "Load images from product URL");
    }

    private void SetFetchStatus(string? message)
    {
        var text = message?.Trim() ?? string.Empty;
        FetchStatusText.Text = text;
        FetchStatusText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void VendorBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateVendorDisplay();

    private void UpdateVendorDisplay()
    {
        var hasSource = !string.IsNullOrWhiteSpace(_source);
        VendorBreadcrumb.Visibility = hasSource ? Visibility.Visible : Visibility.Collapsed;
        VendorBox.Visibility = hasSource ? Visibility.Collapsed : Visibility.Visible;
        VendorBreadcrumb.Text = ProductVendorHelper.FormatBreadcrumb(_source, VendorBox.Text);
        InputToolTip.Set(VendorBreadcrumb, "Vendor", VendorBreadcrumb.Text);
        InputToolTip.Set(VendorBox, "Vendor", VendorBox.Text);
    }

    private async void ChooseImageFile_Click(object sender, RoutedEventArgs e)
    {
        if (_readOnly)
        {
            return;
        }

        StartupLog.Write($"ChooseImageFile_Click loadedImages={_loadedImages.Count}");
        if (_loadedImages.Count > 0)
        {
            await ChooseFromLoadedImagesAsync();
            return;
        }

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
            StartupLog.Write("ChooseImageFile_Click picker failed", ex);
            SetFetchStatus(ex.Message);
            return;
        }

        if (file is null)
        {
            StartupLog.Write("ChooseImageFile_Click: no file selected (picker cancelled or did not show).");
            return;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(file.Path);
            if (bytes.Length == 0)
            {
                await DialogHelper.ShowMessageAsync(XamlRoot, "Image file",
                    "The image file is empty or could not be read.");
                return;
            }

            _imageBlob = bytes;
            _imageContentType = ImageContentType(file.FileType);
            PreviewImage.Source = await ProductImagePicker.ToBitmapAsync(_imageBlob);
            SetFetchStatus(null);
            NotifyProductImageStateChanged();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Image file",
                string.IsNullOrWhiteSpace(ex.Message)
                    ? "The image file is empty or could not be read."
                    : ex.Message);
        }
    }

    private void ClearImage_Click(object sender, RoutedEventArgs e)
    {
        if (_readOnly)
        {
            return;
        }

        _imageBlob = null;
        _imageContentType = null;
        PreviewImage.Source = null;
        NotifyProductImageStateChanged();
    }

    private void SetRequiresProductImage(bool required)
    {
        _requiresProductImage = required;
        NotifyProductImageStateChanged();
    }

    private void NotifyProductImageStateChanged() =>
        ProductImageStateChanged?.Invoke(this, EventArgs.Empty);

    private static void BindPickerToAppWindow(object picker)
    {
        if (App.MainAppWindow is null)
        {
            throw new InvalidOperationException("Main window is not available.");
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private static string ImageContentType(string fileType) =>
        fileType.Trim().ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

    private void SetUrlEditMode(bool editing)
    {
        if (editing)
        {
            _urlBeforeEdit = UrlBox.Text;
        }

        UrlEditPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        UrlDisplayButton.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        RefreshUrlDisplay();
        if (editing)
        {
            UrlBox.Focus(FocusState.Programmatic);
        }
    }

    private void RefreshUrlDisplay()
    {
        var url = UrlBox.Text.Trim();
        UrlDisplayButton.Content = string.IsNullOrWhiteSpace(url) ? "Set product URL…" : url;
        InputToolTip.Set(UrlDisplayButton, UrlBox.PlaceholderText ?? "https://...", url);
    }

    private void SelectPricePoint(string? value)
    {
        PricePointRadios.SelectedItem = ProductPricePoints.Find(value);
    }

    private string GetSelectedPricePoint() =>
        PricePointRadios.SelectedItem is PricePointOption option ? option.Value : string.Empty;

    private void LoadExtraFields(string? yaml)
    {
        _extra = ProductExtra.Parse(yaml);
        SetIntBox(CapacityBox, _extra.Capacity);
        SetIntBox(LengthBox, _extra.LengthMm);
        SetIntBox(WidthBox, _extra.WidthMm);
        SetIntBox(HeightBox, _extra.HeightMm);
        SetIntBox(CcaBox, _extra.Cca);
        TechnologyBox.SelectedItem = _extra.Technology ?? "";
    }

    private void ClearExtraFields()
    {
        _extra = new ProductExtra();
        SetIntBox(CapacityBox, null);
        SetIntBox(LengthBox, null);
        SetIntBox(WidthBox, null);
        SetIntBox(HeightBox, null);
        SetIntBox(CcaBox, null);
        TechnologyBox.SelectedItem = "";
    }

    private string ReadExtraYaml()
    {
        _extra = _extra.WithKnown(
            ReadIntBox(CapacityBox),
            ReadIntBox(LengthBox),
            ReadIntBox(WidthBox),
            ReadIntBox(HeightBox),
            ReadIntBox(CcaBox),
            TechnologyBox.SelectedItem as string);
        return _extra.ToYaml();
    }

    private void ApplyExtraClient(ProductPageClientValues values)
    {
        if (values.Capacity is int capacity)
        {
            SetIntBox(CapacityBox, capacity);
        }

        if (values.LengthMm is int lengthMm)
        {
            SetIntBox(LengthBox, lengthMm);
        }

        if (values.WidthMm is int widthMm)
        {
            SetIntBox(WidthBox, widthMm);
        }

        if (values.HeightMm is int heightMm)
        {
            SetIntBox(HeightBox, heightMm);
        }

        if (values.Cca is int cca)
        {
            SetIntBox(CcaBox, cca);
        }

        if (values.Technology is not null)
        {
            TechnologyBox.SelectedItem = values.Technology;
        }

        if (values.ExtraUnknown is { Count: > 0 } extraUnknown)
        {
            _extra = _extra.MergeUnknown(extraUnknown);
        }
    }

    private static void FillTechnologyBox(ComboBox box)
    {
        box.Items.Clear();
        box.Items.Add("");
        foreach (var token in ProductExtra.TechnologyTokens)
        {
            box.Items.Add(token);
        }

        box.SelectedItem = "";
    }

    private static int? ReadIntBox(NumberBox box)
    {
        if (double.IsNaN(box.Value) || box.Value < 0)
        {
            return null;
        }

        return (int)Math.Round(box.Value);
    }

    private static void SetIntBox(NumberBox box, int? value) =>
        box.Value = value is int n ? n : double.NaN;

    private sealed class JobOption : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isEnabled = true;

        public JobOption(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; }
        public string Name { get; }

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
                OnPropertyChanged(nameof(ToolTipText));
            }
        }

        public string ToolTipText => InputToolTip.Format(Name, IsSelected ? "Yes" : "No");

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
