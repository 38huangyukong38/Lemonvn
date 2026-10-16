namespace LemonVN.Contracts.Services;

public interface IAppDataPathService
{
    string RootPath { get; }
    string DatabasePath { get; }
    string SettingsPath { get; }
    string CoversPath { get; }
    string CachePath { get; }
    string BackupsPath { get; }
    string LogsPath { get; }
    string TempPath { get; }
    string UserDataPath { get; }
    string ExportsPath { get; }

    void EnsureDirectories();
}
