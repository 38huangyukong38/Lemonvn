using GalgameManager;
using LemonVN.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views;

public sealed partial class LemonHomePage : Page
{
    public LemonHomeViewModel ViewModel { get; }
    public LemonHomePage()
    {
        ViewModel = App.GetService<LemonHomeViewModel>();
        InitializeComponent();
    }
}
