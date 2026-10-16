# GitHub 首次源码边界

M6-B2-B1本地待审更新（2026-10-16）：首次main源码5eb46a0已公开。本阶段仅在独立分支逐文件准备更新，未再次Push/发布。SevenZipSharp已从实际编译/运行图排除，原509用例保留、新4，共513/513；单实例1/1，合成GUI关闭重启通过。历史37项中32项补充材料、5待确认，另有隐式SDK再分发条件待决。源码与二进制仍分别准入，见[M6-B2-B1](M6_B2B1_LICENSE_AND_DEPENDENCY_CLEARANCE.md)。

以下保留此前阶段的审查时点记录；首次提交前的“无remote/未Push”等不是本地后续分支的当前状态。

项目：[38huangyukong38/Lemonvn](https://github.com/38huangyukong38/Lemonvn)。2026-10-16 匿名页面为公开且空仓库；main 由维护者确认。匿名 REST API 返回限额403，未尝试绕过或索取凭证。没有上传源码。

拟提交范围是独立客户端源码快照，保留全部 64 个客户端测试 C# 文件、4 个 TestData JSON、4 个必要项目及精简 solution；不是直接推送原开发历史。原仓库保留上游签名对象与历史，仅用于开发，不应设为新公开仓库的推送来源。

不含旧 .git、PFX/私钥/证书、个人数据/库/日志、SDK/缓存/NuGet 二进制、旧字体/游戏静态数据/样图/品牌资源、原发布工作流。图像只有自有 PNG/ICO 和一张合成收藏截图。没有任何 GitHub Actions 工作流，签名、Store、R2、Release、Crowdin 对外写入均未启用。

保留原 LICENSE、NOTICE、GoldenPotato137 与贡献者归属、固定上游 SHA；修改过的继承源码有明显的逐文件告知。UPSTREAM_PROVENANCE.json 记录来源和排除范围。不是 GitHub Fork 网络成员，不冒称上游品牌/服务/签名授权。

scripts/audit_source_snapshot.py 仅作只读路径/凭证形状检查，报告不输出凭证值；.resw 全部通过专用 resw_tool.py 审查，保留资源值。新历史仅允许一个无父根提交，无 remote、alternates、shallow 或旧 PFX 对象。启发式检查不能证明永远不存在秘密，后续每次导出仍需复核。

源码清理与公开二进制不同：源码不供应第三方 DLL，相关包由使用者按条款恢复。LGPL 与部分依赖的完整二进制义务尚未完成，见 [风险](API_AND_LICENSE_RISKS.md)。首次 Push 必须另获维护者审核授权，本阶段没有配置公开 remote。
