using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using System.Collections.ObjectModel;
using GalgameManager.Contracts;

namespace GalgameManager.ViewModels
{
    public partial class ReportSubPage3ViewModel (INavigationService navigationService) : ObservableObject, INavigationAware
    {
        [ObservableProperty] private Category _category = new();
        [ObservableProperty] private int _playedGamesCount;
        [ObservableProperty] private ObservableCollection<Galgame> _games = new();
        
        public void OnNavigatedTo(object parameter)
        {
            Debug.Assert(parameter is AnnualReportData);
            AnnualReportData data = (AnnualReportData)parameter;
            Category = data.FavouriteDeveloper;
            PlayedGamesCount = data.GamesInFavouriteDeveloper.Count;
            Games.SyncCollection(data.GamesInFavouriteDeveloper);
        }

        public void OnNavigatedFrom()
        {
        }

        [RelayCommand]
        private void NavigateTo(IDisplayableGameObject? clickedItem)
        {
            if (clickedItem is Galgame galgame)
            {
                NavigationHelper.NavigateToGalgamePage(navigationService, new GalgamePageParameter {Galgame = galgame});
            }
        }
    }
}
