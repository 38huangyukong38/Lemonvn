using GalgameManager;
using LemonVN.Infrastructure.Anime;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeDetailPage : Page
{
    public AnimeDetailViewModel ViewModel { get; }
    public AnimeDetailPage()
    {
        ViewModel = App.GetService<AnimeDetailViewModel>();
        InitializeComponent();
    }
    private void OnCoverFailed(object sender, ExceptionRoutedEventArgs args) =>
        ((Image)sender).Source = new BitmapImage(new Uri(CoverStorageService.Placeholder));
}
