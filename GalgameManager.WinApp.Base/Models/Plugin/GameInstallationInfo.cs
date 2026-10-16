using System;
using GalgameManager.Models.Sources;

namespace GalgameManager.WinApp.Base.Models.Plugin;

/// <summary>
/// 提供给插件的本地安装实例只读快照。
/// </summary>
/// <param name="EntryId">库内游戏条目Id</param>
/// <param name="SourceId">所属游戏库Id</param>
/// <param name="SourceType">游戏库类型</param>
/// <param name="SourceName">游戏库名称</param>
/// <param name="Path">安装路径</param>
/// <param name="IsPreferred">是否为首选安装实例</param>
/// <param name="IsAvailable">安装路径当前是否可用</param>
public sealed record GameInstallationInfo(
    Guid EntryId,
    Guid SourceId,
    GalgameSourceType SourceType,
    string SourceName,
    string Path,
    bool IsPreferred,
    bool IsAvailable);
