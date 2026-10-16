using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.Views;

public sealed partial class AnnualReportSubPage4 : Page
{
    public AnnualReportSubPage4ViewModel ViewModel { get; private set; }

    public AnnualReportSubPage4()
    {
        ViewModel = new AnnualReportSubPage4ViewModel();
        DataContext = ViewModel;
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter);
    }
}
