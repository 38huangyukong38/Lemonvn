using System.Collections.Generic;
using GalgameManager.Helpers;

namespace GalgameManager.Models;

/// <summary>
/// Galgame的UID，用于唯一标识一款游戏 <br/>
/// <para>
/// 使用其Similarity方法可以计算与另一个UID的相似度，判断是否为同一款游戏
/// </para>
/// </summary>
public class GalgameUid
{
    public string? BangumiId { get; init; }
    public string? VndbId { get; init; }
    public string? YmgalId { get; init; }
    public string? PvnId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CnName { get; init; }
    public string? SteamAppId { get; init; }
    
    /// <summary>
    /// 与另一个UID的相似度，越多字段相同，相似度越高
    /// </summary>
    /// <param name="rhs"></param>
    /// <returns></returns>
    public int Similarity(GalgameUid? rhs)
    {
        if (rhs is null) return 0;
        var result = 0;
        result += !PvnId.IsNullOrEmpty() && PvnId == rhs.PvnId ? 1 : 0;
        result += !BangumiId.IsNullOrEmpty() && BangumiId == rhs.BangumiId ? 1 : 0;
        result += !VndbId.IsNullOrEmpty() && VndbId == rhs.VndbId ? 1 : 0;
        result += !YmgalId.IsNullOrEmpty() && YmgalId == rhs.YmgalId ? 1 : 0;
        result += !SteamAppId.IsNullOrEmpty() && SteamAppId == rhs.SteamAppId ? 1 : 0;
        result += !CnName.IsNullOrEmpty() && CnName == rhs.CnName ? 1 : 0;
        result += Name == rhs.Name ? 1 : 0;
        return result;
    }

    /// <summary>
    /// 是否与另一个UID相同，当且仅当双方均不为null且所有字段相同时返回true <br/>
    /// 不考虑CnName字段 <br/>
    /// <b>除非只有Name的情况，否则不要求Name相同 </b>
    /// </summary>
    /// <param name="rhs"></param>
    /// <returns></returns>
    public bool IsSame(GalgameUid? rhs)
        => GetMatchKind(rhs) is not GalgameUidMatchKind.None;

    /// <summary>
    /// 返回两个UID的匹配原因，用于区分可靠的外部Id匹配和需要用户确认的仅名称匹配。
    /// </summary>
    /// <param name="rhs">要比较的另一个游戏UID</param>
    /// <returns>匹配原因</returns>
    public GalgameUidMatchKind GetMatchKind(GalgameUid? rhs)
    {
        if (rhs is null) return GalgameUidMatchKind.None;
        var containValue = false;
        if (!BangumiId.IsNullOrEmpty() && !rhs.BangumiId.IsNullOrEmpty())
        {
            containValue = true;
            if (BangumiId != rhs.BangumiId) return GalgameUidMatchKind.None;
        }
        if (!VndbId.IsNullOrEmpty() && !rhs.VndbId.IsNullOrEmpty())
        {
            containValue = true;
            if (VndbId != rhs.VndbId) return GalgameUidMatchKind.None;
        }
        if (!YmgalId.IsNullOrEmpty() && !rhs.YmgalId.IsNullOrEmpty())
        {
            containValue = true;
            if (YmgalId != rhs.YmgalId) return GalgameUidMatchKind.None;
        }
        if (!PvnId.IsNullOrEmpty() && !rhs.PvnId.IsNullOrEmpty())
        {
            containValue = true;
            if (PvnId != rhs.PvnId) return GalgameUidMatchKind.None;
        }
        if (!SteamAppId.IsNullOrEmpty() && !rhs.SteamAppId.IsNullOrEmpty())
        {
            containValue = true;
            if (SteamAppId != rhs.SteamAppId) return GalgameUidMatchKind.None;
        }
        if (containValue) return GalgameUidMatchKind.ExternalId;
        return Name == rhs.Name ? GalgameUidMatchKind.NameOnly : GalgameUidMatchKind.None;
    }
    
    public override string ToString()
    {
        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(Name)) parts.Add($"Name: {Name}");
        if (!string.IsNullOrWhiteSpace(CnName)) parts.Add($"CnName: {CnName}");
        if (!string.IsNullOrWhiteSpace(BangumiId)) parts.Add($"BangumiId: {BangumiId}");
        if (!string.IsNullOrWhiteSpace(VndbId)) parts.Add($"VndbId: {VndbId}");
        if (!string.IsNullOrWhiteSpace(YmgalId)) parts.Add($"YmgalId: {YmgalId}");
        if (!string.IsNullOrWhiteSpace(PvnId)) parts.Add($"PvnId: {PvnId}");
        if (!string.IsNullOrWhiteSpace(SteamAppId)) parts.Add($"SteamAppId: {SteamAppId}");

        return $"GalgameUid [{string.Join(", ", parts)}]";
    }
}

public enum GalgameUidFetchMode
{
    /// 获取相似度最高的游戏
    MaxSimilarity, 
    
    /// 获取与指定UID相同<see cref="GalgameUid.IsSame"/>>的游戏
    Same,
}

/// <summary>
/// 两个游戏UID的匹配原因。
/// </summary>
public enum GalgameUidMatchKind
{
    None, // 不匹配
    NameOnly, // 仅游戏名匹配
    ExternalId, // 至少一个外部数据源Id匹配，且不存在冲突Id
}