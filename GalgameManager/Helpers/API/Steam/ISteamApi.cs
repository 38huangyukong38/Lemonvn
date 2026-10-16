using Refit;

namespace GalgameManager.Helpers.API.Steam;

public interface ISteamApi
{
    [Get("/ISteamUser/GetPlayerSummaries/v0002/?key={key}&steamids={steamids}")]
    Task<SteamResponseDto<SteamAccountResponseDto>> GetPlayerSummariesAsync(string key, string steamids);

    [Get("/IPlayerService/GetOwnedGames/v0001/?key={key}&steamid={steamid}&include_appinfo={include_appinfo}")]
    Task<SteamResponseDto<SteamGameListResponseDto>> GetOwnedGamesAsync(string key, string steamid, bool include_appinfo = true);
}

public interface ISteamStoreApi
{
    /// <summary>
    /// 获取一个或多个App的详细信息
    /// </summary>
    /// <param name="appids">App ID，多个ID用逗号分隔</param>
    /// <param name="language">返回的语言，如 schinese (简体中文)</param>
    /// <returns>一个以App ID为键，游戏信息为值的字典</returns>
    [Get("/api/appdetails")]
    Task<Dictionary<string, SteamAppDetailResponse>> GetAppDetailsAsync([AliasAs("appids")] string appids, [AliasAs("l")] string language);
}