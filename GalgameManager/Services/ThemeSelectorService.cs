using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using GalgameManager.Enums;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using WinRT;
using Windows.System;

namespace GalgameManager.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string SettingsKey = "AppBackgroundRequestedTheme";
    private BackgroundMaterialEnum _previousMaterial = BackgroundMaterialEnum.Mica;

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    private readonly ILocalSettingsService _localSettingsService;

    public ThemeSelectorService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        Theme = await LoadThemeFromSettingsAsync();
        await Task.CompletedTask;
    }

    public async Task SetThemeAsync(ElementTheme theme)
    {
        Theme = theme;

        await SetRequestedThemeAsync();
        await SaveThemeInSettingsAsync(Theme);
    }

    public async Task SetRequestedThemeAsync()
    {
        if (App.MainWindow!.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = Theme;
            TitleBarHelper.UpdateTitleBar(Theme);
        }

        await Task.CompletedTask;
    }

    private async Task<ElementTheme> LoadThemeFromSettingsAsync()
    {
        var themeName = await _localSettingsService.ReadSettingAsync<string>(SettingsKey);

        if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
        {
            return cacheTheme;
        }

        return ElementTheme.Default;
    }

    private async Task SaveThemeInSettingsAsync(ElementTheme theme)
    {
        await _localSettingsService.SaveSettingAsync(SettingsKey, theme.ToString());
    }

    public async Task SetBackgroundMaterialAsync()
    {
        if (App.MainWindow?.Content is not FrameworkElement )
        {
            return;
        }

        // 获取当前背景材质类型
        var material = await _localSettingsService.ReadSettingAsync<BackgroundMaterialEnum>(KeyValues.BackgroundMaterial);

        // 获取主窗口
        var appWindow = App.MainWindow;
        if (appWindow == null) return;

        if (_previousMaterial == BackgroundMaterialEnum.Glass && material != BackgroundMaterialEnum.Glass)
        {
            TransparentGlassHelper.Remove(appWindow);
        }

        // 根据材质类型设置背景
        switch (material)
        {
            case BackgroundMaterialEnum.Mica:
                appWindow.SystemBackdrop = new MicaBackdrop();
                break;
            case BackgroundMaterialEnum.MicaAlt:
                appWindow.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
                break;
            case BackgroundMaterialEnum.DesktopAcrylic:
                appWindow.SystemBackdrop = new DesktopAcrylicBackdrop();
                break;
            case BackgroundMaterialEnum.Glass:
                appWindow.SystemBackdrop = null;
                var alpha = await _localSettingsService.ReadSettingAsync<int>(KeyValues.GlassAlpha);
                var hex = await _localSettingsService.ReadSettingAsync<string>(KeyValues.GlassColor);
                var color = TransparentGlassHelper.ParseHexColor(hex);
                TransparentGlassHelper.Apply(appWindow, (byte)(alpha > 0 ? alpha : 5), color.R, color.G, color.B);
                break;
        }
        _previousMaterial = material;
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 设置导航视图是否使用透明背景，设置页面的描述文字和实际设置相反
    /// </summary>
    /// <returns></returns>
    public async Task SetNavigationViewTransparencyAsync()
    {
        if (App.MainWindow?.Content is not FrameworkElement rootElement)
        {
            return;
        }

        var useTransparentNav = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.TransparentNavigationView);
        
        var navView = rootElement.FindName("NavigationViewControl") as Microsoft.UI.Xaml.Controls.NavigationView;
        if (navView != null)
        {
            var transparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            
            // 不使用透明背景即为显示导航栏边框边框
            if (!useTransparentNav)
            {
                
                navView.Resources["NavigationViewContentBackground"] = transparentBrush;
                navView.Resources["NavigationViewContentGridBorderBrush"] = transparentBrush;
                
                navView.UpdateLayout();
            }
            else
            {
                navView.Resources.Remove("NavigationViewContentBackground");
                navView.Resources.Remove("NavigationViewContentGridBorderBrush");
                
                navView.UpdateLayout();
            }
        }

        await Task.CompletedTask;
    }
}
