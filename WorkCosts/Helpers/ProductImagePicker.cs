using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WorkCosts.Services;
using Windows.Storage.Streams;

namespace WorkCosts.Helpers;

public sealed record ProductImagePickResult(
    byte[] Bytes,
    string ContentType,
    ProductPageMetadata Metadata);

public static class ProductImagePicker
{
    /// <summary>
    /// Loads HTML, metadata and images without showing a dialog. Chromium is created here so it
    /// is not nested inside a ContentDialog (that combination never surfaces UI).
    /// </summary>
    public static async Task<ProductPageLoadResult> FetchPageAsync(
        XamlRoot xamlRoot,
        string pageUrl,
        Action<string>? status = null)
    {
        var service = new ProductImageService();
        IBrowserPageSession? browser = null;
        if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)
            && ProductPageMetadataParser.IsAutodocHost(pageUri.Host))
        {
            if (await service.CanServeFromCacheAsync(pageUrl))
            {
                status?.Invoke("Using cached Autodoc page and images…");
            }
            else
            {
                status?.Invoke("Opening Autodoc in Chromium…");
                browser = await ChromiumPageLoader.CreateAsync(xamlRoot);
            }
        }
        else
        {
            status?.Invoke("Loading product page…");
        }

        await using (browser as IAsyncDisposable)
        {
            if (browser is not null)
            {
                status?.Invoke("Reading images from the page…");
            }

            return await service.LoadPageAsync(pageUrl, browser);
        }
    }

    public static async Task<ProductImagePickResult?> PickFromPageAsync(
        XamlRoot xamlRoot,
        string pageUrl,
        Action<bool>? setBusy = null,
        Action<ProductPageMetadata>? onMetadata = null)
    {
        setBusy?.Invoke(true);
        ProductPageLoadResult page;
        try
        {
            page = await FetchPageAsync(xamlRoot, pageUrl);
            onMetadata?.Invoke(page.Metadata);
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageAsync(xamlRoot, "Could not load page", ex.Message);
            return null;
        }
        finally
        {
            setBusy?.Invoke(false);
        }

        if (page.Images.Count == 0)
        {
            await DialogHelper.ShowMessageAsync(
                xamlRoot,
                "No images found",
                "The page loaded but no product images were captured.");
            return null;
        }

        var selected = page.Images.Count == 1
            ? page.Images[0]
            : await ChooseImageAsync(xamlRoot, page.Images);
        if (selected is null)
        {
            return null;
        }

        return new ProductImagePickResult(selected.Bytes, selected.ContentType, page.Metadata);
    }

    private static async Task<ProductImageCandidate?> ChooseImageAsync(
        XamlRoot xamlRoot,
        IReadOnlyList<ProductImageCandidate> images)
    {
        var grid = new GridView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 420,
            MinWidth = 480
        };

        foreach (var candidate in images)
        {
            var image = new Image
            {
                Width = 120,
                Height = 120,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                Source = await ToBitmapAsync(candidate.Bytes)
            };
            grid.Items.Add(new Border
            {
                Width = 128,
                Height = 128,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Child = image,
                Tag = candidate
            });
        }

        if (grid.Items.Count > 0)
        {
            grid.SelectedIndex = 0;
        }

        var dialog = new ContentDialog
        {
            Title = "Select product image",
            Content = grid,
            PrimaryButtonText = "Use image",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = grid.Items.Count > 0,
            XamlRoot = xamlRoot
        };

        var result = await DialogHelper.ShowAsync(dialog, xamlRoot);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return grid.SelectedItem is Border { Tag: ProductImageCandidate chosen } ? chosen : null;
    }

    public static async Task<BitmapImage?> ToBitmapAsync(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}
