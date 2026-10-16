using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.Views;

public sealed partial class AnnualReportSubPage6 : Page
{
    public AnnualReportSubPage6ViewModel ViewModel { get; private set; }

    public AnnualReportSubPage6()
    {
        ViewModel = new AnnualReportSubPage6ViewModel();
        DataContext = ViewModel;
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter);
    }
}
