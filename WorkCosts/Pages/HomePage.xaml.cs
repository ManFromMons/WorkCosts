using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using WorkCosts.Helpers;
using WorkCosts.Models;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace WorkCosts.Pages;

public sealed partial class HomePage : Page
{
    private const double TileGap = 16;
    private const double ScrollPixelsPerTick = 0.85;

    private readonly List<byte[]> _carouselBlobs = [];
    private DispatcherQueueTimer? _scrollTimer;
    private double _loopWidth;
    private double _lastCarouselHeight;
    private int _rebuildVersion;
    private bool _rebuildQueued;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
        await LoadCarouselAsync();
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        StopCarouselAnimation();
        base.OnNavigatedFrom(e);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) => StopCarouselAnimation();

    private async Task LoadAsync()
    {
        await using var db = App.Database.CreateContext();
        // SQLite cannot ORDER BY DateTimeOffset; use UtcDateTime (supported) instead.
        var workJobs = await db.WorkJobs
            .Include(w => w.Job)
            .Include(w => w.Items)
            .OrderByDescending(w => w.CreatedAt.UtcDateTime)
            .ToListAsync();

        WorkJobGrid.ItemsSource = workJobs.Select(w => new WorkJobCard(w)).ToList();
        EmptyText.Visibility = workJobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadCarouselAsync()
    {
        StopCarouselAnimation();
        _carouselBlobs.Clear();
        CarouselStrip.Children.Clear();
        CarouselTranslate.X = 0;

        await using var db = App.Database.CreateContext();
        var blobs = await db.Products
            .AsNoTracking()
            .Select(p => p.ImageBlob)
            .ToListAsync();

        foreach (var blob in blobs)
        {
            if (blob is { Length: > 0 })
            {
                _carouselBlobs.Add(blob);
            }
        }

        SetCarouselVisible(_carouselBlobs.Count > 0);
        if (_carouselBlobs.Count == 0)
        {
            return;
        }

        CarouselHost.UpdateLayout();
        QueueCarouselRebuild();
    }

    private void SetCarouselVisible(bool visible)
    {
        CarouselHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
        {
            ContentRow.Height = new GridLength(0.85, GridUnitType.Star);
            CarouselRow.Height = new GridLength(0.15, GridUnitType.Star);
        }
        else
        {
            ContentRow.Height = new GridLength(1, GridUnitType.Star);
            CarouselRow.Height = new GridLength(0);
        }
    }

    private void CarouselHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CarouselHost.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
        };

        if (_carouselBlobs.Count == 0 || e.NewSize.Height <= 1)
        {
            return;
        }

        if (Math.Abs(e.NewSize.Height - _lastCarouselHeight) < 1
            && CarouselStrip.Children.Count > 0)
        {
            return;
        }

        QueueCarouselRebuild();
    }

    private void QueueCarouselRebuild()
    {
        if (_rebuildQueued)
        {
            return;
        }

        _rebuildQueued = true;
        DispatcherQueue.TryEnqueue(async () =>
        {
            _rebuildQueued = false;
            await RebuildCarouselTilesAsync();
        });
    }

    private async Task RebuildCarouselTilesAsync()
    {
        var version = ++_rebuildVersion;
        StopCarouselAnimation();
        if (_carouselBlobs.Count == 0 || CarouselHost.ActualHeight <= 1)
        {
            return;
        }

        _lastCarouselHeight = CarouselHost.ActualHeight;
        var height = _lastCarouselHeight;
        var tileWidth = Math.Max(88, height * 0.95);
        var decodeHeight = Math.Max(32, (int)Math.Round(height * 0.70));

        var sequence = new List<byte[]>(_carouselBlobs);
        var minWidth = Math.Max(CarouselHost.ActualWidth, 480) * 1.25;
        while (sequence.Count * (tileWidth + TileGap) < minWidth)
        {
            sequence.AddRange(_carouselBlobs);
        }

        var loopCount = sequence.Count;
        sequence.AddRange(sequence.ToList());

        var tiles = new List<UIElement>(sequence.Count);
        var pageColor = GetPageBackgroundColor();
        foreach (var blob in sequence)
        {
            var tile = await CreateMirroredTileAsync(blob, tileWidth, height, decodeHeight, pageColor);
            if (version != _rebuildVersion)
            {
                return;
            }

            if (tile is not null)
            {
                tiles.Add(tile);
            }
        }

        if (version != _rebuildVersion || tiles.Count == 0)
        {
            return;
        }

        CarouselStrip.Children.Clear();
        CarouselTranslate.X = 0;
        foreach (var tile in tiles)
        {
            CarouselStrip.Children.Add(tile);
        }

        CarouselStrip.UpdateLayout();
        _loopWidth = 0;
        for (var i = 0; i < loopCount && i < CarouselStrip.Children.Count; i++)
        {
            if (CarouselStrip.Children[i] is FrameworkElement item)
            {
                _loopWidth += item.ActualWidth + item.Margin.Left + item.Margin.Right;
            }
        }

        if (_loopWidth <= 1)
        {
            _loopWidth = loopCount * (tileWidth + TileGap);
        }

        StartCarouselAnimation();
    }

    private Windows.UI.Color GetPageBackgroundColor()
    {
        var themeKey = ActualTheme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => Application.Current.RequestedTheme == ApplicationTheme.Light ? "Light" : "Dark"
        };

        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var dictObj)
            && dictObj is ResourceDictionary dict
            && dict.TryGetValue("AppPageBackgroundBrush", out var themed)
            && themed is SolidColorBrush themedBrush)
        {
            return themedBrush.Color;
        }

        return Microsoft.UI.Colors.Transparent;
    }

    private static async Task<UIElement?> CreateMirroredTileAsync(
        byte[] blob,
        double tileWidth,
        double totalHeight,
        int decodeHeight,
        Windows.UI.Color pageColor)
    {
        var photoSource = await DecodeBitmapAsync(blob, decodeHeight);
        var reflectionSource = await DecodeBitmapAsync(blob, decodeHeight);
        if (photoSource is null || reflectionSource is null)
        {
            return null;
        }

        var photoHeight = Math.Max(1, totalHeight * 0.70);
        var reflectionHeight = Math.Max(1, totalHeight - photoHeight);

        var photo = new Image
        {
            Source = photoSource,
            Width = tileWidth,
            Height = photoHeight,
            Stretch = Stretch.Uniform
        };

        var reflection = new Image
        {
            Source = reflectionSource,
            Width = tileWidth,
            Height = photoHeight,
            Stretch = Stretch.Uniform,
            Opacity = 0.32,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform { ScaleY = -1 }
        };

        var reflectionHost = new Grid
        {
            Width = tileWidth,
            Height = reflectionHeight,
            Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, tileWidth, reflectionHeight)
            },
            Children = { reflection }
        };

        var tile = new StackPanel
        {
            Width = tileWidth,
            Height = totalHeight,
            Children = { photo, reflectionHost }
        };

        var overlay = new Grid
        {
            Width = tileWidth,
            Height = totalHeight,
            Margin = new Thickness(0, 0, TileGap, 0),
            Children = { tile }
        };

        if (pageColor.A == 0)
        {
            return overlay;
        }

        overlay.Children.Add(new Rectangle
        {
            Width = tileWidth,
            Height = reflectionHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Microsoft.UI.Colors.Transparent, Offset = 0 },
                    new GradientStop { Color = pageColor, Offset = 1 }
                }
            }
        });
        return overlay;
    }

    private static async Task<BitmapImage?> DecodeBitmapAsync(byte[] bytes, int decodeHeight)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        stream.Seek(0);
        var bitmap = new BitmapImage { DecodePixelHeight = decodeHeight };
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private void StartCarouselAnimation()
    {
        StopCarouselAnimation();
        if (_loopWidth <= 1)
        {
            return;
        }

        _scrollTimer = DispatcherQueue.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(16);
        _scrollTimer.IsRepeating = true;
        _scrollTimer.Tick += ScrollTimer_Tick;
        _scrollTimer.Start();
    }

    private void ScrollTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        var x = CarouselTranslate.X - ScrollPixelsPerTick;
        if (x <= -_loopWidth)
        {
            x += _loopWidth;
        }

        CarouselTranslate.X = x;
    }

    private void StopCarouselAnimation()
    {
        if (_scrollTimer is null)
        {
            return;
        }

        _scrollTimer.Tick -= ScrollTimer_Tick;
        _scrollTimer.Stop();
        _scrollTimer = null;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        await using var db = App.Database.CreateContext();
        var jobs = await db.Jobs.OrderBy(j => j.Name).ToListAsync();
        if (jobs.Count == 0)
        {
            await DialogHelper.ShowMessageAsync(XamlRoot, "No jobs",
                "Create a Job under Stuff first, then plan a Work Job against it.");
            return;
        }

        var titleBox = new TextBox { Header = "Title", PlaceholderText = "e.g. Front suspension refresh" };
        var jobBox = new ComboBox
        {
            Header = "Job type",
            ItemsSource = jobs,
            DisplayMemberPath = nameof(Job.Name),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(titleBox);
        panel.Children.Add(jobBox);

        var dialog = new ContentDialog
        {
            Title = "New Work Job",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await DialogHelper.ShowAsync(dialog, XamlRoot) != ContentDialogResult.Primary)
        {
            return;
        }

        if (jobBox.SelectedItem is not Job job)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(titleBox.Text) ? job.Name : titleBox.Text.Trim();
        var workJob = new WorkJob
        {
            JobId = job.Id,
            Title = title,
            CreatedAt = DateTimeOffset.Now
        };
        db.WorkJobs.Add(workJob);
        await db.SaveChangesAsync();

        Frame.Navigate(typeof(WorkJobDetailPage), workJob.Id);
    }

    private void WorkJobGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WorkJobCard card)
        {
            Frame.Navigate(typeof(WorkJobDetailPage), card.Id);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: WorkJobCard card })
        {
            return;
        }

        if (!await DialogHelper.ConfirmAsync(XamlRoot, "Delete Work Job", $"Delete '{card.Title}'?"))
        {
            return;
        }

        await using var db = App.Database.CreateContext();
        var entity = await db.WorkJobs.FindAsync(card.Id);
        if (entity is not null)
        {
            db.WorkJobs.Remove(entity);
            await db.SaveChangesAsync();
        }

        await LoadAsync();
    }

    private sealed class WorkJobCard
    {
        public WorkJobCard(WorkJob workJob)
        {
            Id = workJob.Id;
            Title = workJob.Title;
            JobName = workJob.Job?.Name ?? "Unknown job";
            var diy = workJob.Items.Sum(i => i.Quantity * i.UnitCostSnapshot);
            var garage = workJob.Job?.GaragePrice ?? 0m;
            var saving = garage - diy;
            var duration = DurationHelper.ToDisplay(workJob.Job?.DurationMinutes ?? 0);
            Meta =
                $"DIY {diy.ToString("C", CultureInfo.CurrentCulture)} · Garage {garage.ToString("C", CultureInfo.CurrentCulture)} · {duration}";
            SavingText = $"Save {saving.ToString("C", CultureInfo.CurrentCulture)}";
        }

        public Guid Id { get; }
        public string Title { get; }
        public string JobName { get; }
        public string Meta { get; }
        public string SavingText { get; }
    }
}
