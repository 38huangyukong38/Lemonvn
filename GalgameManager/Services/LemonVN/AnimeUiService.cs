using Windows.Storage.Pickers;
using GalgameManager;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Services;

public sealed class AnimeUiService : IAnimeUiService
{
    public async Task<string?> PickCoverAsync()
    {
        var picker = new FileOpenPicker { ViewMode = PickerViewMode.Thumbnail };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindow!.GetWindowHandle());
        foreach (string extension in new[] { ".png", ".jpg", ".jpeg" }) picker.FileTypeFilter.Add(extension);
        return (await picker.PickSingleFileAsync())?.Path;
    }

    public async Task<bool> ConfirmDeleteAsync(string title)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = ((FrameworkElement)App.MainWindow.Content).RequestedTheme,
            Title = LemonLocalization.GetString("Anime_DeleteTitle"),
            Content = LemonLocalization.GetString("Anime_DeleteDescription", title),
            PrimaryButtonText = LemonLocalization.GetString("Anime_Delete"),
            CloseButtonText = LemonLocalization.GetString("Anime_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmMarkThroughAsync(int episodeNumber)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = ((FrameworkElement)App.MainWindow.Content).RequestedTheme,
            Title = LemonLocalization.GetString("AnimeProgress_ThroughTitle", episodeNumber),
            Content = LemonLocalization.GetString("AnimeProgress_ThroughConfirmation", episodeNumber),
            PrimaryButtonText = LemonLocalization.GetString("AnimeProgress_Confirm"),
            CloseButtonText = LemonLocalization.GetString("Anime_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
