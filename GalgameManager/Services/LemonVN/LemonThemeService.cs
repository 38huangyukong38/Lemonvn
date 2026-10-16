using GalgameManager;
using GalgameManager.Helpers;
using LemonVN.Contracts.Services;
using Microsoft.UI.Xaml;

namespace LemonVN.Services;

public sealed class LemonThemeService(IAppSettingsService settings)
{
    public void Apply()
    {
        if (App.MainWindow?.Content is not FrameworkElement root) return;
        root.RequestedTheme = settings.Theme switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        TitleBarHelper.UpdateTitleBar(root.RequestedTheme);
    }
}
