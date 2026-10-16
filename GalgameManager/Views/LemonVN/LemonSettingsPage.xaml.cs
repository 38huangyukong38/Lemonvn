using GalgameManager;
using LemonVN.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views;

public sealed partial class LemonSettingsPage : Page
{
    public LemonSettingsViewModel ViewModel { get; }
    public LemonSettingsPage()
    {
        ViewModel = App.GetService<LemonSettingsViewModel>();
        InitializeComponent();
    }
}
