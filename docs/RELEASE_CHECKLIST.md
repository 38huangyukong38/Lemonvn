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

## v0.2.0-preview — 2026-10-21

The maintainer accepted the local five-part UX update and explicitly authorized its GitHub release as 0.2.0. Preserve earlier release tags and assets; continue the existing Preview channel.

Reviewed features: native shelf/company arrow paging; staged status/tag filters with button/Enter and explicit popup focus; EpisodeRecord-derived Planned/Watching/Completed with manual OnHold/Dropped exceptions; cached Bangumi results and native back navigation; a manifest-verified uninstall launcher with keep-data default and a second confirmation. The launcher renders through the original LemonVN resource identity before business activation.

No persisted model, LiteDB schema, Revision or ZIP V1/V2 format change. No new NuGet dependency, altered component terms, copied development Git history, user data, private screenshots, credentials or legacy assets. Retain the public project's asset allow-list and per-file modification notices.

Previously verified local implementation: full offline607/607 (all prior595 cases retained), final UI-focus-only targeted106/106, single-instance1/1, packaging29/29; isolated synthetic GUI and maintainer acceptance. The exact public version is rebuilt and checked before publication; actual publication results are recorded separately. Narrow-window/DPI-matrix and clean-system compatibility are not claimed.

## v0.2.0 Preview r2 — 2026-10-22

Maintainer requested strict arrow-only horizontal paging and a clean ZIP root, with application version0.2.0 unchanged and GitHub update after verification. Retain the initial0.2.0 tag/assets; the package revision uses v0.2.0-preview-r2 to pin its own build commit without moving an existing tag.

Shelf/company ListViews disable native scrolling and display only the current page of original card/group references, with responsive page sizes and boundary arrows. Click commands, section counts and native lists remain. A native no-CRT/asInvoker x64 launcher starts app/LemonVN.exe; the same root Uninstall.exe adds the explicit uninstall mode and exits. Root has8 items. Complete WinUI resources and component license texts stay together in app/; docs are grouped separately.

Uninstall validates the distribution's fixed app/ layout and deletes only manifest-owned files, while retaining legacy flat-layout compatibility, default keep-data, second confirmation, owner/hash/path/link/process safeguards and no elevation or execution-policy bypass. Data roots, LiteDB, Revision, EpisodeRecord, ZIP V1/V2, component terms and dependency versions are unchanged. NOTICE only gains the new location explanation.

Prior isolated development validation: Release0 warnings/errors, targeted65/65, packaging37/37; real native relay tests for both modes with Unicode/spaces and foreign working directory; synthetic GUI verifies wheel does not change pages, arrows/boundaries, vertical scrolling, detail/back and uninstall-root confirmation/cancel. All12 raw records and the source/guard remain unchanged. Exact public source is rebuilt/tested before publication; results are recorded separately. No development history, private evidence, compiler binaries or unreviewed assets copied.
