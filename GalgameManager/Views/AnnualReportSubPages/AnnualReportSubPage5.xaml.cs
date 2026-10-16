using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.Views;

public sealed partial class AnnualReportSubPage5 : Page
{
    public AnnualReportSubPage5ViewModel ViewModel { get; private set; }

    public AnnualReportSubPage5()
    {
        ViewModel = new AnnualReportSubPage5ViewModel();
        DataContext = ViewModel;
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter);
    }
}
