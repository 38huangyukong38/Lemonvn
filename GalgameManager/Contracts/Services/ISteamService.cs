using GalgameManager.Helpers.API.Steam;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface ISteamService
{
    /// <summary>
    /// 提供一个初始化的方法
    /// </summary>
    /// <returns></returns>
    public Task InitAsync(string steamid);


    /// <summary>
    /// 取得steam账号
    /// </summary>
    /// 
    /// <returns></returns>
    public Task<SteamAccountDto> GetSteamAccountAsync();

    /// <summary>
    /// 得到所有的steam游戏
    /// </summary>
    /// <returns></returns>
    public Task<List<SteamGameDto>> GetSteamGameListResponseAsync();

    /// <summary>
    /// 获取galgame列表
    /// </summary>
    /// <returns></returns>
    public Task<List<SteamGameDto>> GetGalgameListResponseAsync();

    /// <summary>
    /// 更新steam游戏的方法
    /// </summary>
    /// <param name="galgame"></param>
    /// <returns></returns>
    public Task UpdateSteamGalGameAsync(List<SteamGameDto> list, Galgame galgame);
}
