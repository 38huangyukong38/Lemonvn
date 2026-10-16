# LemonVN 客户端架构

这是基于 PotatoVN 固定基线的轻量番剧收藏记录库，使用 WinUI 3 / MVVM；不提供播放器、视频下载、游戏管理、账户或云同步。目录沿用上游名称，以便最小修改和保留来源。

- GalgameManager：LemonVN.exe、WinUI 页面、ViewModel、Anime 服务与平台适配。
- GalgameManager.Core：共享 DTO/工具，保留兼容旧代码所需类型。
- GalgameManager.WinApp.Base：Windows 客户端基础设施。
- GalgameManager.Test：完整客户端测试源码与合成 TestData，包含离线、历史解析及 E2E 类别。

Anime 独立模块负责收藏、查询、公司分类、封面、进度和备份。EpisodeRecord 是唯一进度来源；写入使用 Revision 并发控制。LiteDB 有应用身份保护，非打包与 MSIX 数据根独立，不能读取 PotatoVN 的旧库。ZIP V1/V2 可恢复，恢复前校验/确认/保留安全副本；不自动清理仍有引用的封面。

启动仅注册 LemonVN 白名单服务。为保留编译/测试兼容，仍有不在该白名单的上游游戏源码，不能重新启用游戏扫描、运行监控、存档、同步、账户、插件市场或旧更新器。未包含独立服务器、旧 AuthServer、C++工具或文档站；不是删除客户端测试来绕过验证。

Bangumi 用于用户主动查询、关联和安全刷新，个人字段/手动保护字段受保护。Trace.moe/AnimeTrace 仅在用户选择图片并确认后上传受限的去元数据副本，结果不会自动入库/修改进度。服务许可与应用源代码 Apache-2.0 不同，见 [风险](API_AND_LICENSE_RISKS.md)。

构建、数据目录及运行依赖见 [安装与开发](INSTALLATION_GUIDE.md)。保留 88 DIP 导航、首页 180 DIP 与番剧页 244 DIP 封面。未来改动应沿用现有业务服务，避免另建数据库或改写既有进度语义。
