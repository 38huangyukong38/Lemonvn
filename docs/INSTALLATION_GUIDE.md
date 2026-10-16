# LemonVN V0.1 安装、运行与数据说明

M6-B2-A源码准备说明；项目主页 https://github.com/38huangyukong38/Lemonvn ，未来发行页 /releases 当前没有正式二进制发行。本文件同时随候选放在 EXE 同级；仓库中的发布、隐私及许可记录以 docs/ 下文件为准。

## 1. 支持范围

- 首发目标 Windows x64；目前只实际验证 Windows 11 10.0.26200 x64。
- 项目声明最低 Windows 10.0.17763；不是对 Windows 10 各版本的已验证支持承诺。
- .NET 自包含：候选带 .NET 8.0.32，无需另装 .NET SDK、Visual Studio 或数据库服务器。
- Windows App SDK 为 2.1.3；**Windows App Runtime 并非自包含**。最低框架版本 2.1.3.0，框架家族 Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe，架构 x64。实际值从 NuGet 的 WindowsAppSDK-VersionInfo.json 生成，见候选内 runtime-requirements.json。

## 2. 解压与运行

1. 取得经维护者允许的个人本地候选及对应 .sha256 文件；当前不能公开转发该候选。
2. 在 PowerShell 运行 `Get-FileHash -Algorithm SHA256 -LiteralPath '<ZIP路径>'`，与校验文件中的 64 位十六进制值比较。校验值只能检测文件一致性，不能代替受信任来源或数字签名。
3. 将 ZIP 完整解压到新的普通用户可访问目录，保留子目录。不要直接在压缩软件内运行，不要只拷贝 EXE。
4. 打开 LemonVN.exe；应用 manifest 为 asInvoker，不要求管理员权限。首次可以离线手动添加收藏，网络资料和识番都是可选功能。
5. 不需要改系统安全策略或关闭防护软件。当前未签名；若系统阻止运行，记录提示交给维护者审核，不提供绕过安全提示的步骤。

## 3. Windows App Runtime 检测与安装引导

候选附带 Check-Runtime.ps1，仅查询当前用户的框架注册、版本和架构，不安装、不卸载、不改变策略。可在普通 PowerShell 中运行 `& '<解压目录>\Check-Runtime.ps1'`。若执行策略不允许脚本，不要修改策略，可手动查询：

```powershell
Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.*' |
    Select-Object Name, Version, Architecture, PackageFamilyName
```

找到相同家族、x64、版本不低于 2.1.3.0 只是注册预检；DDLM 等依赖和系统状态也必须完整，**实际 Bootstrap.Initialize 与 GUI 启动才是最终判断**。

若缺少或损坏运行时，当前 EXE 在 Application.Start 之前显示“未能初始化 Windows App SDK 运行时”及错误类型，要求安装/修复 x64 Runtime，不会自动删库重建。Release 不写 DEBUG bootstrap.log。此错误路径经过源码检查，尚未通过卸载本机运行时实测。

请从 [Microsoft 官方 Windows App SDK 下载页](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)寻找与本候选版本兼容的 x64 Windows App Runtime 安装程序；按官方安装说明操作后重新启动 LemonVN。不要下载第三方“运行库合集”，不要只根据安装程序文件名猜测注册版本。本轮未捆绑或自动执行安装程序，也未卸载现有系统组件。

框架依赖方案体积较小、由系统共享维护，但增加安装前置；未来 WinAppSDK 自包含可降低预装要求、增加目录大小及维护责任。当前入口手动调用 Bootstrap 且关闭部分自动初始化，不能只切一个 SelfContained 标记就声称已完成自包含。是否采用该方案由后续阶段决定，当前不改启动流程。

## 4. 数据与备份

非打包版默认根为 `%LocalAppData%\LemonVN`；包含 lemonvn.db、Covers、Settings、Backups、Cache、Logs 等。程序目录不作为用户数据目录，ZIP 免安装不等于真正便携。测试可用独立 LEMONVN_LOCALDATA_PATH 根，普通用户不必配置该变量，不读取 POTATOVN_*。

首次使用：番剧页手动添加或主动搜索 Bangumi→预览→确认；详情修改状态/评分/标签/备注，+1 标记下一未看正片。总已看数来自逐集记录，状态不会因进度自动改变。

设置→“导出番剧收藏备份”会写出完整 ZIP V2；旧 ZIP V1/V2 均可读取。备份含收藏、回收站、个人字段、完整逐集记录和引用封面，不含主题设置、日志、Token 或无关缓存。分享备份会分享其中的个人内容。

设置→“从备份恢复”：选择→校验→核对数量和替换提示→由用户确认。执行前保存原数据安全副本；恢复替换收藏与回收站，不做合并，旧封面不自动删除。出错时保留原数据；不要先手动清空数据库再导入。

## 5. 升级与其他部署模式

升级前导出备份并妥善保留，正常关闭旧实例，把新版本完整解压到新程序目录。程序和数据分离；本阶段测试用同一版本的两份独立解压程序验证“换目录再启动”不重置收藏/设置，不冒充真实跨版本迁移。不要混合新旧 DLL、直接覆盖运行中的 EXE、删除数据根或降级编辑含新版公司字段的数据。

未来 MSIX 使用独立包的 LocalFolder/LemonVN；它与非打包默认目录不同，不会自动读取另一模式数据。可以通过现有 ZIP 导出/恢复迁移收藏和封面，再手动选择主题。MSIX 安装、签名、升级和该模式实机迁移本轮未验证。

## 6. 本地构建

Windows 与 .NET 8 SDK 下，先按仓库 NuGet.Config 恢复依赖；不要运行继承的签名/商店发布工作流。工作区已有 scripts/Use-LocalDotNet.ps1 时可先 dot-source 使用局部 SDK。

```powershell
dotnet restore GalgameManager/GalgameManager.csproj --configfile NuGet.Config -p:Platform=x64 -p:Configuration=Release -p:WindowsPackageType=None -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -p:GenerateTemporaryStoreCertificate=false
dotnet build GalgameManager/GalgameManager.csproj --no-restore -p:Platform=x64 -p:Configuration=Release -p:WindowsPackageType=None -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -p:GenerateTemporaryStoreCertificate=false -m:1 -nr:false
```

当前客户端项目已按自有素材白名单整理输出；这不等于许可证及部署准入全部完成。内部候选准备脚本有独立文件策略、SHA/内容清单及许可风险标识。参见 M6_B1_DEPLOYMENT_REPORT.md 与 API_AND_LICENSE_RISKS.md。
