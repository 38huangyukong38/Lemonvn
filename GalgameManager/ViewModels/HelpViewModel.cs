using System.Collections.ObjectModel;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class HelpViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private ObservableCollection<Faq>? _faqs;

    [ObservableProperty]
    private string _currentUrl = "https://potatovn.net/usage/quick-start/import-game.html";

    public HelpViewModel()
    {

    }

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }


    [RelayCommand]
    private async Task OpenWeb()
    {
        // 在默认浏览器中打开当前URL
        await Launcher.LaunchUriAsync(new Uri(CurrentUrl));

    }

    [RelayCommand]
    private async Task Issues()
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/GoldenPotato137/GalgameManager/issues/new/choose"));
    }
}