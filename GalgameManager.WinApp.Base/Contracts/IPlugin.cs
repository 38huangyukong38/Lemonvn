using System;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.WinApp.Base.Models;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IPlugin
{
    /// <summary>
    /// 返回本插件的信息（ID、插件名、支持版本等） <p/>
    /// 这个属性必须确保任何时候都可用（比如说插件没被加载的时候）
    /// </summary>
    public PluginInfo Info { get; }
    
    /// <summary>
    /// 插件加载时会被调用
    /// </summary>
    /// <returns></returns>
    public Task InitializeAsync(IPotatoVnApi hostApi);

    /// <summary>
    /// 插件被卸载的时候会被调用，用于清理一些插件相关的外部配置和数据
    /// </summary>
    /// <remarks>
    /// 对于开发阶段的插件可以在这个接口中实现热重载相关逻辑方便开发。
    /// 插件默认最多用时 5s，可以通过 extendWaitHandler 延长，但是无法超过 60s
    /// </remarks>
    /// <returns></returns>
    public Task OnUninstallAsync(bool deleteData, Action<TimeSpan> extendWaitHandler, CancellationToken cts)
    {
        if (cts.IsCancellationRequested) return Task.FromCanceled(cts);
        return Task.CompletedTask;
    }
}