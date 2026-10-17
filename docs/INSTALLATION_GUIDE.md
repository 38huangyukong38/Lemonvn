# LemonVN V0.1 Preview 安装与运行

项目主页：[38huangyukong38/Lemonvn](https://github.com/38huangyukong38/Lemonvn)。
计划发行页：[GitHub Releases](https://github.com/38huangyukong38/Lemonvn/releases)。
v0.1.0-preview 已公开；本次修复版本为 v0.1.1-preview。请按发行页对应标签下载完整 ZIP 与校验文件，旧版保留。补丁不改变数据库、ZIP V1/V2 或已确认的组件条款。

**仅 Windows x64。已在现有 Windows 11 10.0.26200 x64 开发环境独立解压运行，尚未完成干净 Windows 系统测试。软件未正式代码签名，Windows 可能显示安全提示。**

LemonVN 免费开源，不提供番剧视频播放、视频下载或视频资源。

## 解压与启动

1. 从维护者公布的可信来源取得当前版本的 `LemonVN-v0.1.1-preview-windows-x64.zip` 和同名 `.zip.sha256`。
2. 在 PowerShell 执行 `Get-FileHash -Algorithm SHA256 -LiteralPath '<ZIP路径>'`，与校验文件的 64 位十六进制值比较。校验值用于核对一致性，不能代替可信来源或代码签名。
3. 完整解压到普通用户可访问的新目录，保留子目录。不要直接从压缩软件运行或单独复制 EXE。
4. 运行 `LemonVN.exe`。应用采用 `asInvoker`，不要求管理员权限；不需要 Visual Studio、开发 SDK 或数据库服务器。
5. 首次启动阅读组件条款与原始许可后，明确选择同意才进入；拒绝或关闭会正常退出，不打开收藏数据库。不要关闭安全防护或绕过系统安全提示。

ZIP 内含 .NET 10.0.13 运行时；Windows App Runtime 仍采用框架依赖方式，可能需要另行安装。

## Windows App Runtime

当前使用 Windows App SDK 2.4.0 / WinUI 2.3.6。所需框架家族为 `Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe`，架构 x64，最低版本 **2.4.0.0**。准确参数同时保存在 ZIP 内 `runtime-requirements.json`。

可运行随包的 `Check-Runtime.ps1`，只查询注册信息，不安装、卸载或改策略。若系统不允许脚本，直接在 PowerShell 查询：

```powershell
Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.*' |
    Select-Object Name, Version, Architecture, PackageFamilyName
```

若缺少兼容运行时，请从[微软官方 Windows App SDK 下载页](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)取得相应 x64 Windows App Runtime 安装程序，按官方说明安装或修复后重试。无需开发 SDK；不要使用第三方运行库合集。本包不自动执行安装程序。

版本与注册查询只是预检；DDLM 等依赖及系统状态也必须正常，以实际 Bootstrap 和界面启动为准。当前开发电脑安装的是兼容 Runtime 2.5.1.0；未通过卸载本机 Runtime 模拟干净环境。若初始化失败，程序会报告运行时错误，不会删库重建。

## 数据、备份与升级

非打包模式默认数据目录为 `%LocalAppData%\LemonVN`，含收藏数据库、封面、设置、备份等。**ZIP 免安装不等于数据随程序移动的便携模式**，也不会读取 PotatoVN 数据。普通用户无需配置测试使用的数据目录环境变量。

番剧页可手动添加，或主动搜索 Bangumi、预览并确认添加。已看集数仅从 EpisodeRecord 计算；修改状态、资料、评分或备注不会隐式更改观看记录。

设置中导出完整 ZIP V2 备份；旧 ZIP V1/V2 均支持恢复。备份包含收藏、回收站、个人记录、完整逐集记录和引用封面，不包含主题设置、许可确认、日志或凭证。分享备份会分享其中个人内容。

恢复采用替换语义：先校验、展示数量并确认，再保留原数据安全副本后执行。出错保留原数据；不要手动清空数据库。

升级前先导出备份，正常关闭旧实例，将新版本完整解压到新程序目录。保留原数据，不要混合新旧 DLL 或覆盖运行中的程序。本版未验证 MSIX、安装器、自动更新、所有 Windows 版本或 DPI 配置。

## 许可、隐私与服务

维护者已明确确认适用 SDK、Windows App SDK／Windows ML 发布者责任、代码署名、所供图标公开分发权利及 draft2 组件条款／首启机制。记录不是签名、阅读证明、微软批准或法律认证。正式条款第 1～5 节保持批准的 draft2 内容一致；沿用 `SDK_END_USER_TERMS_DRAFT.md` 历史文件名以兼容加载逻辑。

首次窗口的“阅读全文”提供完整本地许可。明确同意且成功保存后才初始化收藏业务；拒绝正常退出。本地 `Settings/license-consent.json` 只记录格式版本、应用身份、内容哈希和 UTC 同意时间，不上传、不进入收藏数据库或 ZIP 备份。正文内容变化后重新确认。

LemonVN 及适用 PotatoVN 上游代码继续遵守 Apache-2.0，保留 GoldenPotato137 等原作者署名。微软组件原始条款及 C#/WinRT MIT 等许可仅适用于对应组件，不覆盖本项目 Apache 源码。完整原文见随包 `licenses/`，仓库为 `docs/licenses/`。

本地收藏可离线使用。Bangumi 公开资料查询无需 Token；识番仅在用户确认后向所选第三方上传处理后的图片，取消不能撤回已发出的内容。服务有独立条款与额度，无可用性保证。详见隐私与第三方声明。

## 从源码构建

Windows x64 与 .NET 10 SDK 环境下，使用仓库官方 NuGet 源恢复。`scripts/Build-Release.ps1` 执行未签名、非打包 Release 构建；加 `-RunTests` 执行离线测试。无需运行签名、商店上传或发布工作流。

```powershell
dotnet restore GalgameManager/GalgameManager.csproj --configfile NuGet.Config -p:Platform=x64 -p:Configuration=Release -p:WindowsPackageType=None -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -p:GenerateTemporaryStoreCertificate=false
dotnet build GalgameManager/GalgameManager.csproj --no-restore -p:Platform=x64 -p:Configuration=Release -p:WindowsPackageType=None -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -p:GenerateTemporaryStoreCertificate=false -m:1 -nr:false
```
