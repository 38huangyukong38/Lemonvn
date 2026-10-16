using Microsoft.UI.Xaml;
using GalgameManager.Enums;

namespace GalgameManager.Contracts.Services;

public interface IThemeSelectorService
{
    ElementTheme Theme
    {
        get;
    }

    Task InitializeAsync();

    Task SetThemeAsync(ElementTheme theme);

    Task SetRequestedThemeAsync();

    Task SetBackgroundMaterialAsync();

    Task SetNavigationViewTransparencyAsync();
}
