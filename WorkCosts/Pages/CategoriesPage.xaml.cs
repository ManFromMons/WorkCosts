using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WorkCosts.Helpers;
using WorkCosts.Models;

namespace WorkCosts.Pages;

public sealed partial class CategoriesPage : Page
{
    public ObservableCollection<CategoryTileVM> CategoryTiles { get; } = new();
    public ObservableCollection<ProductRow> ProductRows { get; } = new();

    private CategoryTileVM? _selected;
    private readonly List<ProductRow> _allProductRows = [];
    private List<JobCostCard> _jobCards = [];
    private bool _suppressJobFilter;

    public CategoriesPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCategoryPanelLayout();
        await LoadAsync();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCategoryPanelLayout();

    private void UpdateCategoryPanelLayout()
    {
        if (ActualHeight <= 0) return;

        var maxTopHeight = Math.Max(80, (ActualHeight - 48) * 0.30);
        CategoryPanelHost.MaxHeight = maxTopHeight;

        var headerHeight = CategoryHeader.ActualHeight > 0 ? CategoryHeader.ActualHeight : 40;
        CategoryListScroll.MaxHeight = Math.Max(40, maxTopHeight - headerHeight - CategoryPanelHost.RowSpacing);
    }

    private async Task LoadAsync()
    {
        await using var db = App.Database.CreateContext();
        var categories = await db.Categories
            .Include(c => c.Products)
            .OrderBy(c => c.Name)
            .ToListAsync();

        CategoryTiles.Clear();
        foreach (var c in categories)
        {
            var vm = new CategoryTileVM
            {
                Id = c.Id,
                Name = c.Name,
                ItemCount = c.Products.Count,
                TotalCost = c.Products.Sum(p => p.UnitCost),
                IsSelected = _selected is not null && c.Id == _selected.Id
            };
            CategoryTiles.Add(vm);
        }

        if (_selected is not null)
        {
            var match = CategoryTiles.FirstOrDefault(t => t.Id == _selected.Id);
            if (match is not null)
            {
                SelectTile(match);
            }
            else
            {
                ClearSelection();
            }
        }
    }

