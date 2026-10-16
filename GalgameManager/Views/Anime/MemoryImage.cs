using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace LemonVN.Views.Anime;

// Displays only already-validated in-memory bytes; no filename or network URI is retained.
public sealed class MemoryImage : UserControl
{
    public static readonly DependencyProperty BytesProperty = DependencyProperty.Register(nameof(Bytes),
        typeof(byte[]), typeof(MemoryImage), new PropertyMetadata(null, OnBytesChanged));
    public byte[]? Bytes { get => (byte[]?)GetValue(BytesProperty); set => SetValue(BytesProperty, value); }
    public event EventHandler? DecodeFailed;
    private int _version;
    private readonly Image _image = new() { Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
    public MemoryImage()
    {
        Content = _image;
        Loaded += async (_, _) => await LoadAsync();
        Unloaded += (_, _) => { _version++; _image.Source = null; };
    }
    private static async void OnBytesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        await ((MemoryImage)sender).LoadAsync();
    private async Task LoadAsync()
    {
        int version = ++_version;
        _image.Source = null;
        if (!IsLoaded || Bytes is not { } bytes) return;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer()); stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = 640 };
            await bitmap.SetSourceAsync(stream);
            if (version == _version && IsLoaded) _image.Source = bitmap;
        }
        catch (Exception)
        {
            if (version == _version && IsLoaded) DecodeFailed?.Invoke(this, EventArgs.Empty);
        }
    }
}
