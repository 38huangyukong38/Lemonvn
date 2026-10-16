using System.Text.Json;
using LemonVN.Contracts.Services;

namespace LemonVN.Services;

/// <summary>Small local preferences, identical in packaged and unpackaged builds.
/// Unknown/corrupt formats fail visibly; they are never silently reset or migrated.</summary>
public sealed class AppSettingsService(IAppDataPathService paths) : IAppSettingsService
{
    private readonly object _gate = new();
    private bool _initialized;
    private ThemePreference _theme;
    private bool _bangumiEnabled = true;
    public string FilePath => Path.Combine(paths.SettingsPath, "settings.json");

    public ThemePreference Theme
    {
        get
        {
            lock (_gate)
            {
                if (!_initialized) throw new InvalidOperationException("Settings have not been initialized.");
                return _theme;
            }
        }
    }

    public bool BangumiEnabled
    {
        get { lock (_gate) { if (!_initialized) throw new InvalidOperationException("Settings have not been initialized."); return _bangumiEnabled; } }
    }

    public void Initialize()
    {
        lock (_gate)
        {
            if (_initialized) return;
            if (File.Exists(FilePath))
            {
                SettingsDocument document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(FilePath))
                    ?? throw new InvalidDataException("The settings document is empty.");
                if (document.FormatVersion != 1 || document.Application != AppIdentity.PackageName ||
                    !Enum.IsDefined(document.Theme))
                    throw new InvalidDataException("Unsupported settings format or application identity. Original data was preserved.");
                _theme = document.Theme;
                _bangumiEnabled = document.BangumiEnabled;
            }
            _initialized = true;
        }
    }

    public void SaveTheme(ThemePreference theme)
    {
        if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
        lock (_gate)
            Save(theme, _bangumiEnabled);
    }

    public void SaveBangumiEnabled(bool enabled)
    {
        lock (_gate) Save(_theme, enabled);
    }

    private void Save(ThemePreference theme, bool bangumiEnabled)
    {
        lock (_gate)
        {
            if (!_initialized) throw new InvalidOperationException("Settings have not been initialized.");
            Directory.CreateDirectory(paths.SettingsPath);
            var temporary = Path.Combine(paths.SettingsPath, $"settings.{Guid.NewGuid():N}.tmp");
            try
            {
                byte[] content = JsonSerializer.SerializeToUtf8Bytes(new SettingsDocument
                    { Application = AppIdentity.PackageName, FormatVersion = 1, Theme = theme, BangumiEnabled = bangumiEnabled });
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(content);
                    stream.Flush(flushToDisk: true);
                }
                if (File.Exists(FilePath))
                    File.Replace(temporary, FilePath, Path.Combine(paths.SettingsPath, "settings.backup.json"));
                else
                    File.Move(temporary, FilePath);
                _theme = theme; // Publish only after durable storage succeeds.
                _bangumiEnabled = bangumiEnabled;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    private sealed class SettingsDocument
    {
        public required string Application { get; set; }
        public required int FormatVersion { get; set; }
        public required ThemePreference Theme { get; set; }
        public bool BangumiEnabled { get; set; } = true; // Existing format-1 settings remain compatible.
    }
}
