using System.IO.Compression;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;

namespace GalgameManager.Views;

public sealed partial class ImportWindow
{
    private readonly ILocalSettingsService _localSettingsService;
    private DirectoryInfo _tmp = null!;
    private FileInfo _zip;

    public ImportWindow(FileInfo zip, ILocalSettingsService localSettingsService)
    {
        InitializeComponent();
        Title = "AppDisplayName".GetLocalized();
        _zip = zip;
        _localSettingsService = localSettingsService;
    }

    public async Task Import()
    {
        try
        {
            FileInfo targetZip = new(Path.Combine(_localSettingsService.TemporaryFolder.FullName, _zip.Name));
            targetZip.Delete();
            _zip.MoveTo(targetZip.FullName);
            StatusTextBlock.Text = "ImportWindow_BackingUp".GetLocalized();
            await Backup();
            StatusTextBlock.Text = "ImportWindow_Extracting".GetLocalized(_zip.Name);
            await Extract();
            StatusTextBlock.Text = "ImportWindow_Importing".GetLocalized();
        }
        catch (Exception e)
        {
            await Restore(e);
        }
    }

    public async Task Restore(Exception e)
    {
        Width *= 2;
        Height *= 2;
        TitleTextBlock.Text = "ImportWindow_Fail".GetLocalized();
        StatusTextBlock.Text = "ImportWindow_Restoring".GetLocalized($"{e.Message}\n{e.StackTrace}");
        if (_localSettingsService.LocalFolder.Exists) _localSettingsService.LocalFolder.Delete(true);
        _tmp.MoveTo(_localSettingsService.LocalFolder.FullName);
        await Task.Delay(2 * 1000); // 停留一会，让用户看到错误信息
    }

    private async Task Backup()
    {
        await Task.Run(() =>
        {
            _tmp = _localSettingsService.TemporaryFolder.CreateSubdirectory("ImportBackup");
            _tmp.Delete(true);
            Move(_localSettingsService.LocalFolder.FullName, _tmp.FullName);
        });
    }

    private Task Extract()
    {
        return Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(_zip.FullName, _localSettingsService.LocalFolder.FullName);
        });
    }


    /// 在系统指定软件数据位置后，所有的软件数据会被加密，无法直接移动一整个文件夹，只能手动移动文件
    private static void Move(string src, string dest)
    {
        if(!Directory.Exists(dest))
            Directory.CreateDirectory(dest);
        foreach(var file in Directory.GetFiles(src))
            File.Move(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach(var subDir in Directory.GetDirectories(src)) 
            Directory.Move(subDir, Path.Combine(dest, Path.GetFileName(subDir)));
    }
}