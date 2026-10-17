# LemonVN v0.1.1-preview

Windows x64 Preview / GitHub Pre-release，基于 WinUI 3、.NET 10 LTS 与 Windows App SDK 2.4.0。

## 本次修复

- Bangumi 直接添加和已有收藏关联共用的确认按钮移到“作品资料预览”右上角，放在资料滚动区之外；滚动简介和差异列表时仍可操作。保留加载、取消、防重复点击和错误提示。
- 修复“先手动保存名称 111 → 从已有详情关联 Bangumi”只保存外部 ID、空白资料未填充的问题。新建时仅保护实际填写的字段；简介、总集数、日期、原名、制作公司和本地封面可正常导入，手填标题保持原值。
- 对 v0.1.0 的六字段默认保护组合，在预览中明确提示将补全的空白项，用户确认后才保存。它也适用于此前已关联 ID 但资料未填充的旧记录。非空手填字段和独立的主动清空保护不自动解除；旧数据无法还原每个空值的历史意图，可在确认前取消。
- 更新的是同一条收藏及本地封面引用，保留本地 GUID、状态、评分、标签、备注、个人日期和 EpisodeRecord；保留 Revision 冲突及重复 Bangumi ID 检查，不按名称合并。
- 本地封面通过有大小、解码及并发限制的图片流显示，修复非打包环境下图片已经保存却仍显示占位图的问题；继续使用本地缓存与封面引用，不从 XAML 额外联网。

## 下载与运行

下载 `LemonVN-v0.1.1-preview-windows-x64.zip` 与同名 `.zip.sha256`，完整解压后运行 `LemonVN.exe`。用 PowerShell `Get-FileHash -Algorithm SHA256 -LiteralPath '<ZIP路径>'` 核对哈希。不要混用新旧 DLL 或覆盖运行中的程序。

当前仅支持 **Windows x64**，内含 .NET 10.0.13 运行时；可能需要另装兼容的 **Windows App Runtime x64**。所需家族 `Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe`，最低版本 2.4.0.0。请从[微软官方页面](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)获取兼容运行时，普通用户不需要 Visual Studio 或开发 SDK。

默认数据仍为 `%LocalAppData%\LemonVN`，ZIP 免安装不等于数据便携模式。升级前导出备份、正常关闭旧程序，再解压新版本。数据库格式、Revision、EpisodeRecord 和 ZIP V1/V2 备份恢复合同不变；旧版 [v0.1.0-preview](https://github.com/38huangyukong38/Lemonvn/releases/tag/v0.1.0-preview) 保留。

## 许可、隐私与已知限制

软件免费开源，**不提供番剧视频播放、下载或视频资源**。保留 GoldenPotato137 / PotatoVN 署名及 Apache-2.0。微软组件、C#/WinRT MIT 等完整原文和经维护者确认的 draft2 组件条款保留；本次不更改条款或本机明确同意机制，不宣称微软批准或法律认证。

Bangumi 仍是用户主动进行的公开动画资料查询，无需 Token；网络失败不影响离线收藏。识番与第三方 API 边界不变，选择图片不会自动上传，服务有独立条款、额度和可用性限制。

**尚未完成干净 Windows 系统测试；软件未正式代码签名，Windows 可能显示安全提示。** 仅在现有 Windows 11 x64 开发环境验证。未承诺所有 Windows 版本、DPI 或 MSIX 部署兼容；不要关闭安全防护或绕过系统提示。本次没有 MSIX、安装器或自动更新。
