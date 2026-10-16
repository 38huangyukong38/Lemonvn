using GalgameManager;
using LemonVN.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views;

public sealed partial class LemonCategoriesPage : Page
{
    public LemonCategoriesViewModel ViewModel { get; }
    public LemonCategoriesPage()
    {
        ViewModel = App.GetService<LemonCategoriesViewModel>();
        InitializeComponent();
    }
}
