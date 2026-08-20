using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using WorkCosts.Controls;
using WorkCosts.Data;
using WorkCosts.Helpers;
using WorkCosts.Models;
using WorkCosts.Services;

namespace WorkCosts.Pages;

public sealed partial class ProductsPage : Page
{
    public ObservableCollection<CategoryFilterItem> CategoryFilters { get; } = new();
    public ObservableCollection<PricePointFilterItem> PricePointFilters { get; } = new();
    public ObservableCollection<JobFilterPillItem> JobFilters { get; } = new();
    public ObservableCollection<AssignableProductRow> AssignmentProducts { get; } = new();

    private readonly List<AssignableProductRow> _assignmentProductsAll = [];
    private readonly ObservableCollection<ProductRow> _filterProducts = [];
    private readonly ObservableCollection<ProductRow> _allProducts = [];
    private readonly List<ProductRow> _productRowsFlat = [];
    private List<Category> _categories = [];
    private List<Job> _jobs = [];
    private bool _filtersReady;
    private bool _suppressSelection;
    private bool _suppressDetailEvents;
    private Guid? _selectedId;
    private int _persistVersion;
    private bool _addOverlayOpen;
    private Guid? _overwriteProductId;
    private Guid? _viewProductId;
    private bool _addViewExisting;
    private bool _continuingFromUrl;
    private TaskCompletionSource<ContentDialogResult>? _existingChoice;

    public ProductsPage()
    {
        try
        {
            InitializeComponent();
            FilterProductList.ItemsSource = _filterProducts;
            AllProductList.ItemsSource = _allProducts;
            DetailEditor.ValuesChanged += DetailEditor_ValuesChanged;
            AddEditor.ShouldContinueLoadingUrlAsync = async url =>
                await ResolveExistingUrlAsync(url) == ExistingUrlDecision.Fetch;
            AddEditor.DirtyChanged += AddEditor_DirtyChanged;
            AddEditor.ProductImageStateChanged += (_, _) => UpdateAddSaveEnabled();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProductsPage ctor failed: {ex}");
            throw;
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadFiltersAsync();
            DetailEditor.SetLookups(_categories, _jobs);
            AddEditor.SetLookups(_categories, _jobs);
            await LoadAsync();
            InputToolTip.Bind(AddUrlBox);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProductsPage.Loaded failed: {ex}");
            await DialogHelper.ShowMessageAsync(
                XamlRoot,
                "Products page failed",
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

    private async Task LoadFiltersAsync()
    {
        _filtersReady = false;
        await using var db = App.Database.CreateContext();
        _categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        _jobs = await db.Jobs.OrderBy(j => j.Name).ToListAsync();

        JobFilters.Clear();
        var allJob = _jobs.FirstOrDefault(j => j.Id == DbInitializer.AllJobId)
            ?? _jobs.FirstOrDefault(j => j.Name.Equals("All", StringComparison.OrdinalIgnoreCase));
        if (allJob is not null)
        {
            JobFilters.Add(new JobFilterPillItem
            {
                JobId = allJob.Id,
                Name = allJob.Name,
                IsAllOption = true,
                IsSelected = true
            });
        }

        foreach (var job in _jobs.Where(j => allJob is null || j.Id != allJob.Id))
        {
            JobFilters.Add(new JobFilterPillItem
            {
                JobId = job.Id,
                Name = job.Name
            });
        }

        CategoryFilters.Clear();
        foreach (var category in _categories)
        {
            CategoryFilters.Add(new CategoryFilterItem
            {
                Id = category.Id,
                Name = category.Name
            });
        }

        PricePointFilters.Clear();
        foreach (var option in ProductPricePoints.Options)
        {
            PricePointFilters.Add(new PricePointFilterItem
            {
                Value = option.Value,
                Name = option.Label
            });
        }

        _filtersReady = true;
    }

    private IReadOnlyCollection<Guid> GetSelectedCategoryIds() =>
        CategoryFilters.Where(c => c.IsSelected).Select(c => c.Id).ToList();

    private IReadOnlyCollection<string> GetSelectedPricePoints() =>
        PricePointFilters.Where(p => p.IsSelected).Select(p => p.Value).ToList();

    private JobFilterPillItem? AllJobPill =>
        JobFilters.FirstOrDefault(j => j.IsAllOption);

    private bool IsAllJobsFilterActive() =>
        AllJobPill?.IsSelected == true;

    private IReadOnlyCollection<Guid> GetSelectedJobIds() =>
        JobFilters.Where(j => !j.IsAllOption && j.IsSelected).Select(j => j.JobId).ToList();

    private bool HasActiveAssignmentFilters() =>
        GetSelectedCategoryIds().Count > 0 || GetSelectedJobIds().Count > 0;

    private async void CategoryPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_filtersReady)
            return;

        if (GetCategoryFilter(sender) is not CategoryFilterItem pill)
            return;

        pill.IsSelected = !pill.IsSelected;
        _selectedId = null;
        await LoadAsync(selectFirstIfNone: false);
    }

    private async void PricePointPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_filtersReady)
            return;

        if (GetPricePointFilter(sender) is not PricePointFilterItem pill)
            return;

