/*
 * 游戏源移动任务，当进入托盘模式时由SourceMoveTask恢复
 */

using GalgameManager.Helpers;
using GalgameManager.Models.Sources;

namespace GalgameManager.Models.BgTasks;

public class LocalFolderSourceMoveInTask : BgTaskBase
{
    private readonly Galgame _game; // 要复制的逻辑游戏
    private readonly string _originPath; // 来源安装实例路径
    private readonly string _targetPath; // 目标安装实例路径
    
    /// <summary>
    /// 创建本地安装实例复制任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="originPath">来源安装实例路径</param>
    /// <param name="targetPath">目标安装实例路径</param>
    public LocalFolderSourceMoveInTask(Galgame game, string originPath, string targetPath)
    {
        _game = game;
        _originPath = originPath;
        _targetPath = targetPath;
    }
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        await Task.CompletedTask;
        if (Utils.IsPathContained(_originPath, _targetPath))
            throw new PvnException("TargetPath is contained in originPath");
        
        FolderOperations.Copy(_originPath, _targetPath, info =>
        {
            ChangeProgress(0, 1, "LocalFolderSourceMoveTask_MoveIn_Progress".GetLocalized(info.FullName));
        });

        ChangeProgress(1, 1, "LocalFolderSourceMoveTask_MoveIn_Success".GetLocalized(_game.Name, _targetPath));
    }

    public override string Title { get; } = "LocalFolderSourceMoveTask_MoveIn_Title".GetLocalized();
}

public class LocalFolderSourceMoveOutTask : BgTaskBase
{
    private readonly Galgame _game;
    private readonly GalgameSourceBase _target;

    public LocalFolderSourceMoveOutTask(Galgame game, GalgameSourceBase target)
    {
        _game = game;
        _target = target;
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        await Task.CompletedTask;
        var root = _target.GetPath(_game);
        if (root is null) throw new PvnException("root is null"); //不应该发生
        if (!Directory.Exists(root)) throw new PvnException($"{root} not exists");
        
        FolderOperations.Delete(root, info =>
        {
            ChangeProgress(0, 1, "LocalFolderSourceMoveTask_MoveOut_Progress".GetLocalized(info.FullName));
        });
        
        ChangeProgress(1, 1, "LocalFolderSourceMoveTask_MoveOut_Success".GetLocalized(_game.Name, _target.Url));
    }

    public override string Title { get; } = "LocalFolderSourceMoveTask_MoveOut_Title".GetLocalized();
}