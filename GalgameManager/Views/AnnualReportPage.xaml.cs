using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class AnnualReportPage : Page
{
    public AnnualReportViewModel ViewModel
    {
        get;
    }

    public AnnualReportPage()
    {
        ViewModel = App.GetService<AnnualReportViewModel>();
        InitializeComponent();
        ViewModel.ContentFrame = ContentFrame;
    }
}
