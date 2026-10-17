using System.Runtime.InteropServices.WindowsRuntime;
using LemonVN.Infrastructure.Anime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LemonVN.Views.Anime;

public static class LocalCoverImagePolicy
{
    public static bool CanReadLocalUri(Uri? uri) => uri is { IsAbsoluteUri: true, IsFile: true, IsUnc: false } &&
        string.IsNullOrEmpty(uri.Host) && uri.Query.Length == 0 && uri.Fragment.Length == 0 && Path.IsPathFullyQualified(uri.LocalPath);
}

// Decode owned/cache files through a bounded stream. Do not hand arbitrary file
// URIs to WinUI's packaged-resource resolver or issue HTTP requests from XAML.
public static class LocalCoverImage
{
    private static readonly SemaphoreSlim Decodes = new(3, 3);
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached("Source",
        typeof(string), typeof(LocalCoverImage), new PropertyMetadata(null, OnSourceChanged));
    private static readonly DependencyProperty VersionProperty = DependencyProperty.RegisterAttached("Version",
        typeof(int), typeof(LocalCoverImage), new PropertyMetadata(0));
    public static string? GetSource(Image image) => (string?)image.GetValue(SourceProperty);
    public static void SetSource(Image image, string? value) => image.SetValue(SourceProperty, value);

    private static async void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var image = (Image)sender;
        image.Loaded -= OnLoaded; image.Loaded += OnLoaded;
        image.Unloaded -= OnUnloaded; image.Unloaded += OnUnloaded;
        await LoadAsync(image);
    }
    private static async void OnLoaded(object sender, RoutedEventArgs args) => await LoadAsync((Image)sender);
    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        var image = (Image)sender;
        image.SetValue(VersionProperty, (int)image.GetValue(VersionProperty) + 1);
        image.Source = null;
    }
    private static async Task LoadAsync(Image image)
    {
        int version = (int)image.GetValue(VersionProperty) + 1;
        image.SetValue(VersionProperty, version);
        if (!image.IsLoaded) return;
        image.Source = new BitmapImage(new Uri(CoverStorageService.Placeholder));
        string? source = GetSource(image);
        if (source is null || source == CoverStorageService.Placeholder) return;
        await Decodes.WaitAsync();
        try
        {
            if (!image.IsLoaded || (int)image.GetValue(VersionProperty) != version) return;
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || !LocalCoverImagePolicy.CanReadLocalUri(uri))
                throw new InvalidDataException();
            using var file = new FileStream(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length is <= 0 or > CoverStorageService.MaxBytes) throw new InvalidDataException();
            var bytes = new byte[(int)file.Length];
            await file.ReadExactlyAsync(bytes);
            await Task.Run(() => CoverStorageService.ValidateBytes(bytes));
            using var stream = new MemoryStream(bytes, writable: false).AsRandomAccessStream();
            var decoded = new BitmapImage { DecodePixelWidth = 320 };
            await decoded.SetSourceAsync(stream);
            // A recycled card or navigation may have replaced the source meanwhile.
            if (image.IsLoaded && (int)image.GetValue(VersionProperty) == version) image.Source = decoded;
        }
        catch (Exception)
        {
            if (image.IsLoaded && (int)image.GetValue(VersionProperty) == version)
                image.Source = new BitmapImage(new Uri(CoverStorageService.Placeholder));
        }
        finally { Decodes.Release(); }
    }
}
