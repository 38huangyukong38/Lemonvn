using GalgameManager.Models.Sources;
using GalgameManager.WinApp.Base.Contracts.Dialogs;
using Microsoft.UI.Xaml;

namespace GalgameManager.WinApp.Base.Contracts;

/// <summary>
/// 声明这个插件能够提供一个游戏数据源 <br/>
/// </summary>
public interface ISourceProvider
{
    /// <summary>
    /// 这个数据源的Type，请自行拟定一个大于100的id（强制类型转换为GalgameSourceType）
    /// </summary>
    public GalgameSourceType SourceType { get; }
    
    /// <summary>
    /// 这个数据源类别的名称（如：压缩游戏库、百度云盘库等）
    /// </summary>
    public string SourceTypeName { get; }

    // 添加数据源相关的接口
    #region ADD_SOURCE

    /// <summary>
    /// 当添加数据源对话框选中这个类型的源时，返回一个UIElement作为对话框的内容 <br/>
    /// </summary>
    /// <returns></returns>
    /// <param name="dialog">对话框暴露的属性</param>
    public UIElement GetAddSourceDialogContent(IAddSourceDialog dialog);

    #endregion
}