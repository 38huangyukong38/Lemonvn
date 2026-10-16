using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace GalgameManager.ViewModels;

public partial class AnnualReportSubPage6ViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private AnnualReportData _annualReportData = new();
    public ObservableCollection<MonthlyBestGame> MonthlyBestGames { get; } = new();

    public void OnNavigatedTo(object parameter)
    {
        Debug.Assert(parameter is AnnualReportData);
        AnnualReportData = (AnnualReportData)parameter;

        MonthlyBestGames.Clear();
        for (int i = 0; i < 12; i++)
        {
            if (AnnualReportData.MonthlyBestGames[i] != null)
            {
                MonthlyBestGames.Add(new MonthlyBestGame
                {
                    Month = i + 1,
                    Game = AnnualReportData.MonthlyBestGames[i]!
                });
            }
        }
    }

    public void OnNavigatedFrom()
    {
    }
}

public class MonthlyBestGame
{
    public int Month { get; set; }
    public required Galgame Game { get; set; }
}