    private void Tile_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (GetTileVm(sender) is { } vm)
            vm.IsHovered = true;
    }

    private void Tile_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (GetTileVm(sender) is { } vm)
            vm.IsHovered = false;
    }

    private static CategoryTileVM? GetTileVm(object sender) =>
        sender switch
        {
            FrameworkElement { DataContext: CategoryTileVM vm } => vm,
            FrameworkElement { Tag: CategoryTileVM vm } => vm,
            _ => null
        };

    private void Tile_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoryTileVM vm })
            SelectTile(vm);
        else if (sender is Border { Tag: CategoryTileVM vm2 })
            SelectTile(vm2);
    }

    private void SelectTile(CategoryTileVM tile)
    {
        if (_selected == tile) return;
        if (_selected is not null) _selected.IsSelected = false;
        tile.IsSelected = true;
        _selected = tile;
        _ = LoadDetailAsync(tile.Id);
    }

    private void ClearSelection()
    {
        if (_selected is not null) _selected.IsSelected = false;
        _selected = null;
        DetailPanel.Visibility = Visibility.Collapsed;
        NoSelectionText.Visibility = Visibility.Visible;
    }

    private async Task LoadDetailAsync(Guid categoryId)
    {
        DetailPanel.Visibility = Visibility.Visible;
        NoSelectionText.Visibility = Visibility.Collapsed;

        await using var db = App.Database.CreateContext();
        var products = await db.Products
            .Where(p => p.CategoryId == categoryId)
            .Include(p => p.ProductJobs)
                .ThenInclude(pj => pj.Job)
            .ToListAsync();

        var jobs = await db.Jobs.OrderBy(j => j.Name).ToListAsync();

        var totalItems = products.Count;
        var totalCost = products.Sum(p => p.UnitCost);
        SummaryItemCount.Text = $"{totalItems} item{(totalItems == 1 ? "" : "s")} in category";
        SummaryTotalCost.Text = $"Category total: {totalCost:C}";

        var jobCards = new List<JobCostCard>();
        foreach (var job in jobs)
        {
            var applicable = products
                .Where(p => p.IsAllJobs || p.ProductJobs.Any(pj => pj.JobId == job.Id))
                .ToList();
            if (applicable.Count == 0)
                continue;

            jobCards.Add(new JobCostCard
            {
                JobId = job.Id,
                JobName = job.Name,
                Items = applicable.Count,
                Cost = applicable.Sum(p => p.UnitCost)
            });
        }

        var unassigned = products
            .Where(p => !p.IsAllJobs && p.ProductJobs.Count == 0)
            .ToList();
        if (unassigned.Count > 0)
        {
            jobCards.Add(new JobCostCard
            {
                JobId = null,
                JobName = "Unassigned",
                Items = unassigned.Count,
                Cost = unassigned.Sum(p => p.UnitCost)
            });
        }

        _suppressJobFilter = true;
        _jobCards = jobCards;
        JobCardsRepeater.ItemsSource = jobCards;

        _allProductRows.Clear();
        foreach (var p in products.OrderBy(p => p.Name))
        {
            var row = new ProductRow
            {
                Name = p.Name,
                UnitCost = p.UnitCost,
                JobsText = FormatProductJobs(p),
                IsAllJobs = p.IsAllJobs,
                JobIds = p.ProductJobs.Select(pj => pj.JobId).ToHashSet()
            };
            row.PropertyChanged += ProductRow_PropertyChanged;
            _allProductRows.Add(row);
        }

        ApplyJobFilters();
        _suppressJobFilter = false;
    }

    private void JobFilter_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressJobFilter)
            return;

        ApplyJobFilters();
    }

    private void ApplyJobFilters()
    {
        var selected = _jobCards.Where(card => card.IsOn).ToList();
        IEnumerable<ProductRow> visible = _allProductRows;
        if (selected.Count > 0)
        {
            var jobIds = selected
                .Where(card => card.JobId is Guid)
                .Select(card => card.JobId!.Value)
                .ToHashSet();
            var includeUnassigned = selected.Any(card => card.JobId is null);
            visible = _allProductRows.Where(row =>
                (jobIds.Count > 0 && (row.IsAllJobs || row.JobIds.Overlaps(jobIds)))
                || (includeUnassigned && row.IsUnassigned));
        }

        ProductRows.Clear();
        foreach (var row in visible)
            ProductRows.Add(row);

        UpdateRunningTotal();
    }

    private void ProductRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductRow.IsIncluded))
            UpdateRunningTotal();
    }

    private void ProductRow_IncludedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: ProductRow row } cb)
            row.IsIncluded = cb.IsChecked == true;

        UpdateRunningTotal();
    }

    private void UpdateRunningTotal()
    {
        var selected = ProductRows.Where(r => r.IsIncluded).ToList();
        var total = selected.Count > 0
            ? selected.Sum(r => r.UnitCost)
            : ProductRows.Sum(r => r.UnitCost);
        SelectedTotalText.Text = total.ToString("C");
    }

    private static string FormatProductJobs(Product product)
    {
        if (product.IsAllJobs)
            return "All jobs";

        var names = product.ProductJobs
            .Select(pj => pj.Job?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n)
            .ToList();

        return names.Count == 0 ? "Unassigned" : string.Join(", ", names!);
    }

    // --- Add inline ---
    private void AddInline_Click(object sender, RoutedEventArgs e)
    {
        var vm = new CategoryTileVM { Id = Guid.Empty, Name = "", IsEditing = true, EditName = "" };
        CategoryTiles.Insert(0, vm);
    }

    // --- Edit tile ---
    private void EditTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CategoryTileVM vm })
        {
            vm.EditName = vm.Name;
            vm.IsEditing = true;
        }
    }

    private async void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CategoryTileVM vm }) return;

        var name = vm.EditName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            if (vm.Id == Guid.Empty)
                CategoryTiles.Remove(vm);
            else
                vm.IsEditing = false;
            return;
        }

        await using var db = App.Database.CreateContext();

        var duplicate = await db.Categories.AnyAsync(c => c.Name == name && c.Id != vm.Id);
        if (duplicate)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Validation", "A category with that name already exists.");
            return;
        }

        if (vm.Id == Guid.Empty)
        {
            var entity = new Category { Name = name };
            db.Categories.Add(entity);
            await db.SaveChangesAsync();
            vm.Id = entity.Id;
        }
        else
        {
            var entity = await db.Categories.FindAsync(vm.Id);
            if (entity is not null)
            {
                entity.Name = name;
                await db.SaveChangesAsync();
            }
        }

        vm.Name = name;
        vm.IsEditing = false;
        await LoadAsync();
    }

    // --- Delete tile ---
    private async void DeleteTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CategoryTileVM vm }) return;
        if (vm.Id == Guid.Empty) { CategoryTiles.Remove(vm); return; }

        await using var db = App.Database.CreateContext();
        var inUse = await db.Products.AnyAsync(p => p.CategoryId == vm.Id);
        if (inUse)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Cannot delete",
                $"Category '{vm.Name}' is used by one or more items.");
            return;
        }

        if (!await DialogHelper.ConfirmAsync(XamlRoot, "Delete category", $"Delete '{vm.Name}'?"))
            return;

        var entity = await db.Categories.FindAsync(vm.Id);
        if (entity is not null)
        {
            db.Categories.Remove(entity);
            await db.SaveChangesAsync();
        }

        if (_selected?.Id == vm.Id) ClearSelection();
        await LoadAsync();
    }
}

