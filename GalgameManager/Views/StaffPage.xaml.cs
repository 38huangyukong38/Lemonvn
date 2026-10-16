using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class StaffPage : Page
{
    public StaffViewModel ViewModel
    {
        get;
    }

    public StaffPage()
    {
        ViewModel = App.GetService<StaffViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
