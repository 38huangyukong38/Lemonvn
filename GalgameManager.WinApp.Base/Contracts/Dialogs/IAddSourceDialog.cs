using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.WinApp.Base.Contracts.Dialogs;

/// <summary>
/// 这个接口会暴露插件需要控制的添加数据源对话框的数据 <br/>
/// </summary>
public interface IAddSourceDialog
{
    /// <summary>
    /// 要添加的源的路径，当它不为空字符串时确认按钮才可用
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 如果message为empty，则关闭infobar显示
    /// </summary>
    /// <param name="severity"></param>
    /// <param name="message"></param>
    public void DisplayMsg(InfoBarSeverity severity, string message);
}