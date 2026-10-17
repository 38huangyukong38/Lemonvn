# LemonVN v0.1.0-preview

版本类型：**Preview / Pre-release**。目标平台：Windows x64。

LemonVN是一款免费开源的轻量番剧收藏与观看记录工具，基于GoldenPotato137及贡献者的PotatoVN修改，应用及适用上游代码沿用Apache-2.0。

## 本版功能

- 番剧封面墙、首页分区、观看状态与制作公司分类，88 DIP侧边栏与浅/深色主题。
- 手动收藏、编辑、标签、评分、备注、回收站与组合搜索筛选排序。
- 简单进度、+1、批量/逐集记录；EpisodeRecord唯一进度来源及Revision并发保护。
- Bangumi公开资料搜索、预览、添加、关联与安全刷新，支持Enter提交，无需Token。
- 主动确认后向Trace.moe或AnimeTrace上传处理后的图片识别候选，人工核对并联动Bangumi；不自动修改收藏或进度。
- 完整ZIP V2备份及ZIP V1/V2安全恢复。

**软件不提供番剧视频播放、视频下载、视频资源或账号云同步。**

## 下载与启动

发行文件：`LemonVN-v0.1.0-preview-windows-x64.zip`、同名`.zip.sha256`、本中文说明及安装运行说明。

- 当前仅提供 **Windows x64**。完整解压后运行LemonVN.exe，不要单独复制EXE。
- 内含.NET10.0.13运行时；可能需要安装兼容的 **Windows App Runtime x64**。当前AppSDK2.4.0/WinUI2.3.6，框架家族Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe，最低2.4.0.0。从[微软官方页面](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)获取；普通用户不必安装Visual Studio或开发SDK。
- 运行`Get-FileHash -Algorithm SHA256 -LiteralPath '<ZIP路径>'`并与校验文件比较。
- 首次启动阅读各组件原始许可及维护者确认版draft2条款，明确同意才进入；拒绝会正常退出。本机记录不上传、不进入收藏数据库或备份。

## 已验证范围与限制

**本版本尚未完成干净 Windows 系统测试。** 已在现有Windows11 10.0.26200 x64开发环境独立解压运行；不把该结果视为所有Windows版本、DPI或MSIX兼容性验证。

**软件未经过正式代码签名，Windows可能显示安全提示。** 请核对来源及校验值；不要关闭安全防护或绕过系统提示。本版没有MSIX、安装器、自动更新或Store发行。

默认数据位于 `%LocalAppData%\LemonVN`，ZIP不是数据便携模式。升级前导出备份、正常关闭旧程序，新版本解压至新目录；不要删除原数据。恢复替换收藏与回收站，先校验确认并保存安全副本。

第三方API有独立额度和条件，无服务可用性保证。Trace.moe商业用途需另行明确批准；AnimeTrace按即时调用边界使用。选择图片不会自动上传，取消不能撤回已发送内容。部分日文本地化可能回退英文。

WinUI错误附带预览许可证的问题已通过AppSDK2.4.0/WinUI2.3.6修复。SDK/AppSDK/ML原始许可、独立MIT运行库许可、Apache-2.0及原作者署名完整保留。维护者已明确确认相应责任及draft2机制；这不是微软批准、法律认证或逐条阅读证明。源码许可不替第三方服务或素材授予权利。

完整构建、离线回归、单实例和GUI基线证据见发布检查表；本次仅对最终文案状态、来源同步和打包变化作定向验证，不重复全部历史测试。
