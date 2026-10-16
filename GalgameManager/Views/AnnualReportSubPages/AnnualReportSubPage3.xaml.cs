using Microsoft.UI.Xaml.Controls;
using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Navigation;
namespace GalgameManager.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AnnualReportSubPage3 : Page
    {
        public ReportSubPage3ViewModel ViewModel
        {
            get;
        }

        public AnnualReportSubPage3()
        {
            ViewModel = new ReportSubPage3ViewModel(App.GetService<INavigationService>());
            DataContext = ViewModel;
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.OnNavigatedTo(e.Parameter);
        }
    }
}
