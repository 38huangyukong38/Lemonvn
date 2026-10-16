using GalgameManager.Models.Sources;

namespace GalgameManager.Models;

/// <summary>
/// 单个安装目录中的版本化游戏元数据备份。
/// </summary>
public sealed class GameMetaBackup
{
    public const int CurrentVersion = 2; // 当前备份格式版本

    public int Version { get; set; } // 备份格式版本
    public Galgame? Game { get; set; } // 逻辑游戏快照
    public LocalInstallationConfig? Installation { get; set; } // 当前目录对应的安装配置
}
