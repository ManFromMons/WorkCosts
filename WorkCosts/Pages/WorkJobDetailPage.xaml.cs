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

public sealed partial class WorkJobDetailPage : Page
{
    private Guid _workJobId;
    private WorkJob? _workJob;
    private readonly ObservableCollection<LineRow> _lines = [];
    private bool _loading;

    public WorkJobDetailPage()
    {
        InitializeComponent();
        LineList.ItemsSource = _lines;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            _workJobId = id;
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        _loading = true;
        await using var db = App.Database.CreateContext();
        _workJob = await db.WorkJobs
            .Include(w => w.Job)
            .Include(w => w.Items)
            .ThenInclude(i => i.Product!)
            .ThenInclude(i => i.Category)
            .FirstOrDefaultAsync(w => w.Id == _workJobId);

        if (_workJob is null)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "Not found", "This Work Job no longer exists.");
            Frame.GoBack();
            return;
        }

        TitleText.Text = _workJob.Title;
        SubtitleText.Text =
            $"{_workJob.Job?.Name} · created {_workJob.CreatedAt.LocalDateTime:g}";

        var categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        var catOptions = new List<FilterOption> { new(null, "All categories") };
        catOptions.AddRange(categories.Select(c => new FilterOption(c.Id, c.Name)));
        CategoryPicker.ItemsSource = catOptions;
        CategoryPicker.DisplayMemberPath = nameof(FilterOption.Label);
        CategoryPicker.SelectedIndex = 0;

        _lines.Clear();
        foreach (var line in _workJob.Items.OrderBy(i => i.Product?.Category?.Name).ThenBy(i => i.Product?.Name))
        {
            _lines.Add(new LineRow(line));
        }

        EmptyLinesText.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSummary();
        NotesPreview.Text = string.IsNullOrWhiteSpace(_workJob.Job?.NotesMarkdown)
            ? "No garage notes."
            : _workJob.Job!.NotesMarkdown;
        _loading = false;
    }

    private void UpdateSummary()
    {
        if (_workJob?.Job is null)
        {
            return;
        }

        var diy = _lines.Sum(l => l.Quantity * l.UnitCost);
        var garage = _workJob.Job.GaragePrice;
        var saving = garage - diy;

        DurationText.Text = DurationHelper.ToDisplay(_workJob.Job.DurationMinutes);
        GaragePriceText.Text = garage.ToString("C", CultureInfo.CurrentCulture);
        DiyTotalText.Text = diy.ToString("C", CultureInfo.CurrentCulture);
        SavingText.Text = saving.ToString("C", CultureInfo.CurrentCulture);
        EmptyLinesText.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (_workJob is null)
        {
            return;
        }

        await using var db = App.Database.CreateContext();
        var jobId = _workJob.JobId;
        var existingIds = _lines.Select(l => l.ProductId).ToHashSet();

        var query = db.Products
            .Include(i => i.Category)
            .Include(i => i.ProductJobs)
            .Where(i => i.IsAllJobs || i.ProductJobs.Any(ij => ij.JobId == jobId));

        if (CategoryPicker.SelectedItem is FilterOption { Id: Guid categoryId })
        {
            query = query.Where(i => i.CategoryId == categoryId);
        }

        var available = await query
            .Where(i => !existingIds.Contains(i.Id))
            .OrderBy(i => i.Category!.Name)
            .ThenBy(i => i.Name)
            .ToListAsync();

        if (available.Count == 0)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "No products",
                "No matching products are available. Add Products assigned to this job (or All jobs), or clear the category filter.");
            return;
        }

        var productBox = new ComboBox
        {
            Header = "Product",
            ItemsSource = available.Select(i => new ProductPick(i)).ToList(),
            DisplayMemberPath = nameof(ProductPick.Label),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var qtyBox = new NumberBox
        {
            Header = "Quantity",
            Value = 1,
            Minimum = 1,
            Maximum = short.MaxValue,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(productBox);
        panel.Children.Add(qtyBox);

        var dialog = new ContentDialog
        {
            Title = "Add product to plan",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await DialogHelper.ShowAsync(dialog, XamlRoot) != ContentDialogResult.Primary)
        {
            return;
        }

        if (productBox.SelectedItem is not ProductPick pick)
        {
            return;
        }

        var qty = (short)Math.Clamp((int)qtyBox.Value, 1, short.MaxValue);
        var line = new WorkJobItem
        {
            WorkJobId = _workJobId,
            ProductId = pick.Product.Id,
            Quantity = qty,
            UnitCostSnapshot = pick.Product.UnitCost
        };
        db.WorkJobItems.Add(line);
        await db.SaveChangesAsync();

        line.Product = pick.Product;
        _lines.Add(new LineRow(line));
        UpdateSummary();
    }

    private async void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: LineRow row })
        {
            return;
        }

        await using var db = App.Database.CreateContext();
        var entity = await db.WorkJobItems.FindAsync(row.Id);
        if (entity is not null)
        {
            db.WorkJobItems.Remove(entity);
            await db.SaveChangesAsync();
        }

        _lines.Remove(row);
        UpdateSummary();
    }

    private async void Quantity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || sender.DataContext is not LineRow row)
        {
            return;
        }

        if (double.IsNaN(args.NewValue))
        {
            return;
        }

        var qty = (short)Math.Clamp((int)args.NewValue, 1, short.MaxValue);
        if (row.Quantity == qty)
        {
            UpdateSummary();
            return;
        }

        row.Quantity = qty;
        await using var db = App.Database.CreateContext();
        var entity = await db.WorkJobItems.FindAsync(row.Id);
        if (entity is not null)
        {
            entity.Quantity = qty;
            await db.SaveChangesAsync();
        }

        UpdateSummary();
    }

    private void CategoryPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Filter applies when opening Add product dialog.
    }

    private sealed record FilterOption(Guid? Id, string Label);

    private sealed class ProductPick
    {
        public ProductPick(Product product)
        {
            Product = product;
            Label =
                $"{product.Name} · {product.Category?.Name} · {product.Vendor} · {product.UnitCost.ToString("C", CultureInfo.CurrentCulture)}";
        }

        public Product Product { get; }
        public string Label { get; }
    }

    private sealed class LineRow : INotifyPropertyChanged
    {
        private short _quantity;

        public LineRow(WorkJobItem line)
        {
            Id = line.Id;
            ProductId = line.ProductId;
            Name = line.Product?.Name ?? "Product";
            CategoryName = line.Product?.Category?.Name ?? "—";
            Vendor = string.IsNullOrWhiteSpace(line.Product?.Vendor) ? "—" : line.Product!.Vendor;
            UnitCost = line.UnitCostSnapshot;
            _quantity = line.Quantity;
        }

        public Guid Id { get; }
        public Guid ProductId { get; }
        public string Name { get; }
        public string CategoryName { get; }
        public string Vendor { get; }
        public decimal UnitCost { get; }

        public short Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value)
                {
                    return;
                }

                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineTotalText));
            }
        }

        public string LineTotalText => (Quantity * UnitCost).ToString("C", CultureInfo.CurrentCulture);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
