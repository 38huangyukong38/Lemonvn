using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using LemonVN.ViewModels.Anime;

namespace LemonVN.ViewModels;

// Shared local navigation base; feature pages add their own presentation state.
public partial class LemonLandingViewModel(INavigationService navigation) : ObservableObject
{
    protected INavigationService Navigation { get; } = navigation;
    [RelayCommand]
    private void OpenLibrary() => Navigation.NavigateTo(typeof(AnimeLibraryViewModel).FullName!);
}
