# LemonVN v0.2.0 Preview

本文件沿用已有打包文件名，当前版本说明见[完整 v0.2.0 更新说明](https://github.com/38huangyukong38/Lemonvn/releases/tag/v0.2.0-preview)。


Windows x64 / GitHub Pre-release，WinUI 3、.NET 10 LTS、Windows App SDK 2.4.0。

## 本次更新

- 首页最近追番、状态分区和制作公司列表增加左右箭头，按可见卡片翻页；保留原生列表、封面尺寸及88 DIP导航栏。
- 番剧库过滤面板可直接选择状态与标签，点击「应用筛选」或按 Enter 一次提交。修复键盘焦点及选择/刷新冲突，支持与搜索、排序组合；重新打开恢复已应用条件，清除时不会重新套用旧选择。
- 想看/在看/已看完由 EpisodeRecord 推导：初次未看为想看、看过至少一集为在看、已知总数且1～N全部看完才完成。搁置和抛弃为手动覆盖；支持恢复自动。未知总集数不自动完成，越界集数不能补齐缺集，不自动编造观看日期。旧数据只按此规则显示，不批量迁移或重写记录。
- Bangumi 搜索添加后进入详情，可用左上返回键回到原关键词、结果及分页，继续添加另一季；保留重复ID、字段保护及 Revision 检查。
- 增加 `Uninstall.exe`，修复其原先窗口不可见的问题。默认只移除该应用副本、保留收藏；删除本地数据须勾选并二次确认。保留未知文件、系统运行时与其他位置副本，拒绝危险路径/链接和运行中文件；系统脚本策略或权限不允许时安全拒绝，不提升权限或绕过策略。

## 下载、升级与运行

下载 `LemonVN-v0.2.0-preview-windows-x64.zip` 和同名 `.zip.sha256`。完整解压到新目录，正常关闭旧程序后运行 `LemonVN.exe`，不要混用新旧 DLL。升级前先在设置中导出收藏备份。

当前仅支持 **Windows x64**；ZIP包含 .NET 10.0.13，可能需要安装兼容的 **Windows App Runtime x64**（家族 `Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe`，最低2.4.0.0）。从[微软官方页面](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)取得兼容运行时；不需要Visual Studio或开发SDK。随包 `Check-Runtime.ps1` 仅作只读检测。

默认数据为 `%LocalAppData%\LemonVN`，ZIP免安装不等于数据便携。LiteDB、Revision、EpisodeRecord与ZIP V1/V2备份格式保持兼容；评分、标签、备注和观看记录保留。历史 [v0.1.1-preview](https://github.com/38huangyukong38/Lemonvn/releases/tag/v0.1.1-preview) 和 [v0.1.0-preview](https://github.com/38huangyukong38/Lemonvn/releases/tag/v0.1.0-preview) 均保留。

PowerShell校验：`Get-FileHash -Algorithm SHA256 -LiteralPath '<ZIP路径>'`，与校验文件比较。校验不能代替可信来源或代码签名。

## 卸载、隐私与许可

卸载先关闭程序，再运行同目录 `Uninstall.exe`。默认保留本地数据；选择完全删除前，先把需要的备份导出到其他目录。取消或关闭卸载窗口不删除数据。卸载依赖系统 Windows PowerShell，不改变安全策略、不卸载系统运行时。

软件免费开源，**不提供番剧视频播放、下载或视频资源**。Bangumi仍为主动公开查询，无需Token；识番仍需用户确认才上传处理后的图片。第三方服务有独立条款、额度和可用性限制。

保留 **GoldenPotato137 / PotatoVN** 原作者署名及 Apache-2.0；微软组件、C#/WinRT MIT等完整原文、维护者已确认的draft2组件条款及首次启动明确同意机制保持不变。本次没有新增第三方依赖或改变条款，不宣称微软批准或法律认证。

**尚未完成干净 Windows 系统测试；软件未正式代码签名，Windows可能显示安全提示。** 已在现有Windows11 x64开发环境验证；本轮没有验证全部DPI、所有Windows版本或MSIX，不绕过系统安全提示。没有安装器、自动更新或Microsoft Store发布。