// --- View models ---

public sealed class CategoryTileVM : INotifyPropertyChanged
{
    public const double CollapsedNameMaxWidth = 120;

    private static readonly SolidColorBrush SelectedBrush = new(Colors.White);
    private static readonly SolidColorBrush UnselectedBrush = new(Colors.Transparent);

    public Guid Id { get; set; }

    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
            NotifyExpansionChanged();
        }
    }

    private int _itemCount;
    public int ItemCount
    {
        get => _itemCount;
        set { _itemCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ItemCountText)); }
    }
    public string ItemCountText => $"({ItemCount})";

    private decimal _totalCost;
    public decimal TotalCost
    {
        get => _totalCost;
        set { _totalCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalCostText)); }
    }
    public string TotalCostText => TotalCost.ToString("C");

    private bool _isHovered;
    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered == value) return;
            _isHovered = value;
            OnPropertyChanged();
            NotifyExpansionChanged();
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BorderBrush));
            NotifyExpansionChanged();
        }
    }
    public SolidColorBrush BorderBrush => IsSelected ? SelectedBrush : UnselectedBrush;

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayVisible));
            OnPropertyChanged(nameof(EditVisible));
            NotifyExpansionChanged();
        }
    }
    public Visibility DisplayVisible => IsEditing ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EditVisible => IsEditing ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActionButtonsVisible => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

    public double NameMaxWidth => IsExpanded ? double.PositiveInfinity : CollapsedNameMaxWidth;
    public TextTrimming NameTextTrimming => IsExpanded ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    private bool IsExpanded => IsHovered || IsSelected || IsEditing;

    private string _editName = "";
    public string EditName
    {
        get => _editName;
        set { _editName = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private void NotifyExpansionChanged()
    {
        OnPropertyChanged(nameof(NameMaxWidth));
        OnPropertyChanged(nameof(NameTextTrimming));
        OnPropertyChanged(nameof(ActionButtonsVisible));
    }
}

public sealed class JobCostCard : INotifyPropertyChanged
{
    public Guid? JobId { get; set; }
    public string JobName { get; set; } = "";
    public int Items { get; set; }
    public decimal Cost { get; set; }
    public string ItemsText => $"{Items} item{(Items == 1 ? "" : "s")} in this category";
    public string CostText => $"{Cost:C} category cost";

    private bool _isOn;
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
                return;
            _isOn = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOn)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class ProductRow : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string JobsText { get; set; } = "";
    public decimal UnitCost { get; set; }
    public string CostText => UnitCost.ToString("C");
    public bool IsAllJobs { get; set; }
    public HashSet<Guid> JobIds { get; set; } = [];
    public bool IsUnassigned => !IsAllJobs && JobIds.Count == 0;

    private bool _isIncluded;
    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (_isIncluded == value) return;
            _isIncluded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
