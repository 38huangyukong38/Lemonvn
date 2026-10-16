using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class UpdateContentViewModel(ILocalSettingsService settingsService, IInfoService infoService)
    : ObservableObject, INavigationAware
{
    [ObservableProperty] private Visibility _displayTitle = Visibility.Collapsed;
    [ObservableProperty] private Visibility _webView2Ok = Visibility.Collapsed;
    [ObservableProperty] private string _currentVersion =
        "UpdateContentPage_CurrentVersion".GetLocalized() + RuntimeHelper.GetVersion();

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            WebView2Ok = Utils.IsWebview2Ok() ? Visibility.Visible : Visibility.Collapsed;
            await settingsService.SaveSettingAsync(KeyValues.DisplayedUpdateVersion, RuntimeHelper.GetVersion());
            if (parameter is bool displayTitle)
                DisplayTitle = displayTitle ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception e) //不应该发生
        {
            infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Oops, something went wrong", e);
        }
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private async Task LaunchWebsiteAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://potatovn.net/upgrade-log/"));
    }
}