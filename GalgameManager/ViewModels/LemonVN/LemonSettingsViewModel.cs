using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Helpers;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using LemonVN.Services;

namespace LemonVN.ViewModels;

public partial class LemonSettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settings;
    private readonly LemonThemeService _theme;
    private readonly AppLogService _log;
    public string DataPath { get; }
    public BackupSettingsViewModel Transfer { get; }
    public string Version => $"LemonVN {RuntimeHelper.GetVersion()}";
    [ObservableProperty] private int _selectedThemeIndex;
    [ObservableProperty] private bool _bangumiEnabled;
    [ObservableProperty] private bool _isMessageOpen;
    [ObservableProperty] private string _message = string.Empty;

    public LemonSettingsViewModel(IAppDataPathService paths, IAppSettingsService settings,
        LemonThemeService theme, AppLogService log, BackupSettingsViewModel transfer)
    {
        DataPath = paths.RootPath;
        Transfer = transfer;
        _settings = settings;
        _theme = theme;
        _log = log;
        SelectedThemeIndex = (int)settings.Theme;
        BangumiEnabled = settings.BangumiEnabled;
    }

    [RelayCommand]
    private void SaveBangumi()
    {
        try { _settings.SaveBangumiEnabled(BangumiEnabled); Message = LemonLocalization.GetString("LemonSettings_Saved"); }
        catch (Exception error)
        {
            BangumiEnabled = _settings.BangumiEnabled;
            _log.Error(error, "Saving network preference failed.");
            Message = LemonLocalization.GetString("LemonSettings_SaveFailed", error.Message);
        }
        IsMessageOpen = true;
    }

    [RelayCommand]
    private void SaveTheme()
    {
        try
        {
            _settings.SaveTheme((ThemePreference)SelectedThemeIndex);
            _theme.Apply();
            Message = LemonLocalization.GetString("LemonSettings_Saved");
        }
        catch (Exception error)
        {
            _log.Error(error, "Saving preferences failed; original data preserved.");
            Message = LemonLocalization.GetString("LemonSettings_SaveFailed", error.Message);
        }
        IsMessageOpen = true;
    }
}
