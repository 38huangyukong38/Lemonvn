# 当前实际依赖、原文材料与分发边界

2026-10-16，M6-B2-B1。本说明区分源码、实际二进制和第三方服务，不是全部用途的授权保证。

## 当前输出与已移除依赖

客户端普通NuGet图156→155，仅移除Squid-Box.SevenZipSharp1.6.1.23，无新增包、无其他版本变化。AutoMapper仍不在图或输出中。SevenZipSharp只供未注册的旧UnpackGameTask使用；现在该原生分支在执行和恢复的任何文件读取/目录修改前拒绝。保留旧类型/序列化字段及非原生分支，未启用游戏入口，也未改变AnimeBackupService的.NET ZIP实现。

当前不存在SevenZipSharp/7za发行载荷，不再通过供应这两个组件触发对应LGPL义务；历史源码、旧许可文本和M6-B1内部候选完整保留，旧候选不得公开。没有声称已履行旧包装库的精确源码/内嵌SFX/替换/调试义务。如果以后重新分发，必须重新落实这些义务，不能以本次排除或一个源码链接代替。

324个实际DLL/EXE包括4个本项目产物，其余320个均按字节哈希对应已还原包/运行时包资产。普通NuGet表不能覆盖全部来源：另外发现Microsoft.Windows.SDK.NET.Ref10.0.22621.56隐式提供两份投影/运行时DLL，已补入材料索引，不能当成仅构建依赖。实际原生e_sqlite3报告SQLite3.53.3及官方一致source ID；Skia/HarfBuzz/.NET/WinAppSDK原有原生通知继续保留。

## 原37项缺口

当前材料索引110项，涵盖运行资产匹配包及隐式/框架材料。37项历史“包内无独立正文”不等于37项无许可。已从固定源提交/匹配版本标签补正文，并保留nuspec版权声明及哈希；SQLite另外核对实际引擎与官方public-domain声明。32项材料覆盖，5项仍需确认：

| 组件 | 尚待确认 |
| --- | --- |
| GalFingerPrint.Client1.0.2 | 包声明MIT/No Copyright，固定源提交无许可正文；确认生成客户端许可/归属，不凭项目名推断 |
| PotatoVN.Client1.0.11 | 包声明MIT，记录的源提交无法取得；当前Apache仓库不能冒充该包的精确声明 |
| PotatoVnDBMapper及Models1.1.1 | 包声明MIT，固定源提交正文Apache；保留两种事实，确认各自适用范围，不改原文或擅自选择 |
| Uno.Diagnostics.Eventing2.0.1 | 包无license grant字段；当前仓库Apache文本标题为Uno.MonoAnalyzers，未建立精确版本对应 |

另发现的Windows SDK targeting pack使实际“包内缺正文”计数为38：这是新增覆盖范围，不能把数字下降当目标。其原始SDK RTF已取得并保留，仍需确认SDK投影DLL的适用再分发范围和发布者/最终用户条款满足方式；不以MIT仓库标签、匿名问答或拷贝RTF认定已获全部许可。

当前共276份许可/通知/nuspec/补充原文经机械哈希核对；32/5是历史37项的材料审查结果，不是对每个组件/用途的法务认证。详见[逐项记录](licenses/README.md)、[机器可读审查](licenses/REVIEW.json)及[原包索引](licenses/components/INDEX.json)。原文保持原字节，源码与二进制准入分别判断。

## 素材与API

输出、两份PRI及Uno.Fonts.Fluent.dll清单资源均没有发现字体载荷；为实际DLL补了版本标签Apache正文，不把包名等同于已分发字体。现UI用系统字体/图标，自有占位资源保留。用户封面从不作为软件发行素材。

服务调用、User-Agent和图片确认策略未改。Trace商业用途需明确批准；AnimeTrace即时API有条件允许，图像权利和规模抓取限制另计。Apache代码许可不授予服务权利，也没有被额外加上非商业禁令。
