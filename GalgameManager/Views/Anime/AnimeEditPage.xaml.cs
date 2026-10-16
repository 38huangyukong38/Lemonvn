using GalgameManager;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using LemonVN.Infrastructure.Anime;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeEditPage : Page
{
    public AnimeEditViewModel ViewModel { get; }
    public AnimeEditPage()
    {
        ViewModel = App.GetService<AnimeEditViewModel>();
        InitializeComponent();
    }
    private void OnCoverFailed(object sender, ExceptionRoutedEventArgs args) =>
        ((Image)sender).Source = new BitmapImage(new Uri(CoverStorageService.Placeholder));
}