        pill.IsSelected = !pill.IsSelected;
        _selectedId = null;
        await LoadAsync(selectFirstIfNone: false);
    }

    private static CategoryFilterItem? GetCategoryFilter(object sender) =>
        sender switch
        {
            FrameworkElement { DataContext: CategoryFilterItem vm } => vm,
            FrameworkElement { Tag: CategoryFilterItem vm } => vm,
            _ => null
        };

    private static PricePointFilterItem? GetPricePointFilter(object sender) =>
        sender switch
        {
            FrameworkElement { DataContext: PricePointFilterItem vm } => vm,
            FrameworkElement { Tag: PricePointFilterItem vm } => vm,
            _ => null
        };

    private async void RefreshFilters_Click(object sender, RoutedEventArgs e)
    {
        if (!_filtersReady)
            return;

        await LoadAsync(_selectedId);
        await UpdateAssignmentPanelAsync();
    }

    private async void JobPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_filtersReady)
            return;

        if (GetJobPill(sender) is not JobFilterPillItem pill)
            return;

        if (pill.IsAllOption)
        {
            foreach (var jobPill in JobFilters)
                jobPill.IsSelected = jobPill.IsAllOption;
        }
        else
        {
            if (AllJobPill is not null)
                AllJobPill.IsSelected = false;

            pill.IsSelected = !pill.IsSelected;

            if (!JobFilters.Any(j => !j.IsAllOption && j.IsSelected) && AllJobPill is not null)
                AllJobPill.IsSelected = true;
        }

        _selectedId = null;
        await LoadAsync(selectFirstIfNone: false);
    }

    private static JobFilterPillItem? GetJobPill(object sender) =>
        sender switch
        {
            FrameworkElement { DataContext: JobFilterPillItem vm } => vm,
            FrameworkElement { Tag: JobFilterPillItem vm } => vm,
            _ => null
        };

    private async Task LoadAsync(Guid? selectId = null, bool selectFirstIfNone = true)
    {
        await using var db = App.Database.CreateContext();
        var query = db.Products
            .Include(i => i.Category)
            .Include(i => i.ProductJobs)
            .ThenInclude(ij => ij.Job)
            .AsQueryable();

        var selectedCategoryIds = GetSelectedCategoryIds();
        if (selectedCategoryIds.Count > 0)
            query = query.Where(i => selectedCategoryIds.Contains(i.CategoryId));

        var selectedPricePoints = GetSelectedPricePoints();
        if (selectedPricePoints.Count > 0)
            query = query.Where(i => selectedPricePoints.Contains(i.PricePoint));

        var products = await query.OrderBy(i => i.Name).ToListAsync();
        var selectedJobIds = GetSelectedJobIds();
        var allJobsActive = IsAllJobsFilterActive();

        var filterRows = new List<ProductRow>();
        var allRows = new List<ProductRow>();
        var filterGroupTitle = "";
        var showFilterGroup = false;
        var showAllGroup = false;
        var showNoDirectJobMessage = false;
        var jobFilterActive = !allJobsActive && selectedJobIds.Count > 0;

        if (allJobsActive || selectedJobIds.Count == 0)
        {
            filterGroupTitle = "Assigned to jobs";
            foreach (var product in products.Where(p => !p.IsAllJobs))
                filterRows.Add(await ProductRow.CreateAsync(product));
            foreach (var product in products.Where(p => p.IsAllJobs))
                allRows.Add(await ProductRow.CreateAsync(product));
            showFilterGroup = filterRows.Count > 0;
            showAllGroup = allRows.Count > 0;
            FilterGroupHeader.Text = filterGroupTitle;
        }
        else
        {
            var jobIdSet = selectedJobIds.ToHashSet();
            filterGroupTitle = string.Join(", ",
                JobFilters.Where(j => j.IsSelected && !j.IsAllOption).Select(j => j.Name));

            var directProducts = products
                .Where(p => !p.IsAllJobs && p.ProductJobs.Any(pj => jobIdSet.Contains(pj.JobId)))
                .ToList();
            var generalProducts = products.Where(p => p.IsAllJobs).ToList();
            var hasDirectAssignments = directProducts.Count > 0;

            foreach (var product in directProducts)
                filterRows.Add(await ProductRow.CreateAsync(product));

            if (hasDirectAssignments)
            {
                var generalInFilter = new HashSet<Guid>();
                foreach (var product in generalProducts)
                {
                    filterRows.Add(await ProductRow.CreateAsync(product, showGeneralBadge: true));
                    generalInFilter.Add(product.Id);
                }

                foreach (var product in generalProducts.Where(p => !generalInFilter.Contains(p.Id)))
                    allRows.Add(await ProductRow.CreateAsync(product));
            }
            else
            {
                showNoDirectJobMessage = true;
                foreach (var product in generalProducts)
                    allRows.Add(await ProductRow.CreateAsync(product));
            }

            showFilterGroup = jobFilterActive;
            showAllGroup = allRows.Count > 0;
            FilterGroupHeader.Text = filterGroupTitle;
            NoDirectJobProductsText.Text = selectedJobIds.Count == 1
                ? "No products are assigned directly to this job."
                : "No products are assigned directly to these jobs.";
        }

        _suppressSelection = true;
        _filterProducts.Clear();
        _allProducts.Clear();
        _productRowsFlat.Clear();

        foreach (var row in filterRows)
            _filterProducts.Add(row);
        foreach (var row in allRows)
            _allProducts.Add(row);

        _productRowsFlat.AddRange(filterRows);
        _productRowsFlat.AddRange(allRows);

        FilterGroupHeader.Visibility = showFilterGroup ? Visibility.Visible : Visibility.Collapsed;
        NoDirectJobProductsText.Visibility = showNoDirectJobMessage ? Visibility.Visible : Visibility.Collapsed;
        FilterProductList.Visibility = showFilterGroup && filterRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AllGroupHeader.Visibility = showAllGroup ? Visibility.Visible : Visibility.Collapsed;
        AllProductList.Visibility = showAllGroup ? Visibility.Visible : Visibility.Collapsed;

        EmptyText.Visibility = _productRowsFlat.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var targetId = selectId ?? _selectedId;
        ProductRow? target = null;
        if (targetId is Guid id)
            target = _productRowsFlat.FirstOrDefault(p => p.Id == id);
        if (target is null && selectFirstIfNone)
            target = _productRowsFlat.FirstOrDefault();

        FilterProductList.SelectedItem = null;
        AllProductList.SelectedItem = null;
        if (target is not null)
        {
            if (_filterProducts.Contains(target))
                FilterProductList.SelectedItem = target;
            else if (_allProducts.Contains(target))
                AllProductList.SelectedItem = target;
        }

        _suppressSelection = false;

        if (target is null)
        {
            _selectedId = null;
            ShowEmptyRightPanel();
        }
        else
            await ShowDetailAsync(target);

        await UpdateAssignmentPanelAsync();
    }

    private static bool NeedsAssignment(Product product, IReadOnlyCollection<Guid> categoryIds, IReadOnlyCollection<Guid> jobIds)
    {
        if (categoryIds.Count > 0 && !categoryIds.Contains(product.CategoryId))
            return true;

        if (jobIds.Count > 0 && !product.IsAllJobs
            && !product.ProductJobs.Any(pj => jobIds.Contains(pj.JobId)))
            return true;

        return false;
    }

    private void ClearAssignmentMode()
    {
        AssignmentPanel.Visibility = Visibility.Collapsed;
        AssignmentFilterBox.Text = string.Empty;
        _assignmentProductsAll.Clear();
        AssignmentProducts.Clear();
    }

    private async Task UpdateAssignmentPanelAsync()
    {
        if (!HasActiveAssignmentFilters())
        {
            ClearAssignmentMode();
            if (_selectedId is null)
            {
                NoSelectionText.Visibility = Visibility.Visible;
            }

            return;
        }

        var filterNames = CategoryFilters.Where(c => c.IsSelected).Select(c => c.Name)
            .Concat(JobFilters.Where(j => j.IsSelected && !j.IsAllOption).Select(j => j.Name))
            .ToList();
        AssignmentTitle.Text =
            $"Not matching: {string.Join(", ", filterNames)} — check to assign all active filters";

        if (_selectedId is null)
        {
            NoSelectionText.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Collapsed;
            AssignmentPanel.Visibility = Visibility.Visible;
        }

        await LoadAssignmentProductsAsync();
    }

    private async Task LoadAssignmentProductsAsync()
    {
        if (!HasActiveAssignmentFilters())
            return;

        var categoryIds = GetSelectedCategoryIds();
        var jobIds = GetSelectedJobIds();

        await using var db = App.Database.CreateContext();
        var products = await db.Products
            .Include(p => p.Category)
            .Include(p => p.ProductJobs)
            .OrderBy(p => p.Name)
            .ToListAsync();

        _assignmentProductsAll.Clear();
        foreach (var product in products.Where(p => NeedsAssignment(p, categoryIds, jobIds)))
        {
            _assignmentProductsAll.Add(new AssignableProductRow
            {
                ProductId = product.Id,
                Name = product.Name,
                Detail = BuildAssignmentDetail(product)
            });
        }

        ApplyAssignmentTextFilter();
    }

    private void ApplyAssignmentTextFilter()
    {
        var text = AssignmentFilterBox.Text.Trim();
        IEnumerable<AssignableProductRow> rows = _assignmentProductsAll;
        if (text.Length > 2)
        {
            rows = rows.Where(r =>
                r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Detail.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        AssignmentProducts.Clear();
        foreach (var row in rows)
            AssignmentProducts.Add(row);
    }

    private void AssignmentFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!HasActiveAssignmentFilters())
            return;

        ApplyAssignmentTextFilter();
    }

    private void AssignmentFilterClear_Click(object sender, RoutedEventArgs e)
    {
        AssignmentFilterBox.Text = string.Empty;
    }

    private static string BuildAssignmentDetail(Product product)
    {
        var category = product.Category?.Name ?? "—";
        var jobs = product.IsAllJobs
            ? "All jobs"
            : string.Join(", ", product.ProductJobs.Select(pj => pj.Job?.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        if (string.IsNullOrWhiteSpace(jobs))
            jobs = "No jobs";
        return $"{category} · {product.UnitCost:C} · {jobs}";
    }

    private async void AssignProduct_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { IsChecked: true, DataContext: AssignableProductRow row })
            return;

        try
        {
            await using var db = App.Database.CreateContext();
            var entity = await db.Products
                .Include(p => p.ProductJobs)
                .FirstOrDefaultAsync(p => p.Id == row.ProductId);
            if (entity is null)
                return;

            var categoryIds = GetSelectedCategoryIds();
            if (categoryIds.Count > 0 && !categoryIds.Contains(entity.CategoryId))
                entity.CategoryId = categoryIds.First();

            foreach (var jobId in GetSelectedJobIds())
            {
                if (!entity.IsAllJobs && !entity.ProductJobs.Any(pj => pj.JobId == jobId))
                {
                    db.ProductJobs.Add(new ProductJob
                    {
                        ProductId = entity.Id,
                        JobId = jobId
                    });
                }
            }

            await db.SaveChangesAsync();
            _assignmentProductsAll.RemoveAll(r => r.ProductId == row.ProductId);
            AssignmentProducts.Remove(row);
            await LoadAsync(_selectedId);
            await UpdateAssignmentPanelAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Assign failed", ex.Message);
            if (sender is CheckBox cb)
                cb.IsChecked = false;
        }
    }

    private bool RemoveProductRow(ProductRow row)
    {
        var removed = _filterProducts.Remove(row) | _allProducts.Remove(row);
        if (removed)
            _productRowsFlat.Remove(row);
        return removed;
    }

    private ProductRow? SelectedItem =>
        _selectedId is Guid id ? _productRowsFlat.FirstOrDefault(p => p.Id == id) : null;

    private async void ProductList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
            return;

        if (sender is ListView list && list.SelectedItem is ProductRow row)
        {
            if (ReferenceEquals(list, FilterProductList))
                AllProductList.SelectedItem = null;
            else if (ReferenceEquals(list, AllProductList))
                FilterProductList.SelectedItem = null;

            await ShowDetailAsync(row);
        }
        else if (FilterProductList.SelectedItem is null && AllProductList.SelectedItem is null)
        {
            _selectedId = null;
            DetailPanel.Visibility = Visibility.Collapsed;
            await UpdateAssignmentPanelAsync();
            if (!HasActiveAssignmentFilters())
                ShowEmptyRightPanel();
        }
    }

    private void ShowEmptyRightPanel()
    {
        _selectedId = null;
        DetailPanel.Visibility = Visibility.Collapsed;
        if (HasActiveAssignmentFilters())
        {
            NoSelectionText.Visibility = Visibility.Collapsed;
            AssignmentPanel.Visibility = Visibility.Visible;
        }
        else
        {
            AssignmentPanel.Visibility = Visibility.Collapsed;
            NoSelectionText.Visibility = Visibility.Visible;
        }

        DetailTitle.Text = "Product details";
    }

    private void ShowEmptyDetail() => ShowEmptyRightPanel();

    private async Task ShowDetailAsync(ProductRow row)
    {
        _selectedId = row.Id;
        AssignmentPanel.Visibility = Visibility.Collapsed;
        NoSelectionText.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        UpdateDetailTitle(row.Name);

        _suppressDetailEvents = true;
        await DetailEditor.LoadAsync(row.ToProductSnapshot(), row.JobIds);
        _suppressDetailEvents = false;
    }

    private void UpdateDetailTitle(string name)
    {
        var trimmed = name.Trim();
        DetailTitle.Text = string.IsNullOrEmpty(trimmed)
            ? "Product details"
            : $"Product details - {trimmed}";
    }

    private void DetailEditor_ValuesChanged(object? sender, EventArgs e)
    {
        if (_suppressDetailEvents || SelectedItem is null)
        {
            return;
        }

        _ = PersistDetailAsync();
    }

    private async Task PersistDetailAsync()
    {
        if (SelectedItem is not ProductRow row)
        {
            return;
        }

        if (!DetailEditor.TryRead(out var values, out _))
        {
            return;
        }

        var version = ++_persistVersion;
        try
        {
            await using var db = App.Database.CreateContext();
            var entity = await db.Products.Include(p => p.ProductJobs).FirstOrDefaultAsync(p => p.Id == row.Id);
            if (entity is null)
            {
                if (version == _persistVersion)
                {
                    await DialogHelper.ShowMessageAsync(XamlRoot, "Not found", "This product no longer exists.");
                    await LoadAsync();
                }

                return;
            }

            ApplyValues(entity, values, db);
            await db.SaveChangesAsync();

            if (version != _persistVersion)
            {
                return;
            }

            await row.ApplyValuesAsync(values, _jobs);
            UpdateDetailTitle(values.Name);

            if (!MatchesCurrentFilter(row))
            {
                _suppressSelection = true;
                RemoveProductRow(row);
                _suppressSelection = false;
                EmptyText.Visibility = _productRowsFlat.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (_productRowsFlat.FirstOrDefault() is ProductRow next)
                {
                    if (_filterProducts.Contains(next))
                        FilterProductList.SelectedItem = next;
                    else
                        AllProductList.SelectedItem = next;
                }
                else
                {
                    ShowEmptyDetail();
                }
            }
            else
            {
                await LoadAsync(row.Id);
            }
        }
        catch (Exception ex)
        {
            if (version == _persistVersion)
            {
                await DialogHelper.ShowMessageAsync(XamlRoot, "Save failed", ex.Message);
            }
        }
    }

    private bool MatchesCurrentFilter(ProductRow row)
    {
        if (!MatchesCategoryFilter(row.CategoryId))
            return false;

        if (!MatchesPricePointFilter(row.PricePoint))
            return false;

        if (!IsAllJobsFilterActive())
        {
            var selectedJobIds = GetSelectedJobIds();
            if (selectedJobIds.Count > 0
                && !row.IsAllJobs
                && !row.JobIds.Any(selectedJobIds.Contains))
            {
                return false;
            }
        }

        return true;
    }

    private bool MatchesCategoryFilter(Guid categoryId)
    {
        var selectedCategoryIds = GetSelectedCategoryIds();
        return selectedCategoryIds.Count == 0 || selectedCategoryIds.Contains(categoryId);
    }

    private bool MatchesPricePointFilter(string pricePoint)
    {
        var selectedPricePoints = GetSelectedPricePoints();
        return selectedPricePoints.Count == 0 || selectedPricePoints.Contains(pricePoint);
    }

    private static void ApplyValues(Product entity, ProductEditorValues values, Data.WorkCostsDbContext db)
    {
        entity.Name = values.Name;
        entity.Vendor = values.Vendor;
        entity.Source = values.Source;
        entity.Manufacturer = values.Manufacturer;
        entity.ManufacturerReference = values.ManufacturerReference;
        entity.Ean = values.Ean;
        entity.Variation = values.Variation;
        entity.OemEquivalent = values.OemEquivalent;
        entity.ExtraYaml = values.ExtraYaml;
        entity.PricePoint = values.PricePoint;
        entity.Url = string.IsNullOrWhiteSpace(values.Url) ? string.Empty : ProductUrl.Normalize(values.Url);
        entity.UnitCost = values.UnitCost;
        entity.CategoryId = values.CategoryId;
        entity.IsAllJobs = values.IsAllJobs;
        entity.ImageBlob = values.ImageBlob;
        entity.ImageContentType = values.ImageContentType;

        db.ProductJobs.RemoveRange(entity.ProductJobs);
        foreach (var jobId in values.JobIds)
        {
            db.ProductJobs.Add(new ProductJob { ProductId = entity.Id, JobId = jobId });
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_addOverlayOpen)
        {
            if (AddUrlStage.Visibility == Visibility.Visible)
            {
                await ContinueFromUrlStageAsync();
            }

            return;
        }

        if (_categories.Count == 0)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "No categories",
                "Add at least one usage category before creating products.");
            return;
        }

        AddEditor.SetLookups(_categories, _jobs);
        ResetAddInteraction();
        AddEditor.LoadEmpty();
        AddUrlBox.Text = string.Empty;
        ShowAddUrlStage();
        await OpenAddOverlayAsync();
        AddUrlBox.Focus(FocusState.Programmatic);
    }

    private void ResetAddInteraction()
    {
        _overwriteProductId = null;
        _viewProductId = null;
        _addViewExisting = false;
        AddEditor.SetReadOnly(false);
        AddDetailsTitle.Text = "Add product";
        AddDetailsCancelButton.Visibility = Visibility.Visible;
        AddSaveButtons.Visibility = Visibility.Visible;
        AddViewButtons.Visibility = Visibility.Collapsed;
        AddViewSaveButton.Visibility = Visibility.Collapsed;
        UpdateAddSaveEnabled();
    }

    private void EnterViewExistingMode(Guid productId)
    {
        _addViewExisting = true;
        _overwriteProductId = null;
        _viewProductId = productId;
        AddDetailsTitle.Text = "Existing product";
        AddDetailsCancelButton.Visibility = Visibility.Collapsed;
        AddSaveButtons.Visibility = Visibility.Collapsed;
        AddViewButtons.Visibility = Visibility.Visible;
        UpdateViewSaveVisibility();
    }

    private void EnterOverwriteMode(Guid productId)
    {
        _addViewExisting = false;
        _overwriteProductId = productId;
        _viewProductId = null;
        AddEditor.SetReadOnly(false);
        AddDetailsTitle.Text = "Update product";
        AddDetailsCancelButton.Visibility = Visibility.Visible;
        AddSaveButtons.Visibility = Visibility.Visible;
        AddViewButtons.Visibility = Visibility.Collapsed;
        AddViewSaveButton.Visibility = Visibility.Collapsed;
    }

    private void AddEditor_DirtyChanged(object? sender, EventArgs e) => UpdateViewSaveVisibility();

    private void UpdateViewSaveVisibility()
    {
        var dirty = _addViewExisting && AddEditor.IsDirty;
        AddViewSaveButton.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        if (!_addViewExisting)
        {
            return;
        }

        if (dirty)
        {
            AddViewCloseButton.ClearValue(FrameworkElement.StyleProperty);
            if (FindAppStyle("AppAccentButtonStyle") is { } saveStyle)
            {
                AddViewSaveButton.Style = saveStyle;
            }
        }
        else if (FindAppStyle("AppAccentButtonStyle") is { } closeStyle)
        {
            AddViewCloseButton.Style = closeStyle;
        }
    }

    private Style? FindAppStyle(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var direct) && direct is Style style)
        {
            return style;
        }

        var themeKey = ActualTheme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => Application.Current.RequestedTheme == ApplicationTheme.Light ? "Light" : "Dark"
        };

        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var dictObj)
            && dictObj is ResourceDictionary dict
            && dict.TryGetValue(key, out var themed)
            && themed is Style themedStyle)
        {
            return themedStyle;
        }

        return null;
    }

    private void ShowAddUrlStage()
    {
        AddUrlStage.Visibility = Visibility.Visible;
        AddDetailsStage.Visibility = Visibility.Collapsed;
        AddExistingBanner.Visibility = Visibility.Collapsed;
        AddLoadStatus.Visibility = Visibility.Collapsed;
        AddUrlError.Visibility = Visibility.Collapsed;
        AddOverlay.VerticalAlignment = VerticalAlignment.Center;
        AddOverlay.MinHeight = 0;
    }

    private void ShowAddDetailsStage()
    {
        AddUrlStage.Visibility = Visibility.Collapsed;
        AddDetailsStage.Visibility = Visibility.Visible;
        AddOverlay.VerticalAlignment = VerticalAlignment.Stretch;
        AddOverlay.MinHeight = 420;
    }

    private async Task OpenAddOverlayAsync()
    {
        if (_addOverlayOpen)
        {
            return;
        }

        _addOverlayOpen = true;
        PageAddButton.IsEnabled = false;
        AddOverlay.Visibility = Visibility.Visible;
        AddOverlay.Opacity = 0;
        AddOverlayTransform.X = Math.Max(MasterDetailHost.ActualWidth, 320) * 0.25;

        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(slide, AddOverlayTransform);
        Storyboard.SetTargetProperty(slide, "X");
        Storyboard.SetTarget(fade, AddOverlay);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var sb = new Storyboard();
        sb.Children.Add(slide);
        sb.Children.Add(fade);
        var tcs = new TaskCompletionSource();
        sb.Completed += (_, _) => tcs.TrySetResult();
        sb.Begin();
        await tcs.Task;
    }

    private async Task CloseAddOverlayAsync()
    {
        _existingChoice?.TrySetResult(ContentDialogResult.None);
        if (!_addOverlayOpen)
        {
            return;
        }

        _addOverlayOpen = false;

        var slide = new DoubleAnimation
        {
            To = Math.Max(MasterDetailHost.ActualWidth, 320) * 0.25,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        Storyboard.SetTarget(slide, AddOverlayTransform);
        Storyboard.SetTargetProperty(slide, "X");
        Storyboard.SetTarget(fade, AddOverlay);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var sb = new Storyboard();
        sb.Children.Add(slide);
        sb.Children.Add(fade);
        var tcs = new TaskCompletionSource();
        sb.Completed += (_, _) => tcs.TrySetResult();
        sb.Begin();
        await tcs.Task;

        AddOverlay.Visibility = Visibility.Collapsed;
        ResetAddInteraction();
        ShowAddUrlStage();
        PageAddButton.IsEnabled = true;
    }

    private async void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_addOverlayOpen || DialogHelper.HasOpenDialog)
        {
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            await TryDiscardAddOverlayAsync();
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter && _addViewExisting)
        {
            e.Handled = true;
            if (AddEditor.IsDirty)
            {
                await SaveViewExistingAsync();
            }
            else
            {
                await CloseAddOverlayAsync();
            }
        }
    }

    private async void AddCancel_Click(object sender, RoutedEventArgs e) =>
        await TryDiscardAddOverlayAsync();

    private async void AddDetailsCancel_Click(object sender, RoutedEventArgs e) =>
        await TryDiscardAddOverlayAsync();

    private async void AddViewClose_Click(object sender, RoutedEventArgs e) =>
        await TryDiscardAddOverlayAsync();

    private async void AddViewSave_Click(object sender, RoutedEventArgs e) =>
        await SaveViewExistingAsync();

    private async Task SaveViewExistingAsync()
    {
        if (_viewProductId is not Guid existingId)
        {
            return;
        }

        if (!AddEditor.TryRead(out var values, out var error))
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Validation", error ?? "Invalid product.");
            return;
        }

        await using var db = App.Database.CreateContext();
        var entity = await db.Products
            .Include(p => p.ProductJobs)
            .FirstOrDefaultAsync(p => p.Id == existingId);
        if (entity is null)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Missing product",
                "That product is no longer in the library.");
            return;
        }

        ApplyValues(entity, values, db);
        await db.SaveChangesAsync();
        AddEditor.MarkClean();
        await LoadAsync(_selectedId);
    }

    private async Task TryDiscardAddOverlayAsync()
    {
        if (!_addOverlayOpen)
        {
            return;
        }

        if (_existingChoice is not null)
        {
            _existingChoice.TrySetResult(ContentDialogResult.None);
            return;
        }

        if (_addViewExisting || AddDetailsStage.Visibility != Visibility.Visible)
        {
            await CloseAddOverlayAsync();
            return;
        }

        if (DialogHelper.HasOpenDialog)
        {
            return;
        }

        bool discard;
        try
        {
            discard = await DialogHelper.ConfirmAsync(
                XamlRoot,
                "Discard changes?",
                "Close Add product and discard what you have entered?",
                "Yes",
                "No");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Discard confirm failed: {ex}");
            await CloseAddOverlayAsync();
            return;
        }

        if (discard)
        {
            await CloseAddOverlayAsync();
        }
    }

    private async void AddUrlBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await ContinueFromUrlStageAsync();
        }
    }

    private async void AddUrlContinue_Click(object sender, RoutedEventArgs e) =>
        await ContinueFromUrlStageAsync();

    private async void PasteHtml_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetCoercedAddUrl(out var url))
        {
            return;
        }

        var html = await TryReadClipboardHtmlAsync();
        if (html is null)
        {
            AddUrlError.Text = "Clipboard is empty.";
            AddUrlError.Visibility = Visibility.Visible;
            return;
        }

        await ContinueFromHtmlAsync(url, html);
    }

    private async void OpenHtmlFile_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetCoercedAddUrl(out var url))
        {
            return;
        }

        var html = await TryReadHtmlFileAsync();
        if (html is null)
        {
            return;
        }

        await ContinueFromHtmlAsync(url, html);
    }

    private bool TryGetCoercedAddUrl(out string url)
    {
        if (!ProductUrl.TryCoerceHttpUrl(AddUrlBox.Text, out url))
        {
            AddUrlError.Text = "Enter a valid http(s) product page URL.";
            AddUrlError.Visibility = Visibility.Visible;
            AddUrlBox.Focus(FocusState.Programmatic);
            return false;
        }

        AddUrlError.Visibility = Visibility.Collapsed;
        AddUrlBox.Text = url;
        return true;
    }

    private async Task<string?> TryReadClipboardHtmlAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content is null || !content.Contains(StandardDataFormats.Text))
            {
                return null;
            }

            var text = await content.GetTextAsync();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryReadHtmlFileAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        BindPickerToAppWindow(picker);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".html");
        picker.FileTypeFilter.Add(".htm");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        try
        {
            var html = await File.ReadAllTextAsync(file.Path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(html))
            {
                AddUrlError.Text = "The HTML file is empty or could not be read.";
                AddUrlError.Visibility = Visibility.Visible;
                return null;
            }

            return html;
        }
        catch (Exception ex)
        {
            AddUrlError.Text = string.IsNullOrWhiteSpace(ex.Message)
                ? "The HTML file is empty or could not be read."
                : ex.Message;
            AddUrlError.Visibility = Visibility.Visible;
            return null;
        }
    }

    private static void BindPickerToAppWindow(object picker)
    {
        if (App.MainAppWindow is null)
        {
            throw new InvalidOperationException("Main window is not available.");
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async Task ContinueFromUrlStageAsync()
    {
        if (_continuingFromUrl)
        {
            return;
        }

        _continuingFromUrl = true;
        try
        {
            if (!TryGetCoercedAddUrl(out var url))
            {
                return;
            }

            StartupLog.Write($"Add product continue: {url}");

            ShowAddDetailsStage();
            SetAddLoadStatus("Checking library…");
            await SlideDetailsInAsync();

            var decision = await ResolveExistingUrlAsync(url);
            if (decision == ExistingUrlDecision.Abort)
            {
                return;
            }

            if (decision == ExistingUrlDecision.ShowExisting)
            {
                SetAddLoadStatus(null);
                return;
            }

            SetAddLoadStatus("Loading product page…");
            await AddEditor.BeginWithUrlAsync(url);
            SetAddLoadStatus(null);
        }
        catch (Exception ex)
        {
            StartupLog.Write("ContinueFromUrlStageAsync failed", ex);
            ShowAddUrlStage();
            AddUrlError.Text = ex.Message;
            AddUrlError.Visibility = Visibility.Visible;
        }
        finally
        {
            _continuingFromUrl = false;
        }
    }

    private async Task ContinueFromHtmlAsync(string url, string html)
    {
        if (_continuingFromUrl)
        {
            return;
        }

        _continuingFromUrl = true;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var pageUri)
                || !ProductImageService.IsUsablePageHtml(html, pageUri))
            {
                AddUrlError.Text = Uri.TryCreate(url, UriKind.Absolute, out var messageUri)
                    ? ProductImageService.FormatUnusablePageMessage(messageUri, html)
                    : "Enter a valid http(s) product page URL.";
                AddUrlError.Visibility = Visibility.Visible;
                return;
            }

            ShowAddDetailsStage();
            SetAddLoadStatus("Checking library…");
            await SlideDetailsInAsync();

            var decision = await ResolveExistingUrlAsync(url);
            if (decision == ExistingUrlDecision.Abort)
            {
                return;
            }

            if (decision == ExistingUrlDecision.ShowExisting)
            {
                SetAddLoadStatus(null);
                UpdateAddSaveEnabled();
                return;
            }

            SetAddLoadStatus("Reading pasted HTML…");
            await AddEditor.BeginWithHtmlAsync(url, html);
            SetAddLoadStatus(null);
            UpdateAddSaveEnabled();
        }
        catch (Exception ex)
        {
            StartupLog.Write("ContinueFromHtmlAsync failed", ex);
            ShowAddUrlStage();
            AddUrlError.Text = ex.Message;
            AddUrlError.Visibility = Visibility.Visible;
        }
        finally
        {
            _continuingFromUrl = false;
        }
    }

    private void UpdateAddSaveEnabled()
    {
        var enabled = !AddEditor.RequiresProductImage || AddEditor.HasProductImage;
        AddSaveButton.IsEnabled = enabled;
        AddSaveAndCloseButton.IsEnabled = enabled;
    }

    private void SetAddLoadStatus(string? message)
    {
        var text = message?.Trim() ?? string.Empty;
        AddLoadStatus.Text = text;
        AddLoadStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddExistingOverwrite_Click(object sender, RoutedEventArgs e) =>
        _existingChoice?.TrySetResult(ContentDialogResult.Primary);

    private void AddExistingKeep_Click(object sender, RoutedEventArgs e) =>
        _existingChoice?.TrySetResult(ContentDialogResult.Secondary);

    private void AddExistingCancel_Click(object sender, RoutedEventArgs e) =>
        _existingChoice?.TrySetResult(ContentDialogResult.None);

    private enum ExistingUrlDecision
    {
        Fetch,
        ShowExisting,
        Abort
    }

    private async Task<ExistingUrlDecision> ResolveExistingUrlAsync(string url)
    {
        var existing = await FindProductByUrlAsync(url);
        if (existing is null)
        {
            if (_overwriteProductId is not null && !_addViewExisting)
            {
                ResetAddInteraction();
            }

            return ExistingUrlDecision.Fetch;
        }

        if (_overwriteProductId == existing.Value.Product.Id && !_addViewExisting)
        {
            return ExistingUrlDecision.Fetch;
        }

        var name = string.IsNullOrWhiteSpace(existing.Value.Product.Name)
            ? "this product"
            : $"'{existing.Value.Product.Name}'";
        var choice = await PromptExistingUrlAsync(name);
        if (choice == ContentDialogResult.Primary)
        {
            EnterOverwriteMode(existing.Value.Product.Id);
            return ExistingUrlDecision.Fetch;
        }

        if (choice == ContentDialogResult.Secondary)
        {
            await AddEditor.LoadExistingAsync(existing.Value.Product, existing.Value.JobIds);
            EnterViewExistingMode(existing.Value.Product.Id);
            return ExistingUrlDecision.ShowExisting;
        }

        await CloseAddOverlayAsync();
        return ExistingUrlDecision.Abort;
    }

    private async Task<ContentDialogResult> PromptExistingUrlAsync(string name)
    {
        AddExistingMessage.Text =
            $"This URL is already stored as {name}. Overwrite that product with a fresh import?";
        AddExistingBanner.Visibility = Visibility.Visible;
        SetAddLoadStatus(null);
        _existingChoice = new TaskCompletionSource<ContentDialogResult>();
        var choice = await _existingChoice.Task;
        AddExistingBanner.Visibility = Visibility.Collapsed;
        _existingChoice = null;
        return choice;
    }

    private async Task<(Product Product, List<Guid> JobIds)?> FindProductByUrlAsync(string url)
    {
        await using var db = App.Database.CreateContext();
        var candidates = await db.Products
            .AsNoTracking()
            .Where(p => p.Url != "")
            .Select(p => new { p.Id, p.Url })
            .ToListAsync();
        var matchId = candidates.FirstOrDefault(p => ProductUrl.Same(p.Url, url))?.Id;
        if (matchId is not Guid id)
        {
            return null;
        }

        var match = await db.Products
            .AsNoTracking()
            .Include(p => p.ProductJobs)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (match is null)
        {
            return null;
        }

        return (match, match.ProductJobs.Select(j => j.JobId).ToList());
    }

    private async Task SlideDetailsInAsync()
    {
        AddDetailsStage.Opacity = 0;
        AddDetailsStage.RenderTransform = new TranslateTransform { X = 48 };
        var transform = (TranslateTransform)AddDetailsStage.RenderTransform;

        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(slide, transform);
        Storyboard.SetTargetProperty(slide, "X");
        Storyboard.SetTarget(fade, AddDetailsStage);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var sb = new Storyboard();
        sb.Children.Add(slide);
        sb.Children.Add(fade);
        var tcs = new TaskCompletionSource();
        sb.Completed += (_, _) => tcs.TrySetResult();
        sb.Begin();
        await Task.WhenAny(tcs.Task, Task.Delay(600));
        AddDetailsStage.Opacity = 1;
        transform.X = 0;
    }

    private async void AddSave_Click(object sender, RoutedEventArgs e) =>
        await SaveNewProductAsync(closeAfter: false);

    private async void AddSaveAndClose_Click(object sender, RoutedEventArgs e) =>
        await SaveNewProductAsync(closeAfter: true);

    private async Task SaveNewProductAsync(bool closeAfter)
    {
        if (!AddEditor.TryRead(out var values, out var error))
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Validation", error ?? "Invalid product.");
            return;
        }

        var duplicate = await FindProductByUrlAsync(values.Url);
        if (duplicate is not null && duplicate.Value.Product.Id != _overwriteProductId)
        {
            var name = string.IsNullOrWhiteSpace(duplicate.Value.Product.Name)
                ? "an existing product"
                : $"'{duplicate.Value.Product.Name}'";
            var overwrite = await DialogHelper.ConfirmAsync(
                XamlRoot,
                "Product already in library",
                $"This URL is already stored as {name}. Overwrite that product?",
                "Overwrite",
                "Cancel");
            if (!overwrite)
            {
                return;
            }

            _overwriteProductId = duplicate.Value.Product.Id;
        }

        await using var db = App.Database.CreateContext();
        Product product;
        if (_overwriteProductId is Guid existingId)
        {
            var entity = await db.Products
                .Include(p => p.ProductJobs)
                .FirstOrDefaultAsync(p => p.Id == existingId);
            if (entity is null)
            {
                product = CreateProductFromValues(values);
                db.Products.Add(product);
                foreach (var jobId in values.JobIds)
                {
                    db.ProductJobs.Add(new ProductJob { ProductId = product.Id, JobId = jobId });
                }
            }
            else
            {
                ApplyValues(entity, values, db);
                product = entity;
            }
        }
        else
        {
            product = CreateProductFromValues(values);
            db.Products.Add(product);
            foreach (var jobId in values.JobIds)
            {
                db.ProductJobs.Add(new ProductJob { ProductId = product.Id, JobId = jobId });
            }
        }

        await db.SaveChangesAsync();

        if (closeAfter)
        {
            await CloseAddOverlayAsync();
            await LoadAsync(product.Id);
        }
        else
        {
            await LoadAsync(_selectedId);
            ResetAddInteraction();
            AddEditor.LoadEmpty();
            AddUrlBox.Text = string.Empty;
            ShowAddUrlStage();
            AddUrlBox.Focus(FocusState.Programmatic);
        }
    }

    private static Product CreateProductFromValues(ProductEditorValues values) => new()
    {
        Name = values.Name,
        Vendor = values.Vendor,
        Source = values.Source,
        Manufacturer = values.Manufacturer,
        ManufacturerReference = values.ManufacturerReference,
        Ean = values.Ean,
        Variation = values.Variation,
        OemEquivalent = values.OemEquivalent,
        ExtraYaml = values.ExtraYaml,
        PricePoint = values.PricePoint,
        Url = string.IsNullOrWhiteSpace(values.Url) ? string.Empty : ProductUrl.Normalize(values.Url),
        UnitCost = values.UnitCost,
        CategoryId = values.CategoryId,
        IsAllJobs = values.IsAllJobs,
        ImageBlob = values.ImageBlob,
        ImageContentType = values.ImageContentType
    };

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is not ProductRow row)
        {
            return;
        }

        var confirmed = await DialogHelper.ConfirmYesNoAsync(
            XamlRoot,
            "Delete product",
            $"Delete '{row.Name}'? This also removes it from jobs and work jobs.");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await using var db = App.Database.CreateContext();
            await ProductCommands.DeleteAsync(db, row.Id);
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Delete failed", ex.Message);
            return;
        }

        _selectedId = null;
        _persistVersion++;
        await LoadAsync();
    }

    private sealed class ProductRow : INotifyPropertyChanged
    {
        private readonly bool _showGeneralBadge;
        private string _name;
        private string _detail;
        private BitmapImage? _thumbnail;
        private Guid _categoryId;
        private bool _isAllJobs;
        private List<Guid> _jobIds;
        private string _vendor;
        private string _source;
        private string _manufacturer;
        private string _manufacturerReference;
        private string _ean;
        private string _variation;
        private string _oemEquivalent;
        private string _extraYaml;
        private string _pricePoint;
        private string _url;
        private decimal _unitCost;
        private byte[]? _imageBlob;
        private string? _imageContentType;

        private ProductRow(Product product, BitmapImage? thumbnail, bool showGeneralBadge = false)
        {
            Id = product.Id;
            _showGeneralBadge = showGeneralBadge;
            _name = product.Name;
            _categoryId = product.CategoryId;
            _isAllJobs = product.IsAllJobs;
            _jobIds = product.ProductJobs.Select(pj => pj.JobId).ToList();
            _vendor = product.Vendor;
            _source = product.Source;
            _manufacturer = product.Manufacturer;
            _manufacturerReference = product.ManufacturerReference;
            _ean = product.Ean;
            _variation = product.Variation;
            _oemEquivalent = product.OemEquivalent;
            _extraYaml = product.ExtraYaml ?? string.Empty;
            _pricePoint = product.PricePoint;
            _url = product.Url;
            _unitCost = product.UnitCost;
            _imageBlob = product.ImageBlob;
            _imageContentType = product.ImageContentType;
            _thumbnail = thumbnail;
            _detail = BuildDetail(product.Category?.Name, product);
        }

        public static async Task<ProductRow> CreateAsync(Product product, bool showGeneralBadge = false)
        {
            var thumb = await ProductImagePicker.ToBitmapAsync(product.ImageBlob);
            return new ProductRow(product, thumb, showGeneralBadge);
        }

        public Guid Id { get; }

        public Visibility GeneralBadgeVisible =>
            _showGeneralBadge ? Visibility.Visible : Visibility.Collapsed;

        public string Name
        {
            get => _name;
            private set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                OnPropertyChanged();
            }
        }

        public string Detail
        {
            get => _detail;
            private set
            {
                if (_detail == value)
                {
                    return;
                }

                _detail = value;
                OnPropertyChanged();
            }
        }

        public BitmapImage? Thumbnail
        {
            get => _thumbnail;
            private set
            {
                if (ReferenceEquals(_thumbnail, value))
                {
                    return;
                }

                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        public Guid CategoryId => _categoryId;
        public string PricePoint => _pricePoint;
        public bool IsAllJobs => _isAllJobs;
        public IReadOnlyList<Guid> JobIds => _jobIds;

        public Product ToProductSnapshot() => new()
        {
            Id = Id,
            Name = _name,
            Vendor = _vendor,
            Source = _source,
            Manufacturer = _manufacturer,
            ManufacturerReference = _manufacturerReference,
            Ean = _ean,
            Variation = _variation,
            OemEquivalent = _oemEquivalent,
            ExtraYaml = _extraYaml,
            PricePoint = _pricePoint,
            Url = _url,
            UnitCost = _unitCost,
            CategoryId = _categoryId,
            IsAllJobs = _isAllJobs,
            ImageBlob = _imageBlob,
            ImageContentType = _imageContentType
        };

        public async Task ApplyValuesAsync(ProductEditorValues values, IReadOnlyList<Job> allJobs)
        {
            Name = values.Name;
            _vendor = values.Vendor;
            _source = values.Source;
            _manufacturer = values.Manufacturer;
            _manufacturerReference = values.ManufacturerReference;
            _ean = values.Ean;
            _variation = values.Variation;
            _oemEquivalent = values.OemEquivalent;
            _extraYaml = values.ExtraYaml;
            _pricePoint = values.PricePoint;
            _url = values.Url;
            _unitCost = values.UnitCost;
            _categoryId = values.CategoryId;
            _isAllJobs = values.IsAllJobs;
            _jobIds = values.JobIds.ToList();
            _imageBlob = values.ImageBlob;
            _imageContentType = values.ImageContentType;
            Thumbnail = await ProductImagePicker.ToBitmapAsync(_imageBlob);

            var assigned = string.Join(", ", allJobs.Where(j => values.JobIds.Contains(j.Id)).Select(j => j.Name));
            var jobNames = values.IsAllJobs
                ? string.IsNullOrWhiteSpace(assigned) ? "All jobs" : $"All jobs · {assigned}"
                : assigned;
            if (string.IsNullOrWhiteSpace(jobNames))
            {
                jobNames = "No jobs";
            }

            Detail = BuildDetail(
                values.CategoryName,
                values.Source,
                values.Vendor,
                values.Manufacturer,
                values.ManufacturerReference,
                values.Ean,
                values.Variation,
                values.UnitCost,
                jobNames);
        }

        private static string BuildDetail(string? categoryName, Product product)
        {
            var assigned = string.Join(", ", product.ProductJobs.Select(ij => ij.Job?.Name).Where(n => n is not null));
            var jobs = product.IsAllJobs
                ? string.IsNullOrWhiteSpace(assigned) ? "All jobs" : $"All jobs · {assigned}"
                : assigned;
            if (string.IsNullOrWhiteSpace(jobs))
            {
                jobs = "No jobs";
            }

            return BuildDetail(
                categoryName,
                product.Source,
                product.Vendor,
                product.Manufacturer,
                product.ManufacturerReference,
                product.Ean,
                product.Variation,
                product.UnitCost,
                jobs);
        }

        private static string BuildDetail(
            string? categoryName,
            string source,
            string vendor,
            string manufacturer,
            string manufacturerReference,
            string ean,
            string variation,
            decimal unitCost,
            string jobs)
        {
            vendor = ProductVendorHelper.FormatBreadcrumb(source, vendor);
            if (vendor == "—")
            {
                vendor = "No vendor";
            }
            var mfr = string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer;
            var mfrRef = string.IsNullOrWhiteSpace(manufacturerReference)
                ? null
                : $"Mfr {manufacturerReference}";
            var eanText = string.IsNullOrWhiteSpace(ean) ? null : $"EAN {ean}";
            var variationText = string.IsNullOrWhiteSpace(variation) ? null : variation;
            return string.Join(" · ", new[]
            {
                categoryName ?? "—",
                vendor,
                mfr,
                mfrRef,
                eanText,
                variationText,
                unitCost.ToString("C", CultureInfo.CurrentCulture),
                jobs
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class PricePointFilterItem : INotifyPropertyChanged
{
    private static readonly SolidColorBrush SelectedBrush = new(Colors.Yellow);
    private static readonly SolidColorBrush UnselectedBrush = new(Colors.Transparent);

    public string Value { get; set; } = "";
    public string Name { get; set; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BorderBrush));
        }
    }

    public SolidColorBrush BorderBrush => IsSelected ? SelectedBrush : UnselectedBrush;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class CategoryFilterItem : INotifyPropertyChanged
{
    private static readonly SolidColorBrush SelectedBrush = new(Colors.Yellow);
    private static readonly SolidColorBrush UnselectedBrush = new(Colors.Transparent);

    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BorderBrush));
        }
    }

    public SolidColorBrush BorderBrush => IsSelected ? SelectedBrush : UnselectedBrush;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class JobFilterPillItem : INotifyPropertyChanged
{
    private static readonly SolidColorBrush SelectedBrush = new(Colors.Yellow);
    private static readonly SolidColorBrush UnselectedBrush = new(Colors.Transparent);

    public Guid JobId { get; set; }
    public string Name { get; set; } = "";
    public bool IsAllOption { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BorderBrush));
        }
    }

    public SolidColorBrush BorderBrush => IsSelected ? SelectedBrush : UnselectedBrush;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class AssignableProductRow : INotifyPropertyChanged
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";

    private bool _isAssigned;
    public bool IsAssigned
    {
        get => _isAssigned;
        set
        {
            if (_isAssigned == value)
                return;
            _isAssigned = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
