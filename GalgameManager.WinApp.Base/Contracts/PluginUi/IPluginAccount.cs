using Microsoft.UI.Xaml;

namespace GalgameManager.WinApp.Base.Contracts.PluginUi;

/// <summary>
/// 如果这个插件需要在账户界面显示自己的账户项，请实现这个接口
/// </summary>
public interface IPluginAccount
{
    public FrameworkElement CreateAccountUi();
}