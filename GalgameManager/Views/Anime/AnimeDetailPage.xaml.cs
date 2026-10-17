using GalgameManager;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeDetailPage : Page
{
    public AnimeDetailViewModel ViewModel { get; }
    public AnimeDetailPage()
    {
        ViewModel = App.GetService<AnimeDetailViewModel>();
        InitializeComponent();
    }
}
