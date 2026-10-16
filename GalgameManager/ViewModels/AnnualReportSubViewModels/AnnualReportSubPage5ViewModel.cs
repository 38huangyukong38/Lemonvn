using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;
using System.Diagnostics;

namespace GalgameManager.ViewModels;

public partial class AnnualReportSubPage5ViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private AnnualReportData _annualReportData = new();

    public void OnNavigatedTo(object parameter)
    {
        Debug.Assert(parameter is AnnualReportData);
        AnnualReportData = (AnnualReportData)parameter;
    }

    public void OnNavigatedFrom()
    {
    }
}
