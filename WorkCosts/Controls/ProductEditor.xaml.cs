using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using WorkCosts.Data;
using WorkCosts.Helpers;
using WorkCosts.Models;
using WorkCosts.Services;

namespace WorkCosts.Controls;

public sealed partial class ProductEditor
{
    private readonly ObservableCollection<JobOption> _jobOptions = [];
    private readonly ObservableCollection<EquivalentProductItem> _equivalentProducts = [];
    private bool _suppressEvents;
    private byte[]? _imageBlob;
    private string? _imageContentType;
    private Guid? _productId;
    private string _source = string.Empty;
    /// <summary>URL last saved to the database (or empty). Edit buffer does not update this until an image is chosen.</summary>
    private string _committedUrl = string.Empty;
    private ProductExtra _extra = new();

    public ProductEditor()
    {
        InitializeComponent();
        JobChecks.ItemsSource = _jobOptions;
        EquivalentProductsList.ItemsSource = _equivalentProducts;
        PricePointRadios.ItemsSource = ProductPricePoints.Options;
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
        UpdateVendorDisplay();
        UpdateOpenUrlButton();
    }

    /// <summary>Raised when any editable value changes (for immediate persist in detail mode).</summary>
    public event EventHandler? ValuesChanged;

    public void SetLookups(IReadOnlyList<Category> categories, IReadOnlyList<Job> jobs)
    {
        _suppressEvents = true;
        CategoryRadios.ItemsSource = categories;
        if (categories.Count > 0 && CategoryRadios.SelectedItem is null)
        {
            CategoryRadios.SelectedItem = categories[0];
        }

        _jobOptions.Clear();
        foreach (var job in jobs)
        {
            _jobOptions.Add(new JobOption(job.Id, job.Name));
        }

        NoJobsText.Visibility = jobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        JobChecks.Visibility = jobs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _suppressEvents = false;
    }

    public void LoadEmpty()
    {
        _productId = null;
        _suppressEvents = true;
        NameBox.Text = string.Empty;
        _source = string.Empty;
        VendorBox.Text = string.Empty;
        UpdateVendorDisplay();
        ManufacturerBox.Text = string.Empty;
        MfrBox.Text = string.Empty;
        EanBox.Text = string.Empty;
        VariationBox.Text = string.Empty;
        OemBox.Text = string.Empty;
        ClearExtraFields();
        _committedUrl = string.Empty;
        UrlBox.Text = string.Empty;
        CostBox.Value = 0;
        if (CategoryRadios.Items.Count > 0)
        {
            CategoryRadios.SelectedIndex = 0;
        }

        SelectPricePoint(null);

        AllJobsToggle.IsOn = false;
        foreach (var job in _jobOptions)
        {
            job.IsSelected = false;
            job.IsEnabled = true;
        }

        _imageBlob = null;
        _imageContentType = null;
        PreviewImage.Source = null;
        _equivalentProducts.Clear();
        SetUrlEditMode(false);
        UpdateOpenUrlButton();
        _suppressEvents = false;
    }

