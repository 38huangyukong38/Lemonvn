using LemonVN.Contracts.Services;

namespace LemonVN.Services;

/// <summary>
/// LemonVN owns one data root. Computing paths does not create files, open a
/// database, or read PotatoVN environment variables or migration settings.
/// </summary>
public sealed class AppDataPathService : IAppDataPathService
{
    public string RootPath { get; }
    public string DatabasePath => Path.Combine(RootPath, "lemonvn.db");
    public string SettingsPath => Path.Combine(RootPath, "Settings");
    public string CoversPath => Path.Combine(RootPath, "Covers");
    public string CachePath => Path.Combine(RootPath, "Cache");
    public string BackupsPath => Path.Combine(RootPath, "Backups");
    public string LogsPath => Path.Combine(RootPath, "Logs");
    public string TempPath => Path.Combine(RootPath, "Temp");
    public string UserDataPath => Path.Combine(RootPath, "UserData");
    public string ExportsPath => Path.Combine(RootPath, "Exports");

    public static AppDataPathService ForPackage(string packageName, string packageLocalFolder)
    {
        if (!string.Equals(packageName, AppIdentity.PackageName, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to use another application's package data.");
        if (!Path.IsPathFullyQualified(packageLocalFolder))
            throw new ArgumentException("The package data directory must be absolute.", nameof(packageLocalFolder));
        return new AppDataPathService(Path.Combine(packageLocalFolder, "LemonVN"));
    }

    // An explicit root supports isolated tests and future application-owned configuration.
    public AppDataPathService(string? dataRoot = null)
    {
        if (dataRoot is null)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                throw new InvalidOperationException("The local application data directory is unavailable.");
            dataRoot = Path.Combine(localAppData, "LemonVN");
        }

        if (string.IsNullOrWhiteSpace(dataRoot) || !Path.IsPathFullyQualified(dataRoot))
            throw new ArgumentException("The application data root must be an absolute directory path.", nameof(dataRoot));

        RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        if (string.Equals(RootPath, Path.GetPathRoot(RootPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The application data root must not be a volume root.", nameof(dataRoot));
    }

    // Called explicitly by the future LemonVN startup flow. Failures propagate to
    // its error boundary; existing files are never removed or rebuilt here.
    public void EnsureDirectories()
    {
        foreach (var directory in new[] { RootPath, SettingsPath, CoversPath, CachePath, BackupsPath, LogsPath, TempPath, UserDataPath, ExportsPath })
            Directory.CreateDirectory(directory);
    }
}
