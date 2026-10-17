// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;


namespace GalgameManager.Models.BgTasks;

public class UnpackGameTask : BgTaskBase
{
    public string PackPath = string.Empty;
    public string? Password = string.Empty;
    public string GameName = string.Empty;
    public string TargetPath = null!;
    /// 临时解压文件夹，序列化用，防止启动任务后最小化到后台后重新生成任务时再次生成新的临时文件夹
    public string TmpDirName = Path.GetRandomFileName(); 
    
    private StorageFile? _pack;

    public UnpackGameTask() { }
    
    /// <param name="pack">压缩包</param>
    /// <param name="targetPath">解压位置，最终会解压到：targetPath/gameName</param>
    /// <param name="gameName"></param>
    /// <param name="password">压缩包密码，若没有可不填</param>
    public UnpackGameTask(StorageFile pack, string targetPath,string gameName, string? password = null)
    {
        _pack = pack;
        TargetPath = targetPath;
        PackPath = pack.Path;
        GameName = gameName;
        Password = password;
    }
    
    // LemonVN modification: the native game-only backend is deliberately absent.
    // Reject both execution and persisted-task recovery before opening files or
    // deleting an old temporary directory. AnimeBackupService has its own .NET ZIP path.
    private void RejectNativeGameArchive()
    {
        string extension = Path.GetExtension(_pack?.Path ?? PackPath);
        if (extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) || extension.Equals(".001", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Native game archive extraction is disabled in LemonVN; anime backups use the separate ZIP service.");
    }

    protected async override Task RecoverFromJsonInternal()
    {
        RejectNativeGameArchive();
        try
        {
            _pack = await StorageFile.GetFileFromPathAsync(PackPath);
            if (Directory.Exists(Path.Combine(TargetPath, TmpDirName)))
                Directory.Delete(Path.Combine(TargetPath, TmpDirName), true);
        }
        catch
        {
            //ignore
        }
    }

    /// 注意：必须为async方法，前置检查的异常才会被捕获进返回的Task（BgTaskBase.Task），
    /// 否则同步抛出的异常会导致Task属性停留在默认的CompletedTask，调用方无法感知失败
    protected override async Task RunInternal()
    {
        RejectNativeGameArchive();
        if(_pack is null || GameName == string.Empty) return;
        if (!Directory.Exists(TargetPath))
            throw new PvnException("GalgameFolder_UnpackGame_PathNotExist".GetLocalized(TargetPath));
        var saveDirectory = Path.Combine(TargetPath, GameName); // 游戏保存路径
        if (Directory.Exists(saveDirectory))
            throw new PvnException("GalgameFolder_UnpackGame_PathNotEmpty".GetLocalized(saveDirectory));

        var tmpDir = Path.Combine(TargetPath, TmpDirName);
        Directory.CreateDirectory(tmpDir); // 临时解压路径，若没有权限则在此步抛异常报错

        await Task.Run((async Task() =>
        {
            try
            {
                ChangeProgress(0, 1, "GalgameFolder_UnpackGame_Start".GetLocalized());
                await UnZip(tmpDir);

                if (Directory.GetDirectories(tmpDir).Length == 1) // 压缩包内为完整的文件夹
                    Directory.Move(Directory.GetDirectories(tmpDir)[0], saveDirectory);
                else // 压缩包内为零散文件
                    Directory.Move(tmpDir, saveDirectory);

                ChangeProgress(0, 1, "GalgameFolder_UnpackGame_Done".GetLocalized());
                await UiThreadInvokeHelper.InvokeAsync(async Task () =>
                {
                    // AddGameAsync内部会优先尝试从本地meta备份（含压缩库sidecar）读取游戏信息，无需联网解析
                    await App.GetService<IGalgameCollectionService>()
                        .AddGameAsync(GalgameSourceType.LocalFolder, saveDirectory, true);
                });
                ChangeProgress(1, 1, string.Empty);

                if (StartFromBg && await App.GetService<ILocalSettingsService>()
                                    .ReadSettingAsync<bool>(KeyValues.NotifyWhenUnpackGame)
                                && App.MainWindow is null)
                {
                    App.SystemTray?.ShowNotification("UnpackGameTask_Done_Title".GetLocalized(),
                        "UnpackGameTask_Done_Msg".GetLocalized(GameName, saveDirectory));
                }
            }
            catch (Exception) //密码错误或压缩包损坏
            {
                ChangeProgress(-1, 1, string.Empty);
            }
            finally
            {
                if (Directory.Exists(tmpDir)) // 删除解压失败的文件夹
                    Directory.Delete(tmpDir, true);
            }
        })!);
    }
    private async Task UnZip(string tmpDir)
    {
        await Task.CompletedTask;
        var finishedCnt = 0;
        using IArchive archive = ArchiveFactory.OpenArchive(_pack!.Path, new ReaderOptions { Password = Password });
        List<IArchiveEntry> entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
        var totalFileCnt = entries.Count;
        ExtractionOptions options = new()
        {
            ExtractFullPath = true,
            Overwrite = true,
        };

        // 解压文件并更新进度
        foreach (IArchiveEntry entry in entries)
        {
            ChangeProgress(finishedCnt, totalFileCnt, $"{entry.Key}");
            entry.WriteToDirectory(tmpDir, options);
            Interlocked.Increment(ref finishedCnt);
        }
    }

    public override bool OnSearch(string key) => Utils.ArePathsEqual(key, TargetPath);

    public override string Title { get; } = "UnpackGameTask_Title".GetLocalized();
}