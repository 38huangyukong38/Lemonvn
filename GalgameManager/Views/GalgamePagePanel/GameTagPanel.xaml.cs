using GalgameManager.Contracts.Services;
using GalgameManager.Models.Filters;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.GalgamePagePanel;

public partial class GameTagPanel
{
    private readonly IFilterService _filterService = App.GetService<IFilterService>();
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    
    public GameTagPanel()
    {
        InitializeComponent();
    }

    protected override void Update()
    {
        if (Panel is not null)
            Panel.Visibility = Game?.Tags.Value?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not string tag) return;
        _filterService.ClearFilters();
        _filterService.AddFilter(new TagFilter(tag));
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }
}
