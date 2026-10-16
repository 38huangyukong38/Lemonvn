namespace GalgameManager.Contracts.Services;

public interface IUpdateService
{
    /// <summary>
    /// 是否应该显示更新内容(每个版本只显示一次)
    /// </summary>
    public bool ShouldDisplayUpdateContent();
    
    /// <summary>
    /// 获取最新版本号 <br/>
    /// <b>每次启动只检查一次</b>，若已获取过最新版本号则直接返回缓存值<br/>
    /// 如果已经是最新版本，则返回null
    /// </summary>
    /// <returns>如果找不到返回null</returns>
    public Task<Version?> GetLatestVersionAsync();
    
    /// <summary>
    /// 检查是否有可用更新
    /// </summary>
    /// <returns></returns>
    public Task<bool> IsUpdateAvailableAsync();
    
    /// <summary>
    /// 更新更行提醒的小蓝点
    /// </summary>
    public Task UpdateSettingsBadgeAsync();

    /// <summary>
    /// 检查是否有新版本且未被忽略
    /// </summary>
    /// <returns>有可用更新返回版本号，否则返回null</returns>
    public Task<string?> GetAvailableUpdateVersionAsync();

    /// <summary>
    /// 显示更新确认对话框
    /// </summary>
    /// <returns>用户选择：0=取消，1=立即更新，2=忽略这个版本</returns>
    public Task<int> ShowUpdateConfirmationAsync();

    /// <summary>
    /// 忽略指定版本的更新
    /// </summary>
    /// <param name="version">要忽略的版本号</param>
    public Task IgnoreVersionAsync(string version);

    /// <summary>
    /// 执行更新操作
    /// </summary>
    public Task PerformUpdateAsync();
    
    public event Action<bool>? SettingBadgeEvent;

    void SetUpdateCancelledThisSession();
}