    public async Task LoadAsync(Product product, IEnumerable<Guid> selectedJobIds)
    {
        _productId = product.Id;
        _suppressEvents = true;
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
        _committedUrl = product.Url ?? string.Empty;
        UrlBox.Text = _committedUrl;
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
        SetUrlEditMode(false);
        UpdateOpenUrlButton();
        await LoadEquivalentsAsync(product.Id);
        _suppressEvents = false;
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

        values = new ProductEditorValues(
            name,
            VendorBox.Text.Trim(),
            _source.Trim(),
            ManufacturerBox.Text.Trim(),
            MfrBox.Text.Trim(),
            EanBox.Text.Trim(),
            VariationBox.Text.Trim(),
            OemBox.Text.Trim(),
            _committedUrl,
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

    private void RaiseChanged()
    {
        if (!_suppressEvents)
        {
            ValuesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Field_Changed(object sender, TextChangedEventArgs e)
    {
        if (ReferenceEquals(sender, VendorBox))
        {
            UpdateVendorDisplay();
        }

        RaiseChanged();
    }

    private void CostBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => RaiseChanged();

    private void CategoryRadios_SelectionChanged(object sender, SelectionChangedEventArgs e) => RaiseChanged();

    private void PricePointRadios_SelectionChanged(object sender, SelectionChangedEventArgs e) => RaiseChanged();

    private void AllJobsToggle_Toggled(object sender, RoutedEventArgs e) => RaiseChanged();

    private void JobCheck_Changed(object sender, RoutedEventArgs e) => RaiseChanged();

    private void ImportNew_Click(object sender, RoutedEventArgs e)
    {
        UrlBox.Text = _committedUrl;
        SetUrlEditMode(true);
    }

    private async void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_committedUrl)
            || !Uri.TryCreate(_committedUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        await Launcher.LaunchUriAsync(uri);
    }

    private void UrlCancel_Click(object sender, RoutedEventArgs e)
    {
        UrlBox.Text = _committedUrl;
        SetUrlEditMode(false);
    }

    private async void UrlGo_Click(object sender, RoutedEventArgs e) =>
        await FetchImagesFromUrlAsync(commitUrlOnImagePick: true);

    private async void FetchImages_Click(object sender, RoutedEventArgs e)
    {
        // Reload from the committed product URL (not a pending import buffer).
        if (string.IsNullOrWhiteSpace(_committedUrl))
        {
            SetUrlEditMode(true);
            await DialogHelper.ShowMessageAsync(XamlRoot, "URL required",
                "Use Import new to enter a product page URL, then Go to choose an image.");
            return;
        }

        UrlBox.Text = _committedUrl;
        await FetchImagesFromUrlAsync(commitUrlOnImagePick: true);
    }

    private async Task FetchImagesFromUrlAsync(bool commitUrlOnImagePick)
    {
        var url = UrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            SetUrlEditMode(true);
            await DialogHelper.ShowMessageAsync(XamlRoot, "URL required",
                "Enter a product page URL, then tap Go to load images.");
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Invalid URL",
                "Enter a valid http(s) product page URL.");
            return;
        }

        SetFetchBusy(true);
        try
        {
            var picked = await ProductImagePicker.PickFromPageAsync(
                XamlRoot,
                url,
                SetFetchBusy);
            if (picked is null)
            {
                // User cancelled image chooser — do not change committed URL or persist.
                return;
            }

            ApplyPageMetadata(picked.Metadata, raiseChanged: false);

            _imageBlob = picked.Bytes;
            _imageContentType = picked.ContentType;
            PreviewImage.Source = await ProductImagePicker.ToBitmapAsync(_imageBlob);

            if (commitUrlOnImagePick)
            {
                _committedUrl = url;
                UrlBox.Text = _committedUrl;
                SetUrlEditMode(false);
                UpdateOpenUrlButton();
            }

            var inferredSource = ProductVendorHelper.InferSourceFromUrl(url);
            if (!string.IsNullOrWhiteSpace(inferredSource))
            {
                _source = inferredSource;
                UpdateVendorDisplay();
            }

            RaiseChanged();
        }
        finally
        {
            SetFetchBusy(false);
        }
    }

    private void ApplyPageMetadata(ProductPageMetadata metadata, bool raiseChanged)
    {
        var values = ProductPageClientValues.From(metadata);
        _suppressEvents = true;
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

        if (values.UnitPrice is { } price)
        {
            CostBox.Value = (double)price;
        }

        ApplyExtraClient(values);

        _suppressEvents = false;
        if (raiseChanged)
        {
            RaiseChanged();
        }
    }

    private void SetFetchBusy(bool busy)
    {
        FetchImagesButton.IsEnabled = !busy;
        FetchImagesButton.Opacity = busy ? 0.4 : 1;
        FetchImagesIcon.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        FetchImagesRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        FetchImagesRing.IsActive = busy;
        UrlGoButton.IsEnabled = !busy;
        ImportNewButton.IsEnabled = !busy;
        OpenUrlButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(_committedUrl);
        ToolTipService.SetToolTip(FetchImagesButton, busy ? "Loading…" : "Load images from product URL");
    }

    private void ClearImage_Click(object sender, RoutedEventArgs e)
    {
        _imageBlob = null;
        _imageContentType = null;
        PreviewImage.Source = null;
        RaiseChanged();
    }

    private void SetUrlEditMode(bool editing)
    {
        UrlEditPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        ImportNewButton.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        if (editing)
        {
            UrlBox.Focus(FocusState.Programmatic);
        }
    }

    private void SelectPricePoint(string? value)
    {
        PricePointRadios.SelectedItem = ProductPricePoints.Find(value);
    }

    private string GetSelectedPricePoint() =>
        PricePointRadios.SelectedItem is PricePointOption option ? option.Value : string.Empty;

    private void UpdateVendorDisplay()
    {
        var hasSource = !string.IsNullOrWhiteSpace(_source);
        VendorBreadcrumb.Visibility = hasSource ? Visibility.Visible : Visibility.Collapsed;
        VendorBox.Visibility = hasSource ? Visibility.Collapsed : Visibility.Visible;
        VendorBreadcrumb.Text = ProductVendorHelper.FormatBreadcrumb(_source, VendorBox.Text);
        InputToolTip.Set(VendorBreadcrumb, "Vendor", VendorBreadcrumb.Text);
        InputToolTip.Set(VendorBox, "Vendor", VendorBox.Text);
    }

    private void UpdateOpenUrlButton()
    {
        OpenUrlButton.IsEnabled = !string.IsNullOrWhiteSpace(_committedUrl);
    }

    private async Task LoadEquivalentsAsync(Guid productId)
    {
        await using var db = App.Database.CreateContext();
        var links = await db.ProductEquivalents
            .AsNoTracking()
            .Where(e => e.ProductId == productId || e.EquivalentProductId == productId)
            .Include(e => e.Product)
            .Include(e => e.EquivalentProduct)
            .ToListAsync();

        _equivalentProducts.Clear();
        foreach (var link in links)
        {
            var other = link.ProductId == productId ? link.EquivalentProduct : link.Product;
            if (other is null)
            {
                continue;
            }

            _equivalentProducts.Add(new EquivalentProductItem
            {
                ProductId = other.Id,
                DisplayText = $"{other.Name} · {other.UnitCost.ToString("C", CultureInfo.CurrentCulture)}"
            });
        }
    }

    private async void ManageEquivalents_Click(object sender, RoutedEventArgs e)
    {
        if (_productId is not { } productId)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Save product first",
                "Equivalent products can be linked after the product has been saved.");
            return;
        }

