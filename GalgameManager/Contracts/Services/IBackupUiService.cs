using LemonVN.Infrastructure.Backup;

namespace LemonVN.Contracts.Services;

public interface IBackupUiService
{
    Task<string?> PickExportAsync();
    Task<string?> PickRestoreAsync();
    Task<bool> ConfirmRestoreAsync(PreparedBackup preview);
    Task<bool> ConfirmPurgeAsync(string title, bool finalConfirmation);
}
