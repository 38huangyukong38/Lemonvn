using LemonVN.ViewModels;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeShelfControl : UserControl
{
    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(nameof(Section),
        typeof(AnimeShelfViewModel), typeof(AnimeShelfControl), new PropertyMetadata(null));
    public AnimeShelfViewModel Section
    {
        get => (AnimeShelfViewModel)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }
    public AnimeShelfControl() => InitializeComponent();
    private void OnAnimeClicked(object sender, ItemClickEventArgs args) => Section?.OpenAnimeCommand.Execute(args.ClickedItem as AnimeCardViewModel);
}
