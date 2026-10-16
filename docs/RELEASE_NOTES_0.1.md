# LemonVN 0.1.0 — 内部测试候选说明

状态：M6-B2-A源码准备，项目主页 https://github.com/38huangyukong38/Lemonvn ，未来发行页 https://github.com/38huangyukong38/Lemonvn/releases ，尚未公开发行二进制、签名或上架。软件版本为 0.1.0.0，候选文件名使用 0.1.0。不包含新的产品功能，已验收的 M5.5 界面和业务保持。

## 已实现

- WinUI 3 / MVVM，独立应用与数据目录；88 DIP 导航、首页综合分区、紧凑分类和番剧封面墙。
- 手动收藏、资料编辑、评分/标签/备注、回收站、筛选排序；EpisodeRecord 统一简单/逐集进度。
- Bangumi 动画搜索与 Enter 提交、预览添加/关联、手动保护字段安全刷新、封面缓存。
- 用户确认后的 Trace.moe 截图与 AnimeTrace 角色识别、候选人工核对及 Bangumi 联动。
- ZIP V2 完整备份、V1/V2 安全恢复、校验/确认/安全副本和 Revision 并发保护。

## 候选文件清单

- LemonVN-v0.1.0-windows-x64-test.zip：仅供维护者个人本地研究测试，未完成公开准入。
- 同名 .zip.sha256：SHA-256 文件校验值。
- 候选内 FILE_MANIFEST.json：相对路径、文件大小和逐文件 SHA-256；不包含用户数据或凭证。
- LICENSE、NOTICE、THIRD_PARTY_NOTICES.md、licenses/INDEX.json 与已收集的许可证/原生声明。
- 安装、隐私、API 风险说明、runtime-requirements.json、只读 Check-Runtime.ps1。

未压缩/ZIP 实际体积与验证结果见 M6_B1_DEPLOYMENT_REPORT.md；这里不把预估大小写成事实。ZIP 不是用户数据备份，也不是数据随 EXE 移动的便携模式。

## 运行与升级

Windows x64，当前只实测 Windows 11 10.0.26200。自带 .NET 8.0.32，但仍依赖当前用户下的兼容 Windows App Runtime x64。完整解压后打开 LemonVN.exe，按 INSTALLATION_GUIDE.md 检查前置依赖。

数据默认位于 `%LocalAppData%\LemonVN`；换程序目录不会自动迁移或删除数据。升级前先导出备份、正常退出，再将新版完整解压到新目录。不同版本/部署模式的实机迁移不能仅凭本轮同版本换目录测试宣称通过。

## 已知限制与风险

- 未签名；未验证干净机器、其他 Windows 版本、全部 DPI、MSIX 安装升级或自动更新。
- AutoMapper已从客户端引用中移除，需以本阶段构建、回归及发行清单验证为准；旧M6-B1候选仍是历史内部产物，不能据此视为当前版本。
- 遗留 SevenZipSharp 动态 DLL 的 LGPL 完整义务/源码来源、包内原生库通知仍需复核；排除未使用的 7za 与资源不等于全部依赖已清关。
- Trace.moe 商业用途未经明确批准不可用；当前个人非商业运营目标不改变 Apache-2.0 代码允许商业使用的范围。
- Bangumi/识别服务由第三方维护，额度、网络和结果可能变化；断网可继续手动使用。角色识别不返回可保证的数值准确率。
- 日文新增文案存在英文回退。没有视频下载、播放、资源扫描、云同步或账号功能。

保留 GoldenPotato137 和 PotatoVN 贡献者署名。适用条款与未解决项见 THIRD_PARTY_NOTICES.md 和 API_AND_LICENSE_RISKS.md；本候选不是公开分发授权。公开仓库已由维护者创建，源码首次提交仅在本地准备，尚未Push；没有公开ZIP。
