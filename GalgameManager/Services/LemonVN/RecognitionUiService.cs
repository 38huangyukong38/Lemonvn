using GalgameManager;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace LemonVN.Services;

public sealed class RecognitionUiService : IRecognitionUiService
{
    public async Task<string?> PickImageAsync()
    {
        var picker = new FileOpenPicker(App.MainWindow!.AppWindow.Id);
        foreach (string extension in new[] { ".png", ".jpg", ".jpeg" }) picker.FileTypeFilter.Add(extension);
        return (await picker.PickSingleFileAsync())?.Path;
    }
    public async Task<bool> ConfirmUploadAsync(RecognitionMode mode)
    {
        string destination = mode switch
        {
            RecognitionMode.Scene => "Trace.moe · https://api.trace.moe",
            RecognitionMode.Character => "AnimeTrace · https://api.animetrace.com",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        var dialog = new ContentDialog
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = ((FrameworkElement)App.MainWindow.Content).RequestedTheme,
            Title = LemonLocalization.GetString("Recognition_ConfirmTitle"),
            Content = LemonLocalization.GetString("Recognition_ConfirmBody", destination),
            PrimaryButtonText = LemonLocalization.GetString("Recognition_ConfirmUpload"),
            CloseButtonText = LemonLocalization.GetString("Anime_Cancel"), DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
