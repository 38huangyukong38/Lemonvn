# LemonVN v0.1.0-preview 发布检查表

本文件记录公开源码快照的当前状态；历史开发仓库及其 Git 对象不属于此快照。

- [x] 保留 GoldenPotato137 / PotatoVN Apache-2.0 许可、NOTICE 和修改说明。
- [x] .NET 10 LTS；Windows App SDK 2.4.0，WinUI 2.3.6 已采用修正后的普通许可证。
- [x] 原开发基线 Release 构建、541/541 离线测试、单实例 E2E 1/1 已通过。
- [x] AutoMapper、SevenZipSharp 从客户端发行依赖移除；LiteDB、Revision、EpisodeRecord 和 ZIP V1/V2 保留。
- [x] 已取得维护者对适用 Windows SDK 及 Windows App SDK／Windows ML 发布者责任的明确文字确认。
- [x] 维护者确认新增代码版权署名、所供图标公开分发权利以及 draft2 条款和首次启动明确同意机制。
- [x] 确认记录仅代表维护者原话，不构成签名、阅读证明、微软批准或法律认证。
- [x] 正式组件条款第 1～5 节与批准的 draft2 保持一致，原始许可证保留。
- [x] 保留首次启动同意／拒绝与数据库初始化隔离；确认记录不进入收藏备份。
- [x] 独立历史、匿名测试资料及逐文件上游修改声明予以保留；不复制旧 .git、证书或工作流。
- [ ] 最终本地候选构建、定向测试及 ZIP 哈希须按实际验证报告核对。
- [ ] 等待维护者回复「确认正式发布」，之后才允许正常 Push、创建 v0.1.0-preview Tag 和 GitHub Pre-release。

计划资产：LemonVN-v0.1.0-preview-windows-x64.zip 及同名 .zip.sha256。
中文发行说明：[RELEASE_NOTES_0.1.md](RELEASE_NOTES_0.1.md)。
许可确认依据：[MAINTAINER_CONFIRMATIONS.json](release-review/MAINTAINER_CONFIRMATIONS.json)。
安装说明：[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)。

仅 Windows x64，未签名，尚未完成干净系统测试；可能需要安装兼容 Windows App Runtime。
不提供视频播放、下载或视频资源。已识别分发条件的检查不代表全部法律要求获得认证。

## v0.1.1-preview

Retain previously confirmed component terms, LICENSE/NOTICE and original authors. Patch only manual/Bangumi metadata handling and the preview header/local cover display. No new dependency, database schema, backup format or signing.

Before publication: build the exact independent source commit; run offline regressions/single-instance and packaging checks; verify isolated GUI linking/addition/restart, protected fields and covers; prepare a fresh versioned ZIP and hash; normal main/tag Push and GitHub Pre-release only if all pass. Keep v0.1.0-preview unchanged. Actual results are recorded separately after verification, not inferred from this plan.
