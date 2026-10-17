using GalgameManager;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeEditPage : Page
{
    public AnimeEditViewModel ViewModel { get; }
    public AnimeEditPage()
    {
        ViewModel = App.GetService<AnimeEditViewModel>();
        InitializeComponent();
    }
}
