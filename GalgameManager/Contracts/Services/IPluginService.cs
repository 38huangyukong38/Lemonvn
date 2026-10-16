using System.Collections.ObjectModel;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IPluginService
{
    /// <summary>
    /// 新增插件，注意捕获异常
    /// </summary>
    /// <param name="path"></param>
    /// <param name="isDev"> 是否以 Dev 模式加载插件 </param>
    /// <exception cref="PvnException">已知错误</exception>
    /// <returns></returns>
    public Task AddPluginAsync(string path, bool isDev);

    /// <summary>
    /// 标记一个插件要卸载，下次启动时会卸载插件。 <br/>
    /// 对于开发者的插件不会删除文件，只会取消加载并从插件列表移除 <br/>
    /// （注意，对于以压缩包安装的测试包，也会删除掉其在程序数据目录下的解压目录）
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="deleteData">是否删除插件数据</param>
    /// <returns></returns>
    public Task DeletePluginAsync(PluginX plugin, bool deleteData);

    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync();

    public Task InitAsync();

    /// <summary>
    /// 加载某个插件，如果已经加载则不操作，注意捕获异常
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="load">是否要加载插件，若设置为false则只把插件加到插件列表里而不加载（初始化插件表时用）</param>
    /// <returns></returns>
    public Task LoadPluginAsync(PluginX plugin, bool load);

    /// <summary>
    /// 把加载失败的插件加入插件列表，让用户可以通过插件列表来卸载插件
    /// </summary>
    /// <param name="pluginX"></param>
    /// <returns></returns>
    public Task LoadFailedPluginAsync(PluginX pluginX);

    /// <summary>
    /// 立即删除插件的关联数据
    /// </summary>
    /// <param name="plugin"></param>
    public void PluginDeleteData(PluginX plugin);

    /// <summary>
    /// 插件存放目录
    /// </summary>
    public DirectoryInfo PluginDir { get; }

    /// <summary>
    /// 检查插件是否在数据库里
    /// </summary>
    /// <param name="pluginId"></param>
    /// <returns></returns>
    public bool PluginInDb (Guid pluginId);

    /// <summary>
    /// 是否有正在等待取消加载/删除的插件
    /// </summary>
    public bool PluginOffloadInProgress { get; }

    /// <summary>
    /// 立即设置插件的关联数据
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="data"></param>
    public void PluginSetData(PluginX plugin, string? data);

    /// <summary>
    /// 保存插件信息
    /// </summary>
    /// <param name="plugin"></param>
    public void SavePlugin(PluginX plugin);

    /// <summary>
    /// 直接操作数据库，设置某个插件的版本号
    /// 这个函数只用于升级插件任务：因为升级插件时加载插件时插件不会走正常的加载程序，而是等待后续的加载任务把升级过后的插件加载进来
    /// 这个时候任务列表里还没有插件，但需要修改插件版本号，只能直接编辑数据库
    /// </summary>
    /// <param name="pluginId"></param>
    /// <param name="version"></param>
    public void SetPluginVersion (Guid pluginId, Version version);

    /// <summary>
    /// 当调用插件功能抛出异常时可以调用此方法提醒用户（内部会调用infoService发送一个Event）
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="e"></param>
    /// <param name="msgHeader">消息头，最后消息会这样组合：mesHeader+"建议联系插件开发者了解解决方案~\n\n以下为详细报错：\n"</param>
    public void ThrowPluginExceptionEvent(PluginX plugin, Exception e, string msgHeader);
}
