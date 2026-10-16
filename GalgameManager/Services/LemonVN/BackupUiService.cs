using GalgameManager;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using LemonVN.Infrastructure.Backup;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace LemonVN.Services;

public sealed class BackupUiService : IBackupUiService
{
    public async Task<string?> PickExportAsync()
    {
        var picker = new FileSavePicker(App.MainWindow!.AppWindow.Id)
        {
            SuggestedFileName = $"LemonVN-Backup-{DateTime.Now:yyyyMMdd-HHmm}",
            DefaultFileExtension = ".zip"
        };
        picker.FileTypeChoices.Add("LemonVN ZIP", new List<string> { ".zip" });
        return (await picker.PickSaveFileAsync())?.Path;
    }

    public async Task<string?> PickRestoreAsync()
    {
        var picker = new FileOpenPicker(App.MainWindow!.AppWindow.Id);
        picker.FileTypeFilter.Add(".zip");
        return (await picker.PickSingleFileAsync())?.Path;
    }

    public Task<bool> ConfirmRestoreAsync(PreparedBackup preview) => ConfirmAsync("Backup_ConfirmTitle",
        LemonLocalization.GetString("Backup_ConfirmBody", preview.AnimeCount, preview.TrashCount,
            preview.CurrentAnimeCount, preview.CurrentTrashCount), "Backup_ConfirmReplace");

    public Task<bool> ConfirmPurgeAsync(string title, bool finalConfirmation) => ConfirmAsync(
        finalConfirmation ? "Recycle_FinalTitle" : "Recycle_PurgeTitle",
        LemonLocalization.GetString(finalConfirmation ? "Recycle_FinalBody" : "Recycle_PurgeBody", title),
        finalConfirmation ? "Recycle_Purge" : "Recycle_Continue");

    private static async Task<bool> ConfirmAsync(string titleKey, string body, string primaryKey)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = ((FrameworkElement)App.MainWindow.Content).RequestedTheme,
            Title = LemonLocalization.GetString(titleKey), Content = body,
            PrimaryButtonText = LemonLocalization.GetString(primaryKey),
            CloseButtonText = LemonLocalization.GetString("Anime_Cancel"), DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
