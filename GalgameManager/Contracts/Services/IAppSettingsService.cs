namespace LemonVN.Contracts.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public interface IAppSettingsService
{
    ThemePreference Theme { get; }
    bool BangumiEnabled { get; }
    void Initialize();
    void SaveTheme(ThemePreference theme);
    void SaveBangumiEnabled(bool enabled);
}
