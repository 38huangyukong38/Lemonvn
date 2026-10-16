using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using PotatoVN.Client.Client;
using PotatoVN.Client.Model;

namespace GalgameManager.Contracts.Services;

public interface IPvnService
{
    public Uri BaseUri { get; }

    public Action<PvnServiceStatus>? StatusChanged { get; set; }
    
    public PvnSyncTask? SyncTask { get; }

    public void Startup();

    public Task<PvnServerInfo?> GetServerInfoAsync();

    public Task<PvnAccount?> LoginAsync(string username, string password);

    public Task<PvnAccount?> RegisterAsync(string username, string password);

    public Task<PvnAccount?> LoginViaBangumiAsync();
    
    public Task<PvnAccount?> RefreshTokenAsync();

    public Task<PvnAccount?> ModifyAccountAsync(string? userDisplayName = null, string? avatarPath = null,
        string? newPassword = null, string? oldPassword = null);
    
    public Task<long> GetLastGalChangedTimeStampAsync();
    
    public Task<List<GalgameDto>> GetChangedGalgamesAsync();
    
    public Task<List<int>> GetDeletedGalgamesAsync();

    public void SyncGames();
    
    public void Upload(Galgame galgame, PvnUploadProperties properties);
    
    /// <summary>
    /// 不要调用它，这个函数只有PvnSyncTask才能调用
    /// </summary>
    public Task DeleteInternal(int pvnId);

    public Task LogOutAsync();

    /// <summary>
    /// 上传文件到OSS
    /// </summary>
    /// <param name="filePath">若为null或文件不存在则什么都不做</param>
    /// <param name="targetName">OSS上目标文件名，不需要填拓展名，若不填则用原文件名</param>
    /// <param name="onFailed"></param>
    /// <returns>文件在OSS中的路径，若失败返回null</returns>
    public Task<string?> UploadFileAsync(string? filePath, string? targetName = null,
        Action<Exception>? onFailed = null);
    
    /// <summary>
    /// 获取api调用配置
    /// </summary>
    /// <returns></returns>
    public Task<Configuration> GetConfigAsync();
}