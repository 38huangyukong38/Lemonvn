using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace GalgameManager.Helpers.API.Steam;
public class SteamResponseDto<T>
{
    [JsonProperty("response")]
    public required T response { get; set; }
}

public class SteamAccountResponseDto
{
    [JsonProperty("players")]
    public List<SteamAccountDto> players { get; set; } = new List<SteamAccountDto>();
}

public class SteamAccountDto
{
    [JsonProperty("steamid")]
    public string steamId { get; set; } = string.Empty;

    [JsonProperty("communityvisibilitystate")]
    public int communityVisibilityState { get; set; }

    [JsonProperty("profilestate")]
    public int profileState { get; set; }

    [JsonProperty("personaname")]
    public string personaName { get; set; } = string.Empty;

    [JsonProperty("commentpermission")]
    public int commentPermission { get; set; }

    [JsonProperty("profileurl")]
    public string profileUrl { get; set; } = string.Empty;

    [JsonProperty("avatar")]
    public string Avatar { get; set; } = string.Empty;

    [JsonProperty("avatarmedium")]
    public string avatarMedium { get; set; } = string.Empty;

    [JsonProperty("avatarfull")]
    public string avatarFull { get; set; } = string.Empty;

    [JsonProperty("avatarhash")]
    public string avatarHash { get; set; } = string.Empty;

    [JsonProperty("lastlogoff")]
    public int LastLogOff { get; set; }

    [JsonProperty("personastate")]
    public int PersonaState { get; set; }

    [JsonProperty("primaryclanid")]
    public string PrimaryClanId { get; set; } = string.Empty;

    [JsonProperty("timecreated")]
    public int TimeCreated { get; set; }

    [JsonProperty("personastateflags")]
    public int PersonaStateFlags { get; set; }

    [JsonProperty("loccountrycode")]
    public string? LocCountryCode { get; set; }
}
public class SteamGameListResponseDto
{
    [JsonProperty("game_count")]
    public int gameCount { get; set; }

    [JsonProperty("games")]
    public List<SteamGameDto> games { get; set; } = new List<SteamGameDto>();
}
public class SteamGameDto
{
    [JsonProperty("rtime_last_played")]
    public long rtime_last_played;

    [JsonProperty("appid")]
    public int appid { get; set; }

    [JsonProperty("name")]
    public string name { get; set; } = string.Empty;

    [JsonProperty("playtime_2weeks")]
    public int playtime_2weeks { get; set; }

    [JsonProperty("playtime_forever")]
    public int playtime_forever { get; set; }

    [JsonProperty("img_icon_url")] 
    public string img_icon_url { get; set; } = string.Empty;
}


/// <summary>
/// Steam App详细信息接口返回的根对象的值部分
/// </summary>
public class SteamAppDetailResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("data")]
    public SteamAppDetailDataDto? Data { get; set; }
}

/// <summary>
/// 包含具体游戏信息的对象
/// </summary>
public class SteamAppDetailDataDto
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("steam_appid")]
    public int SteamAppid { get; set; }

    [JsonProperty("is_free")]
    public bool IsFree { get; set; }

    [JsonProperty("short_description")]
    public string? ShortDescription { get; set; }
    
    [JsonProperty("detailed_description")]
    public string? DetailedDescription { get; set; }

    [JsonProperty("header_image")]
    public string? HeaderImage { get; set; }

    [JsonProperty("website")]
    public string? Website { get; set; }

    [JsonProperty("developers")]
    public List<string>? Developers { get; set; }

    [JsonProperty("publishers")]
    public List<string>? Publishers { get; set; }

    [JsonProperty("platforms")]
    public PlatformsDto? Platforms { get; set; }

    [JsonProperty("genres")]
    public List<GenreDto>? Genres { get; set; }

    [JsonProperty("screenshots")]
    public List<ScreenshotDto>? Screenshots { get; set; }

    [JsonProperty("release_date")]
    public ReleaseDateDto? ReleaseDate { get; set; }

    [JsonProperty("background")]
    public string? Background { get; set; }
    
    [JsonProperty("background_raw")]
    public string? BackgroundRaw { get; set; }
}

public class PlatformsDto
{
    [JsonProperty("windows")]
    public bool Windows { get; set; }

    [JsonProperty("mac")]
    public bool Mac { get; set; }

    [JsonProperty("linux")]
    public bool Linux { get; set; }
}

public class GenreDto
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
}

public class ScreenshotDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("path_thumbnail")]
    public string? PathThumbnail { get; set; }

    [JsonProperty("path_full")]
    public string? PathFull { get; set; }
}

public class ReleaseDateDto
{
    [JsonProperty("coming_soon")]
    public bool ComingSoon { get; set; }

    [JsonProperty("date")]
    public string? Date { get; set; }
}
