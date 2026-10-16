using System.Threading.Tasks;
using GalgameManager.Models;
using Microsoft.UI.Xaml;

namespace GalgameManager.WinApp.Base.Contracts.PluginUi;

/// <summary>
/// 自定义Galgame详情页，实现该接口表示本插件能够实现Galgame详情页（会替换掉原版界面）
/// </summary>
public interface IGalgamePage
{
    /// <summary>
    /// 创建Galgame详情页UI
    /// </summary>
    /// <param name="game">要显示的游戏</param>
    /// <returns></returns>
    Task<FrameworkElement> CreateUiAsync(Galgame game);
}

/// <summary>
/// 向Galgame详情页左侧面板追加UI，实现该接口表示本插件能够在原版详情页左侧面板注入内容
/// </summary>
public interface IGalgamePageLeftPanel
{
    /// <summary>
    /// 创建Galgame详情页左侧面板追加UI
    /// </summary>
    /// <param name="game">要显示的游戏</param>
    /// <returns></returns>
    Task<FrameworkElement> CreateLeftPanelUiAsync(Galgame game);
}

/// <summary>
/// 向Galgame详情页右侧面板追加UI，实现该接口表示本插件能够在原版详情页右侧面板注入内容
/// </summary>
public interface IGalgamePageRightPanel
{
    /// <summary>
    /// 创建Galgame详情页右侧面板追加UI
    /// </summary>
    /// <param name="game">要显示的游戏</param>
    /// <returns></returns>
    Task<FrameworkElement> CreateRightPanelUiAsync(Galgame game);
}

public interface IGalgamePageSetting
{
    /// <summary>
    /// 设置自定义界面，如果你的插件所提供的Galgame详情页可以进行设置，请实现该方法
    /// 这个函数会在用户点击折叠菜单中的”自定义页面布局“时被调用
    /// </summary>
    /// <returns></returns>
    Task SettingAsync();
}
