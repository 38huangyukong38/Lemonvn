using GalgameManager;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeRecyclePage : Page
{
    public AnimeRecycleViewModel ViewModel { get; }
    public AnimeRecyclePage()
    {
        ViewModel = App.GetService<AnimeRecycleViewModel>();
        InitializeComponent();
    }
}
