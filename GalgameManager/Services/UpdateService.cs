using Windows.Storage;
using Windows.ApplicationModel;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GalgameManager.Models;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class UpdateService : IUpdateService
{
    // 更新相关常量
    private const string VERSION_CHECK_URL = "https://potatovn.net/version.json";
    private const string STORE_UPDATE_URL = "https://apps.microsoft.com/detail/9p9cbkd5hr3w";
    private const string SIDELOAD_STABLE_DOWNLOAD_URL = "https://download.potatovn.net/release";
    private const string SIDELOAD_BETA_DOWNLOAD_URL = "https://download.potatovn.net/flight-released";

    private readonly bool _firstUpdate;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInfoService _infoService;

    // 本次启动是否已经更新失败过的标记
    private bool _updateFailedThisSession;
    // 本次启动是否已经取消过更新的标记
    private bool _updateCancelledThisSession;
    // 本次启动是否已经执行过更新的标记
    private bool _updatePerformedThisSession;
    private Version? _targetVersion; //本次启动检查出来的最新版本

    public event Action<bool>? SettingBadgeEvent;

    public UpdateService(ILocalSettingsService localSettingsService, IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _infoService = infoService;
        var last = localSettingsService.ReadSettingAsync<string>(KeyValues.DisplayedUpdateVersion).Result ?? "";
        _firstUpdate = last != RuntimeHelper.GetVersion();
    }

    private static async Task<StorageFolder> GetTempRootAsync()
    {
        return await StorageFolder.GetFolderFromPathAsync(AppStoragePaths.TempPath);
    }

    private class VersionJson
    {
        [JsonProperty("released")]
        public string Release { get; set; } = string.Empty;
        [JsonProperty("released-msstore")]
        public string MsStore { get; set; } = string.Empty;
        [JsonProperty("flight-released")]
        public string Beta { get; set; } = string.Empty;
    }

    public async Task<Version?> GetLatestVersionAsync()
    {
        if (_targetVersion is not null) return _targetVersion;
        try
        {
            HttpClient client = Utils.GetDefaultHttpClient();
            HttpResponseMessage response = await client.GetAsync(VERSION_CHECK_URL);
            VersionJson? version = JsonConvert.DeserializeObject<VersionJson>(await response.Content.ReadAsStringAsync());
            if (version is null) throw new PvnException("Version Json is null");
            Version stableVersion = Version.Parse(version.Release);
            Version betaVersion = Version.Parse(version.Beta);
            Version storeVersion = Version.Parse(version.MsStore);
            Version currentVersion = Version.Parse(RuntimeHelper.GetVersion());

            bool needsUpdate; // 判断当前版本是否需要更新
            var isStoreVersion = App.IsStoreVersion();
            var isBetaVersion = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.IsBetaChannel);

            _targetVersion = isBetaVersion ? betaVersion : stableVersion;
            _targetVersion = isStoreVersion ? storeVersion : _targetVersion;
            if (isStoreVersion) // 商店版：只检查稳定版更新
            {
                needsUpdate = currentVersion < storeVersion;
                var updateType = "stable";
                await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, updateType);
                await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, GetDownloadUrl(updateType, isStoreVersion, storeVersion));
            }
            else
            {
                if (isBetaVersion) // 侧载版：根据当前版本类型决定更新策略
                {
                    // 测试版：检查是否有新的测试版，如果没有则检查稳定版
                    if (currentVersion < betaVersion)
                    {
                        needsUpdate = true;
                        var updateType = "beta";
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, updateType);
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, GetDownloadUrl(updateType, isStoreVersion, betaVersion));
                    }
                    else if (currentVersion < stableVersion)
                    {
                        needsUpdate = true;
                        var updateType = "stable";
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, updateType);
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, GetDownloadUrl(updateType, isStoreVersion, stableVersion));
                    }
                    else
                        needsUpdate = false;
                }
                else
                {
                    // 侧载正式版：只检查稳定版更新
                    needsUpdate = currentVersion < stableVersion;
                    var updateType = "stable";
                    await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, updateType);
                    await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, GetDownloadUrl(updateType, isStoreVersion, stableVersion));
                }
            }

            await _localSettingsService.SaveSettingAsync(KeyValues.LastUpdateCheckResult, needsUpdate);
            return _targetVersion;
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
            return null;
        }
    }

    public async Task<bool> IsUpdateAvailableAsync()
    {
        Version? v = await GetLatestVersionAsync();
        if (v is null) return false;
        Version currentVersion = Version.Parse(RuntimeHelper.GetVersion());
        return v > currentVersion;
    }

    /// <summary>
    /// 根据更新类型获取相应的下载URL
    /// </summary>
    /// <param name="updateType">更新类型：stable 或 beta</param>
    /// <param name="isStoreVersion">是否为商店版</param>
    /// <param name="version">目标下载版本</param>
    /// <returns>下载URL</returns>
    private static string GetDownloadUrl(string updateType, bool isStoreVersion, Version version)
    {
        if (isStoreVersion) return STORE_UPDATE_URL;
        Architecture processArch = RuntimeInformation.ProcessArchitecture;
        var archSuffix = processArch switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "ARM64",
            _ => "x64",
        };
        var url = updateType == "beta" ? SIDELOAD_BETA_DOWNLOAD_URL : SIDELOAD_STABLE_DOWNLOAD_URL;
        return $"{url}/{version}_{archSuffix}.msix";
    }

    /// <summary>
    /// 检查是否有新版本且未被忽略
    /// </summary>
    /// <returns>有可用更新返回版本号，否则返回null</returns>
    public async Task<string?> GetAvailableUpdateVersionAsync()
    {
        try
        {
            // 如果本次启动已经更新失败过、用户已取消过或已经执行过更新，不再返回可用更新
            if (_updateFailedThisSession || _updateCancelledThisSession || _updatePerformedThisSession) return null;

            Version? newestVersion = await GetLatestVersionAsync();
            if (newestVersion is null) return null;
            if (newestVersion <= Version.Parse(RuntimeHelper.GetVersion()))
                return null; // 没有新版本
            List<string> ignoredVersions = await _localSettingsService.ReadSettingAsync<List<string>>(KeyValues.IgnoredUpdateVersions) ?? [];
            if (ignoredVersions.Contains(newestVersion.ToString()))
                return null; // 该版本已被忽略
            return newestVersion.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 显示更新确认对话框
    /// </summary>
    /// <returns>用户选择：0=取消，1=立即更新，2=忽略这个版本</returns>
    public async Task<int> ShowUpdateConfirmationAsync()
    {
        try
        {
            var isStoreVersion = App.IsStoreVersion();

            var title = "UpdateService_UpdateAvailable_Title".GetLocalized();
            var content = isStoreVersion
                ? "UpdateService_StoreUpdate_Content".GetLocalized()
                : "UpdateService_SideloadUpdate_Content".GetLocalized();

            ContentDialog updateDialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
                RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
                Title = title,
                Content = content,
                PrimaryButtonText = "UpdateService_Update_Confirm".GetLocalized(),
                SecondaryButtonText = "UpdateService_Update_Ignore".GetLocalized(),
                CloseButtonText = "UpdateService_Update_Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await updateDialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => 1,    // 立即更新
                ContentDialogResult.Secondary => 2,  // 忽略这个版本
                _ => 0                               // 取消/稍后提醒
            };
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Dialog_Error".GetLocalized(), ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// 忽略指定版本的更新
    /// </summary>
    /// <param name="version">要忽略的版本号</param>
    public async Task IgnoreVersionAsync(string version)
    {
        try
        {
            List<string> ignoredVersions = await _localSettingsService.ReadSettingAsync<List<string>>(KeyValues.IgnoredUpdateVersions) ?? new List<string>();
            if (!ignoredVersions.Contains(version))
            {
                ignoredVersions.Add(version);
                await _localSettingsService.SaveSettingAsync(KeyValues.IgnoredUpdateVersions, ignoredVersions);
            }
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_IgnoreVersion_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 执行更新操作
    /// </summary>
    public async Task PerformUpdateAsync()
    {
        try
        {
            _updatePerformedThisSession = true; // 设置本次启动已执行更新标记，避免再次弹窗
            var isStoreVersion = App.IsStoreVersion();
            if (isStoreVersion)
                await OpenStoreForUpdateAsync();
            else
            {
                // 侧载版：下载更新包并准备在应用退出后安装
                // await DownloadAndPrepareUpdateAsync(); //暂时取消，改用浏览器下载用户人工安装
                await Launcher.LaunchUriAsync(new Uri(
                    await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateUrl) ??
                    throw new InvalidOperationException("No update URL found.")));
            }
        }
        catch (Exception ex)
        {
            // 设置本次启动更新失败标记
            _updateFailedThisSession = true;
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Update_Error".GetLocalized(), ex.Message);

            // 对于侧载版，如果自动更新失败，提供手动安装选项
            if (!App.IsStoreVersion())
            {
                await ShowManualInstallOptionsAsync();
            }
        }
    }

    /// <summary>
    /// 打开商店进行更新
    /// </summary>
    private async Task OpenStoreForUpdateAsync()
    {
        try
        {
            // 使用ms-windows-store协议直接打开商店应用，避免通过浏览器
            var storeProtocolUrl = "ms-windows-store://pdp/?productid=9p9cbkd5hr3w";
            await Launcher.LaunchUriAsync(new Uri(storeProtocolUrl));
            _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Store_Opened".GetLocalized());
        }
        catch (Exception ex)
        {
            // 设置本次启动更新失败标记
            _updateFailedThisSession = true;
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Store_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 下载更新包并准备在应用退出后安装
    /// </summary>
    private async Task DownloadAndPrepareUpdateAsync()
    {
        try
        {
            _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Download_Started".GetLocalized());

            var downloadUrl = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateUrl);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                // 如果没有保存的下载URL，根据当前环境生成默认URL
                var updateType = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateType) ?? "stable";
                Version? version = await GetLatestVersionAsync();
                if (version is null) throw new PvnException("UpdateService_FailedToGetVersion".GetLocalized());
                downloadUrl = GetDownloadUrl(updateType, App.IsStoreVersion(), version);
            }

            // 创建临时下载目录
            StorageFolder tempRoot = await GetTempRootAsync();
            StorageFolder? tempFolder = await tempRoot.CreateFolderAsync(
                "UpdateDownload", CreationCollisionOption.ReplaceExisting);

            // 下载更新包
            HttpClient client = Utils.GetDefaultHttpClient();
            HttpResponseMessage response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            StorageFile? msixFile = await tempFolder.CreateFileAsync("PotatoVN_Update.msix", CreationCollisionOption.ReplaceExisting);
            await using (Stream? fileStream = await msixFile.OpenStreamForWriteAsync())
            {
                await response.Content.CopyToAsync(fileStream);
            }

            _infoService.Info(InfoBarSeverity.Success, "UpdateService_Download_Completed".GetLocalized());

            // 创建安装 PowerShell 脚本
            await CreateInstallPowerShellScriptAsync(msixFile.Path);

            // 询问是否立即重启安装
            await ShowInstallConfirmationAsync();
        }
        catch (Exception ex)
        {
            // 设置本次启动更新失败标记
            _updateFailedThisSession = true;
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Download_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 创建安装 PowerShell 脚本，用于在应用退出后安装更新
    /// </summary>
    /// <param name="msixPath">MSIX文件路径</param>
    private async Task CreateInstallPowerShellScriptAsync(string msixPath)
    {
        try
        {
            if (!RuntimeHelper.IsMSIX)
                throw new Exception("Unpackaged mode does not support MSIX install script.");

            StorageFolder tempFolder = await GetTempRootAsync();
            StorageFile? psFile = await tempFolder.CreateFileAsync("InstallUpdate.ps1", CreationCollisionOption.ReplaceExisting);

            // 获取当前应用的包信息用于重启
            var packageName = Package.Current.Id.FamilyName;
            var appId = Package.Current.Id.Name;

            // 获取本地化文本
            var msgPreparing = "UpdateService_Script_Preparing".GetLocalized();
            var msgDoNotClose = "UpdateService_Script_DoNotClose".GetLocalized();
            var msgWaitingClose = "UpdateService_Script_WaitingClose".GetLocalized();
            var msgInstalling = "UpdateService_Script_Installing".GetLocalized();
            var msgFilePath = "UpdateService_Script_FilePath".GetLocalized();
            var msgInstallSuccess = "UpdateService_Script_InstallSuccess".GetLocalized();
            var msgWaitingComplete = "UpdateService_Script_WaitingComplete".GetLocalized();
            var msgRestarting = "UpdateService_Script_Restarting".GetLocalized();
            var msgRestarted = "UpdateService_Script_Restarted".GetLocalized();
            var msgInstallFailed = "UpdateService_Script_InstallFailed".GetLocalized();
            var msgManualInstall = "UpdateService_Script_ManualInstall".GetLocalized();
            var msgPressKey = "UpdateService_Script_PressKey".GetLocalized();
            var msgCleaningFiles = "UpdateService_Script_CleaningFiles".GetLocalized();
            var msgFilesCleaned = "UpdateService_Script_FilesCleaned".GetLocalized();
            var msgCleanError = "UpdateService_Script_CleanError".GetLocalized();
            var msgInstallComplete = "UpdateService_Script_InstallComplete".GetLocalized();
            var msgAutoClose = "UpdateService_Script_AutoClose".GetLocalized();

            // 创建简单的日志文件路径（仅用于调试）
            var logPath = Path.Combine(AppStoragePaths.TempPath, "UpdateLog.txt");

            // PowerShell 脚本内容：等待应用关闭，然后安装更新包，安装成功后重启应用，最后清理临时文件
            var psContent = $@"# PotatoVN Update Install Script
# 设置控制台编码为UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = ""Stop""
$LogPath = ""{logPath}""

# 创建日志函数
function Write-Log {{
    param([string]$Message)
    $Timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
    $LogMessage = ""[$Timestamp] $Message""
    Write-Host $LogMessage
    Add-Content -Path $LogPath -Value $LogMessage -Encoding UTF8 -ErrorAction SilentlyContinue
}}

Write-Log ""{msgPreparing}""
Write-Host ""{msgDoNotClose}"" -ForegroundColor Green

# 等待应用完全关闭，检查进程是否还在运行
Write-Log ""{msgWaitingClose}""
$processName = ""GalgameManager""
$waitCount = 0
$maxWait = 15

do {{
    $process = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($process) {{
        Write-Log ""等待进程 $processName 关闭... ($waitCount/$maxWait)""
        Start-Sleep -Seconds 1
        $waitCount++
    }}
}} while ($process -and $waitCount -lt $maxWait)

if ($waitCount -ge $maxWait) {{
    Write-Log ""强制终止进程 $processName""
    Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}}

Write-Log ""应用已完全关闭，开始安装更新""

# 验证MSIX文件存在
if (-not (Test-Path ""{msixPath}"")) {{
    Write-Log ""错误：MSIX文件不存在 - {msixPath}""
    Write-Host ""{msgPressKey}"" -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey(""NoEcho,IncludeKeyDown"")
    exit 1
}}

# 安装更新包
Write-Log ""{msgInstalling}""
Write-Host ""{msgFilePath}: {msixPath}"" -ForegroundColor Gray

try {{
    # 首先尝试移除旧版本（如果存在）
    Write-Log ""正在检查已安装的包...""
    $existingPackage = Get-AppxPackage -Name ""{appId}"" -ErrorAction SilentlyContinue
    if ($existingPackage) {{
        Write-Log ""找到现有包: $($existingPackage.PackageFullName)""
    }}

    # 使用 Add-AppxPackage 安装更新，强制安装标志
    Write-Log ""正在安装更新包...""
    Add-AppxPackage -Path ""{msixPath}"" -ForceApplicationShutdown -ErrorAction Stop

    Write-Log ""{msgInstallSuccess}""
    Write-Host ""{msgInstallSuccess}"" -ForegroundColor Green

    # 等待安装完成
    Write-Log ""{msgWaitingComplete}""
    Start-Sleep -Seconds 3

         # 验证安装是否成功
     $newPackage = Get-AppxPackage -Name ""{appId}"" -ErrorAction SilentlyContinue
     if ($newPackage) {{
         Write-Log ""安装验证成功: $($newPackage.PackageFullName)""

         # 等待安装完全完成
         Start-Sleep -Seconds 2

         # 重新启动应用
         Write-Log ""{msgRestarting}""

         try {{
             # 重新启动应用
             Write-Log ""正在启动应用...""

             # 等待一下确保安装完全完成
             Start-Sleep -Seconds 3

             # 使用多种方法尝试启动应用
             $launched = $false

             # 方法1：使用包家族名和标准App ID启动
             try {{
                 $appUserModelId = ""{packageName}!App""
                 Write-Log ""尝试方法1: $appUserModelId""
                 Start-Process ""explorer.exe"" -ArgumentList ""shell:appsFolder\$appUserModelId"" -ErrorAction Stop
                 Start-Sleep -Seconds 3

                 # 检查是否启动成功
                 $process1 = Get-Process -Name ""GalgameManager"" -ErrorAction SilentlyContinue
                 if ($process1) {{
                     Write-Log ""方法1启动成功，进程ID: $($process1.Id)""
                     $launched = $true
                 }}
             }}
             catch {{
                 Write-Log ""方法1失败: $($_.Exception.Message)""
             }}

             # 方法2：如果方法1失败，尝试从开始菜单查找并启动
             if (-not $launched) {{
                 try {{
                     Write-Log ""尝试方法2: 从开始菜单启动""
                     $startApps = Get-StartApps | Where-Object {{$_.Name -like ""*PotatoVN*"" -or $_.AppID -like ""*{packageName}*""}}
                     if ($startApps) {{
                         $app = $startApps[0]
                         Write-Log ""找到开始菜单项: $($app.Name), AppID: $($app.AppID)""
                         Start-Process ""explorer.exe"" -ArgumentList ""shell:appsFolder\$($app.AppID)"" -ErrorAction Stop
                         Start-Sleep -Seconds 3

                         $process2 = Get-Process -Name ""GalgameManager"" -ErrorAction SilentlyContinue
                         if ($process2) {{
                             Write-Log ""方法2启动成功，进程ID: $($process2.Id)""
                             $launched = $true
                         }}
                     }}
                     else {{
                         Write-Log ""在开始菜单中未找到PotatoVN应用""
                     }}
                 }}
                 catch {{
                     Write-Log ""方法2失败: $($_.Exception.Message)""
                 }}
             }}

             if ($launched) {{
                 Write-Host ""{msgRestarted}"" -ForegroundColor Green
             }}
             else {{
                 Write-Log ""所有启动方法都失败，请手动启动应用""
                 Write-Host ""应用已安装完成，请从开始菜单手动启动 PotatoVN"" -ForegroundColor Yellow
             }}
         }}
         catch {{
             Write-Log ""启动应用时出现异常: $($_.Exception.Message)""
             Write-Host ""应用已安装完成，请从开始菜单手动启动 PotatoVN"" -ForegroundColor Yellow
         }}
     }}
     else {{
         Write-Log ""错误：安装验证失败，未找到新安装的包""
         throw ""Package verification failed""
     }}
}}
catch {{
    Write-Log ""{msgInstallFailed}: $($_.Exception.Message)""
    Write-Host ""{msgInstallFailed}: $($_.Exception.Message)"" -ForegroundColor Red
    Write-Host ""{msgManualInstall}: {msixPath}"" -ForegroundColor Yellow
    Write-Host ""更新安装失败，您可以手动双击MSIX文件进行安装"" -ForegroundColor Yellow
    Write-Host ""窗口将在10秒后自动关闭，或按任意键立即关闭..."" -ForegroundColor Gray

    # 等待用户输入或10秒超时
    $host.UI.RawUI.FlushInputBuffer()
    $timeout = 10
    $timer = [System.Diagnostics.Stopwatch]::StartNew()

    while ($timer.Elapsed.TotalSeconds -lt $timeout) {{
        if ($host.UI.RawUI.KeyAvailable) {{
            $null = $host.UI.RawUI.ReadKey(""NoEcho,IncludeKeyDown"")
            break
        }}
        Start-Sleep -Milliseconds 100
    }}

    $timer.Stop()
    exit 1
}}

# 清理临时文件
Write-Log ""{msgCleaningFiles}""
try {{
    Remove-Item ""{msixPath}"" -Force -ErrorAction SilentlyContinue
    Write-Log ""{msgFilesCleaned}""
    Write-Host ""{msgFilesCleaned}"" -ForegroundColor Green
}}
catch {{
    Write-Log ""{msgCleanError}: $($_.Exception.Message)""
    Write-Host ""{msgCleanError}: $($_.Exception.Message)"" -ForegroundColor Yellow
}}

Write-Log ""{msgInstallComplete}""
Write-Host ""{msgInstallComplete}"" -ForegroundColor Green
Write-Host ""{msgAutoClose}"" -ForegroundColor Gray

# 等待用户看到结果
Start-Sleep -Seconds 3

# 清理脚本文件自身
try {{
    Remove-Item $PSCommandPath -Force -ErrorAction SilentlyContinue
}}
catch {{
    # 忽略删除脚本文件的错误
}}

Write-Host ""PowerShell 窗口将自动关闭..."" -ForegroundColor Gray
Start-Sleep -Seconds 2
";

            // 使用UTF-8 BOM编码保存PowerShell脚本，确保中文字符正确显示
            UTF8Encoding utf8WithBom = new System.Text.UTF8Encoding(true);
            await File.WriteAllTextAsync(psFile.Path, psContent, utf8WithBom);

            // 保存 PowerShell 脚本文件路径
            await _localSettingsService.SaveSettingAsync(KeyValues.UpdateBatchPath, psFile.Path);
        }
        catch (Exception ex)
        {
            throw new Exception($"{"UpdateService_Create_Script_Failed".GetLocalized()}: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示安装确认对话框
    /// </summary>
    private async Task ShowInstallConfirmationAsync()
    {
        try
        {
            ContentDialog installDialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
                RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
                Title = "UpdateService_Install_Title".GetLocalized(),
                Content = "UpdateService_Install_Content".GetLocalized(),
                PrimaryButtonText = "UpdateService_Install_Confirm".GetLocalized(),
                CloseButtonText = "UpdateService_Install_Later".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await installDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // 执行安装并退出应用
                await ExecuteInstallAndExitAsync();
            }
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Install_Dialog_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 检查 PowerShell 是否可用
    /// </summary>
    /// <returns>PowerShell 是否可用</returns>
    private static bool IsPowerShellAvailable()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-Command \"Write-Host 'Test'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using Process? process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit(5000); // 等待最多5秒
                return process.ExitCode == 0;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 执行安装 PowerShell 脚本并退出应用
    /// </summary>
    private async Task ExecuteInstallAndExitAsync()
    {
        try
        {
            var scriptPath = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateBatchPath) ?? "";
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                throw new Exception("UpdateService_Install_Script_NotFound".GetLocalized());
            }

            // 检查 PowerShell 是否可用
            if (!IsPowerShellAvailable())
            {
                throw new Exception("UpdateService_PowerShell_NotAvailable".GetLocalized());
            }



            // 创建 PowerShell 进程启动信息
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -InputFormat None -OutputFormat Text -WindowStyle Normal -File \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                Verb = "runas" // 以管理员权限运行，确保能够安装 MSIX 包
            };

            try
            {
                // 启动 PowerShell 进程（在前台显示命令行窗口）
                Process? process = Process.Start(startInfo);

                if (process == null)
                {
                    throw new Exception("UpdateService_PowerShell_Start_Failed".GetLocalized());
                }

                _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Install_Started".GetLocalized());

                // 等待更长时间确保 PowerShell 脚本启动并获得焦点
                await Task.Delay(2000);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // 用户取消了 UAC 提示
                _infoService.Info(InfoBarSeverity.Warning, "UpdateService_Install_UAC_Cancelled".GetLocalized());
                return; // 不退出应用，让用户可以再次尝试
            }

            // 退出应用
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Install_Execute_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 设置用户取消更新标记
    /// </summary>
    public void SetUpdateCancelledThisSession()
    {
        _updateCancelledThisSession = true;
    }

    /// <summary>
    /// 提供手动安装选项（直接打开MSIX文件）
    /// </summary>
    private async Task ShowManualInstallOptionsAsync()
    {
        try
        {
            // 查找MSIX文件
            StorageFolder tempFolder = await GetTempRootAsync();
            StorageFolder? updateFolder = await tempFolder.GetFolderAsync("UpdateDownload");
            IReadOnlyList<StorageFile>? files = await updateFolder.GetFilesAsync();
            StorageFile? msixFile = files.FirstOrDefault(f => f.FileType.ToLower() == ".msix");

            if (msixFile == null)
            {
                _infoService.Info(InfoBarSeverity.Error, "UpdateService_Manual_Install_File_NotFound".GetLocalized());
                return;
            }

            ContentDialog manualInstallDialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
                RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
                Title = "UpdateService_Manual_Install_Title".GetLocalized(),
                Content = "UpdateService_Manual_Install_MSIX_Content".GetLocalized(),
                PrimaryButtonText = "UpdateService_Manual_Install_Open_MSIX".GetLocalized(),
                SecondaryButtonText = "UpdateService_Manual_Install_OpenFolder".GetLocalized(),
                CloseButtonText = "UpdateService_Update_Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await manualInstallDialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    // 直接打开MSIX文件进行安装
                    await OpenMsixFileAsync(msixFile);
                    break;
                case ContentDialogResult.Secondary:
                    // 打开包含更新文件的文件夹
                    await OpenUpdateFolderAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Manual_Install_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 直接打开MSIX文件进行安装
    /// </summary>
    private async Task OpenMsixFileAsync(StorageFile msixFile)
    {
        try
        {
            await Launcher.LaunchFileAsync(msixFile);
            _infoService.Info(InfoBarSeverity.Informational, "UpdateService_MSIX_File_Opened".GetLocalized());
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_MSIX_File_Open_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 打开包含更新文件的文件夹
    /// </summary>
    private async Task OpenUpdateFolderAsync()
    {
        try
        {
            StorageFolder tempFolder = await GetTempRootAsync();
            StorageFolder? updateFolder = await tempFolder.GetFolderAsync("UpdateDownload");

            await Launcher.LaunchFolderAsync(updateFolder);
            _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Update_Folder_Opened".GetLocalized());
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Update_Folder_Error".GetLocalized(), ex.Message);
        }
    }

    public async Task UpdateSettingsBadgeAsync()
    {
        // 每次都检查是否有可用更新（未被忽略的且本次启动未失败或取消的）
        var availableVersion = await GetAvailableUpdateVersionAsync();
        SettingBadgeEvent?.Invoke(availableVersion != null);
    }

    public bool ShouldDisplayUpdateContent() => _firstUpdate;


}
