using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using LemonVN.Helpers;
using Microsoft.UI.Xaml.Navigation;

namespace LemonVN.ViewModels;

public partial class LemonShellViewModel : ObservableObject
{
    public INavigationService NavigationService { get; }
    public INavigationViewService NavigationViewService { get; }
    [ObservableProperty] private bool _isBackEnabled;
    [ObservableProperty] private bool _showHeader = true;
    [ObservableProperty] private object? _selected;
    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private bool _isErrorOpen;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public LemonShellViewModel(INavigationService navigation, INavigationViewService navigationView)
    {
        NavigationService = navigation;
        NavigationViewService = navigationView;
        navigation.Navigated += OnNavigated;
    }

    private void OnNavigated(object sender, NavigationEventArgs args)
    {
        IsBackEnabled = NavigationService.CanGoBack;
        Type section = args.SourcePageType.Namespace == "LemonVN.Views.Anime"
            ? typeof(Views.Anime.AnimeLibraryPage) : args.SourcePageType;
        Selected = NavigationViewService.GetSelectedItem(section);
        ShowHeader = args.SourcePageType != typeof(Views.Anime.AnimeLibraryPage) &&
            args.SourcePageType != typeof(Views.LemonRecognitionPage);
        string key = args.SourcePageType == typeof(Views.LemonSettingsPage) ? "LemonSettings_Title"
            : args.SourcePageType == typeof(Views.Anime.AnimeDetailPage) ? "Anime_Detail"
            : args.SourcePageType == typeof(Views.Anime.AnimeEditPage) ? (args.Parameter is Guid ? "Anime_Edit" : "Anime_Add")
            : args.SourcePageType == typeof(Views.Anime.AnimeRecyclePage) ? "Recycle_Title"
            : args.SourcePageType == typeof(Views.Anime.AnimeLookupPage) ? "Bangumi_Title"
            : args.SourcePageType == typeof(Views.LemonCategoriesPage) ? "Ux_CategoriesTitle"
            : args.SourcePageType == typeof(Views.LemonRecognitionPage) ? "Ux_RecognitionTitle"
            : args.SourcePageType == typeof(Views.Anime.AnimeLibraryPage) ? "Ux_LibraryTitle"
            : "Ux_HomeTitle";
        PageTitle = LemonLocalization.GetString(key);
    }

    public void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorOpen = true;
    }
}