        await using var db = App.Database.CreateContext();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.Id != productId)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var linkedIds = await GetEquivalentProductIdsAsync(db, productId);
        var panel = new StackPanel { Spacing = 4, MaxHeight = 420 };
        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        foreach (var product in products)
        {
            var row = new CheckBox
            {
                Content = $"{product.Name} · {product.UnitCost.ToString("C", CultureInfo.CurrentCulture)}",
                IsChecked = linkedIds.Contains(product.Id),
                Tag = product.Id
            };

            row.Checked += EquivalentChooser_Changed;
            row.Unchecked += EquivalentChooser_Changed;
            panel.Children.Add(row);
        }

        if (products.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Opacity = 0.7,
                Text = "No other products are available to link.",
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        var dialog = new ContentDialog
        {
            Title = "Equivalent products",
            Content = scroll,
            CloseButtonText = "Done",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
        await LoadEquivalentsAsync(productId);
    }

    private async void EquivalentChooser_Changed(object sender, RoutedEventArgs e)
    {
        if (_productId is not Guid productId
            || sender is not CheckBox { Tag: Guid otherId } cb
            || otherId == productId)
        {
            return;
        }

        var assign = cb.IsChecked == true;
        try
        {
            await using var db = App.Database.CreateContext();
            var (leftId, rightId) = ProductEquivalentHelper.OrderPair(productId, otherId);
            var existing = await db.ProductEquivalents.FindAsync(leftId, rightId);

            if (assign)
            {
                if (existing is null)
                {
                    db.ProductEquivalents.Add(new ProductEquivalent
                    {
                        ProductId = leftId,
                        EquivalentProductId = rightId
                    });
                    await db.SaveChangesAsync();
                }
            }
            else if (existing is not null)
            {
                db.ProductEquivalents.Remove(existing);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Update failed", ex.Message);
            _suppressEvents = true;
            cb.IsChecked = !assign;
            _suppressEvents = false;
        }
    }

    private static async Task<HashSet<Guid>> GetEquivalentProductIdsAsync(WorkCostsDbContext db, Guid productId)
    {
        var links = await db.ProductEquivalents
            .AsNoTracking()
            .Where(e => e.ProductId == productId || e.EquivalentProductId == productId)
            .ToListAsync();

        var ids = new HashSet<Guid>();
        foreach (var link in links)
        {
            ids.Add(link.ProductId == productId ? link.EquivalentProductId : link.ProductId);
        }

        return ids;
    }

    private sealed class EquivalentProductItem
    {
        public Guid ProductId { get; init; }
        public string DisplayText { get; init; } = "";
    }

    private sealed class JobOption(Guid id, string name) : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isEnabled = true;

        public Guid Id { get; } = id;
        public string Name { get; } = name;

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

    private void ExtraNumber_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        RaiseChanged();

    private void TechnologyBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RaiseChanged();

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
}

public sealed record ProductEditorValues(
    string Name,
    string Vendor,
    string Source,
    string Manufacturer,
    string ManufacturerReference,
    string Ean,
    string Variation,
    string OemEquivalent,
    string Url,
    decimal UnitCost,
    Guid CategoryId,
    string CategoryName,
    bool IsAllJobs,
    string PricePoint,
    IReadOnlyList<Guid> JobIds,
    byte[]? ImageBlob,
    string? ImageContentType,
    string ExtraYaml = "");
