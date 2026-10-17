# LemonVN

LemonVN 是基于 [PotatoVN](https://github.com/GoldenPotato137/PotatoVN) 修改的轻量番剧收藏与观看记录工具，使用 WinUI 3 / .NET 10 / LiteDB。

项目主页：[38huangyukong38/Lemonvn](https://github.com/38huangyukong38/Lemonvn) · [发行页](https://github.com/38huangyukong38/Lemonvn/releases)

当前最新版本：**[v0.1.1 Preview](https://github.com/38huangyukong38/Lemonvn/releases/tag/v0.1.1-preview)**。本次更新修复手动收藏关联 Bangumi 后资料未正确保存的问题，并将添加／关联确认按钮固定到资料预览右上角，无需滚动到底部即可操作。

历史版本：[v0.1.0-preview](https://github.com/38huangyukong38/Lemonvn/releases/tag/v0.1.0-preview)，保留历史下载。所有版本及附件见[发行页](https://github.com/38huangyukong38/Lemonvn/releases)。

## 功能

- 离线手动收藏、封面墙、搜索/筛选/排序、标签、评分、备注与回收站。
- 首页分区与观看状态/制作公司分类；简单进度、+1、批量及逐集记录。
- 无需账号或 Token 的 Bangumi 公开动画搜索（支持 Enter）、预览、关联及保护字段的刷新。
- 主动确认后使用 Trace.moe 截图识别或 AnimeTrace 角色识别，人工核对后联动 Bangumi。
- ZIP V2 完整收藏备份，兼容恢复 ZIP V1/V2；Revision 并发保护，EpisodeRecord 为唯一进度来源。

**软件免费开源，不提供番剧视频播放、视频下载、视频资源或账号云同步。**

## 安装运行

本版仅提供 **Windows x64 ZIP**，不提供 MSIX、安装器、自动更新或 Microsoft Store 版本。

1. 下载完整 ZIP 及同名 `.sha256`，使用 `Get-FileHash -Algorithm SHA256 -LiteralPath '<ZIP路径>'` 核对。
2. 完整解压到新的可访问目录，保留所有子目录，运行 `LemonVN.exe`。不要只复制EXE或在压缩软件内直接启动。
3. 首次运行阅读第三方组件完整原文及组件使用条款，明确同意才进入；不同意或关闭会正常退出，不打开收藏数据库。
4. 如缺少兼容的 **Windows App Runtime x64**，从[微软官方页面](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)获取兼容安装程序。当前AppSDK为2.4.0（WinUI2.3.6），框架家族 `Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe`，最低2.4.0.0。随包 `Check-Runtime.ps1` 仅作只读检测，不会安装或修改系统。

.NET 10.0.13运行时随ZIP提供，运行程序无需Visual Studio、开发SDK、Python或数据库服务器。程序以普通用户权限运行。

**本版本尚未完成干净 Windows 系统测试。** 已在现有Windows11 10.0.26200 x64开发环境独立解压验证；项目声明的最低系统版本10.0.17763不代表各Windows10版本均已验收。未测试全部DPI或MSIX部署。

**软件未经过正式代码签名，Windows可能显示安全提示。** 校验值只能检查文件一致性，不能代替签名或受信任来源；不要关闭防护或绕过系统安全提示。完整说明见[安装与运行](docs/INSTALLATION_GUIDE.md)、[中文Release Notes](docs/RELEASE_NOTES_0.1.md)。

## 数据与隐私

非打包版默认保存于 `%LocalAppData%\LemonVN`，包含数据库、封面、设置、备份及日志。ZIP免安装不等于数据随EXE移动的便携模式。

升级前在设置中导出完整备份、正常关闭旧程序、把新ZIP完整解压至新目录；不要清理原数据目录。恢复先校验并确认替换范围，执行前保存安全副本。主题与本机许可记录不进入收藏备份。

本地管理可离线使用。Bangumi发送关键词或作品ID；识番仅在主动确认后向所选服务发送去元数据的图片副本，不发送原文件路径、收藏或观看记录。取消不能撤回已经发送的内容。没有账号登录、Token保存、遥测上传或云端收藏。详见[隐私说明](docs/PRIVACY.md)。

第三方服务的额度、可用性及使用条件独立适用。Trace.moe未经明确批准不得用于商业目的；AnimeTrace仅按其允许的即时调用边界使用，图片版权另计。开源代码许可不授予第三方API或作品图像权利。详见[第三方风险与边界](docs/API_AND_LICENSE_RISKS.md)。

## 许可证与来源

LemonVN自有新增代码及适用上游代码继续按 **Apache-2.0** 提供；`WinRT.Runtime.dll`另有精确CsWinRT2.2.0 MIT许可依据；Windows SDK及Windows App SDK/ML组件使用各自未经修改的原始许可。详见[LICENSE](LICENSE)、[NOTICE](NOTICE)、[第三方声明](THIRD_PARTY_NOTICES.md)及随包licenses目录。

维护者已明确确认适用发布者责任、新增代码署名、所提供图标公开分发权利，以及draft2组件条款/首启机制。该记录是维护者文字指示，不是签名、逐条阅读证明、微软认可或法律认证；不声称在所有地区满足全部法律要求。当前组件条款沿用历史文件名[SDK_END_USER_TERMS_DRAFT.md](docs/SDK_END_USER_TERMS_DRAFT.md)，正文已按确认状态标识，第1～5节及确认机制与审阅版draft2不变。

感谢 **GoldenPotato137 及 PotatoVN 全部贡献者**。固定上游基线为 `2adef493ea0d82d27d4037e91507e558c567305d`，保留原版权、Apache正文和修改说明。LemonVN是独立修改，不继承PotatoVN的商店、签名、账号或品牌授权。

## 开发

客户端位于GalgameManager，使用.NET10 SDK、WinUI3与Windows App SDK2.4.0。仅本地构建无需签名证书。离线回归与部署说明见[开发及安装说明](docs/INSTALLATION_GUIDE.md)。遵循AGENTS.md，修改本地化只使用resw_tool，不启用上游旧发布工作流。
