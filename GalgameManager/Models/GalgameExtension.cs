using GalgameManager.Helpers;
using GalgameManager.Models.Sources;

namespace GalgameManager.Models;

public static class GalgameExtension
{
    /// <summary>
    /// 试图从游戏根目录中找到存档位置（仅能找到已同步到服务器的存档）
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="installation">目标安装实例；为null时使用首选实例</param>
    public static void FindSaveInPath(this Galgame game, GalgameAndPath? installation = null)
    {
        installation ??= game.PreferredLocalInstallation;
        if (installation is null || !Directory.Exists(installation.Path)) return;
        string path = installation.Path;
        try
        {
            var cnt = 0;
            string? result = null;
            foreach (var subDir in Directory.GetDirectories(path))
                if (FolderOperations.IsSymbolicLink(subDir))
                {
                    cnt++;
                    result = subDir;
                }
            if (cnt == 1)
            {
                installation.LocalConfig ??= new LocalInstallationConfig();
                installation.LocalConfig.SavePath = result;
            }
        }
        catch (Exception e)
        {
            game.ErrorOccurred?.Invoke(e);
        }
    }

    public static bool ApplySearchKey(this Galgame game, string searchKey)
    {
        return Contain(game.Name.Value) ||
               Contain(game.ChineseName.Value) ||
               Contain(game.OriginalName.Value) ||
               Contain(game.Developer.Value) ||
               (game.Tags.Value??[]).Any(Contain);

        bool Contain(string? str)
        {
            try
            {
                return str is not null && str.ContainX(searchKey);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
