using Microsoft.UI.Xaml;

namespace GalgameManager.WinApp.Base.Contracts.PluginUi;

/// <summary>
/// 如果这个插件需要在插件界面显示自己的设置项，请实现这个接口
/// </summary>
public interface IPluginSetting
{
    public FrameworkElement CreateSettingUi();
}