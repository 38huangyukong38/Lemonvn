using GalgameManager.Models;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IFilter
{
    /// <summary>
    /// 检查是否符合过滤条件
    /// </summary>
    /// <param name="galgame">待检查游戏</param>
    /// <returns>是否符合过滤条件</returns>
    bool Apply(Galgame galgame);
}
