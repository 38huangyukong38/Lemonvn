using LemonVN.ViewModels.Anime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using LemonVN.Infrastructure.Backup;
using LemonVN.Services;

namespace LemonVN.ViewModels;

public partial class BackupSettingsViewModel(AnimeBackupService backup, IBackupUiService dialogs,
    INavigationService navigation, AppLogService log, Func<string, object[], string>? translate = null) : ObservableObject
{
    private readonly Func<string, object[], string> _translate = translate ?? LemonLocalization.GetString;
    private CancellationTokenSource? _cancel;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanOperate))] private bool _isBusy;
    [ObservableProperty] private bool _isMessageOpen;
    [ObservableProperty] private string _message = string.Empty;
    public bool CanOperate => !IsBusy;

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        _cancel = new();
        try
        {
            string? file = await dialogs.PickExportAsync();
            if (file is null) return;
            Message = Text("Backup_Exporting");
            IsMessageOpen = true;
            CancellationToken token = _cancel.Token;
            await Task.Run(() => backup.ExportAsync(file, token));
            Message = Text("Backup_Exported", file);
        }
        catch (OperationCanceledException) { Message = Text("Backup_Cancelled"); IsMessageOpen = true; }
        catch (Exception error) { Report(error); }
        finally { _cancel?.Dispose(); _cancel = null; IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        _cancel = new();
        try
        {
            string? file = await dialogs.PickRestoreAsync();
            if (file is null) return;
            Message = Text("Backup_Validating");
            IsMessageOpen = true;
            CancellationToken token = _cancel.Token;
            using PreparedBackup plan = await Task.Run(() => backup.PrepareAsync(file, token));
            if (!await dialogs.ConfirmRestoreAsync(plan)) { Message = Text("Backup_Cancelled"); return; }
            Message = Text("Backup_Restoring");
            BackupRestoreResult result = await Task.Run(() => backup.RestoreAsync(plan, token));
            Message = Text("Backup_Restored", result.AnimeCount, result.TrashCount, result.RecoveryBackupPath);
            navigation.NavigateTo(typeof(AnimeLibraryViewModel).FullName!, result, clearNavigation: true);
        }
        catch (OperationCanceledException) { Message = Text("Backup_Cancelled"); IsMessageOpen = true; }
        catch (Exception error) { Report(error); }
        finally { _cancel?.Dispose(); _cancel = null; IsBusy = false; }
    }

    [RelayCommand] private void Cancel() => _cancel?.Cancel();
    private string Text(string key, params object[] args) => _translate(key, args);
    private void Report(Exception error)
    {
        log.Error(error, "Backup operation failed; existing data was preserved.");
        Message = Text("Backup_Failed", error.Message);
        IsMessageOpen = true;
    }
}
