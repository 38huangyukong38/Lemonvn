using LemonVN.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views;

public sealed partial class LemonShellPage : Page
{
    public LemonShellViewModel ViewModel { get; }
    public LemonShellPage(LemonShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
        Unloaded += (_, _) => ViewModel.NavigationViewService.UnregisterEvents();
    }
}
