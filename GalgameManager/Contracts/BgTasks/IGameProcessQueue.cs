using GalgameManager.Models;

namespace GalgameManager.Contracts.BgTasks;

public interface IGameProcessQueue
{
    /// <summary>
    /// 添加一个游戏到操作队列中
    /// </summary>
    /// <param name="game"></param>
    public void AddGalgame(Galgame? game);
}