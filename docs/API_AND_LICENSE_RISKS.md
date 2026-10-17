# 第三方许可证与 API 授权风险清单

当前发布准备状态（2026-10-17）：维护者通过本轮文字指示明确确认SDK、Windows App SDK/Windows ML适用发布者责任、代码署名/图标公开分发权利及draft2条款/首启机制。第1～5节与确认机制未作实质修改。记录不构成签名、阅读证明、微软或法律认证；实际Push、Tag、Release和公开ZIP仍等待“确认正式发布”。以下待审/内部候选描述保留为历史记录，最新状态以RELEASE_CHECKLIST末尾和release-review/MAINTAINER_CONFIRMATIONS.json为准。

最新.NET10最小补查：五个开源组件的授权/通知依据已补齐，不再保留为“缺包内LICENSE即阻断”的问题。映射器固定源共享Directory.Build.props明确为两个库设置MIT；生成客户端精确包MIT声明/原始归属保留；Uno官方同一Apache正文可追至2019初始提交。当前SDK.NET.Ref10.0.22621.57实际分发两DLL，微软[REDIST](https://learn.microsoft.com/en-us/legal/windows-sdk/redist)明确允许随应用未修改分发，**范围已确认**；尚需维护者确认落实发布者责任与外部最终用户条款。当前仍仅内部ZIP，不代用户接受SDK赔偿义务。API调用规则未改，免费/开源不豁免第三方条款。以下段落保留历史审查状态。

V0.1 Preview 本机候选：沿用B1材料，仅核对新ZIP实际六项DLL/版本与276份许可原文哈希；六项范围未决状态保持。允许维护者本机内部测试不构成公开二进制准入，没有购买授权、联系权利人或改变API规则。用户已将干净系统测试延期，本轮不会把本机运行当作该项通过。见[V0.1内部候选报告](V0_1_PREVIEW_ZIP_VERIFICATION.md)。

M6-B2-B1当前更新：SevenZipSharp原生游戏后端已在I/O前明确禁用并从引用/输出移除；普通NuGet155包，无其他版本变化。历史37项中32项补充材料覆盖、5项仍需确认；实际DLL来源审查另补Windows SDK隐式包及其再分发/最终用户条款问题，共6项待决。完整许可义务尚未全部解决，不批准公开ZIP。详见[M6-B2-B1](M6_B2B1_LICENSE_AND_DEPENDENCY_CLEARANCE.md)、[逐项材料](licenses/README.md)和[当前依赖](THIRD_PARTY_LICENSES.md)。此前M6-A/B1/B2-A段落保留当时事实。

M6-B2-A更新：客户端AutoMapper16.1.1及4个IdentityModel包已移除，图161→156；旧服务器留在开发树但不进入客户端快照。当前输出/快照只保留自有占位图，旧native7za/字体/静态数据库/样图排除。独立GitHub主页已存在并进入Bangumi/识番UA。逐文件修改告知已在快照完成。当前110项原文材料中37项缺精确包正文，SevenZipSharp精确对应源码/嵌入SFX/替换方案仍阻断公开ZIP。详见 [最新组件边界](THIRD_PARTY_COMPONENTS.md)和[M6-B2-A报告](M6_B2A_OPEN_SOURCE_CLEARANCE.md)。下面原M6-A/B1表格的AutoMapper携带、161包、旧资源和无主页描述为历史审查快照，不用于覆盖当前状态。

## 以下为 M6-A/B1 历史审查记录


审查日期：2026-10-16（Asia/Shanghai）。范围为当前本地 V0.1 x64 Release 构建，不是公开发布、商业许可或合规认证。M5.5 功能用户验收与第三方授权分别处理。

## 发布前需要决定或补齐

| 编号 | 事项 | 已核实事实 | 状态及下一步 |
| --- | --- | --- | --- |
| L1 | AutoMapper 16.1.1 | 安装包 LICENSE.md 为 RPL-1.5／Lucky Penny 许可路径；AutoMapper.dll 实际随输出携带。 | **阻断全用途分发结论**：未选择并落实许可路径。可后续隔离旧代码后去除依赖，或由用户决定适用授权/开源义务；不擅自改变项目许可、注册、购买或填写Key。 |
| L2 | SevenZipSharp / 原生7za | NuGet声明LGPL-3.0-only；原生7za.dll文件版本23.01，x64/x86均携带。 | **分发材料不完整**：核实原生构建来源、适用许可、完整通知、源码/替换链接义务。当前LemonVN ZIP使用.NET ZIP，不能据此忽略仍携带的旧库。 |
| L3 | 原生库及运行时通知 | SkiaSharp2.88.6、.NET8.0.32、WinAppSDK及其他库有各自条款；161项NuGet元数据已枚举。 | **清单不是清关**：补齐完整文本及原生内嵌第三方声明。18项文件式、43项旧URL/未声明式元数据需要按具体版本复核；不等于43项都无许可证。 |
| L4 | 静态数据、字体及图像 | 输出含vn_mapper.db、ex-vndb.db、VNDB标签/翻译、制作公司数据、Segoe Fluent Icons字体、上游图标/样图。 | **来源/使用范围待逐项确认**：代码Apache-2.0不能自动覆盖数据库或素材。优先在后续阶段验证排除无用资源，保留所需归属。 |
| L5 | 上游修改告知 | 原LICENSE保留，本轮补全项目级修改来源NOTICE。 | 发布源码前仍需逐文件核查Apache-2.0第4节的修改告知及归属义务，不能用根目录摘要代替所有具体要求；本轮未批量重写源码头部。 |
| A1 | Trace.moe | 官方条款禁止未经明确批准的商业用途；公开非商业调用仍受服务限制，无SLA。 | **商业用途必须先获明确批准**。非商业公开客户端的预期用途/流量及集成边界应在分发前确认；本轮不替用户承诺已取得任何商业许可。 |
| A2 | AnimeTrace | 官方版权页公开允许个人和商用API调用，禁止未经许可的系统性规模抓取；识别图像权利另计。 | 当前手动即时调用、文本候选、不用角色图/Agent与该边界一致。网站UI/Logo和角色图片不因此获得复制/商业使用许可。 |
| A3 | Bangumi | 开发者平台允许API应用，数据使用受版权声明约束，公开开源项目UA需有项目主页。 | 保留来源/作品ID，分发数据或封面须处理归属和素材权利。当前尚无独立公开主页，公开仓库确定后补真实URL，不能冒用PotatoVN。 |
| S1 | 旧原生解压器维护 | 7za23.01早于官方后续多次安全修复；未发现LemonVN可达解压入口。NuGet漏洞扫描不覆盖此手工携带DLL。 | 分发前优先移除旧组件或核实版本/适用修复，不能以NuGet无报告推定此DLL安全。没有据此宣称当前Anime备份可被某个具体CVE利用。 |
| S2 | .NET生命周期 | 当前自包含运行时8.0.32；官方列.NET8支持结束为2026-11-10。 | 发布计划需覆盖运行时更新/迁移；本轮不盲目升级框架。自包含运行时需随应用更新维护。 |

## 官方依据

- AutoMapper：[固定版本包](https://www.nuget.org/packages/AutoMapper/16.1.1)、[作者许可FAQ](https://luckypennysoftware.com/faq)、[客户端密钥配置说明](https://docs.automapper.io/en/latest/License-configuration.html)。包内LICENSE.md及其SHA见清单JSON。运行时没有强制停用不代表授权已解决；桌面应用不能把私密许可Key随客户端分发。不要为规避许可直接降级未审查的旧版本。
- 7-Zip：[使用与分发许可](https://7-zip.org/license.txt)、[官方版本历史](https://7-zip.org/history.txt)。当前网页包括较新版本条款，不能代替对仓库所带23.01具体二进制的出处确认。SevenZipSharp的具体LGPL声明来自本地固定版本NuGet nuspec。
- Trace：[官方条款](https://github.com/soruly/trace.moe-api/blob/master/docs/terms.md)、[额度](https://github.com/soruly/trace.moe-api/blob/master/docs/limits.md)。IP限额、服务变化、上传图片处理及商业限制独立于LemonVN代码许可；赞助额度不等于商业授权。
- AnimeTrace：[官方繁体版权/隐私页](https://ai.animedb.cn/tw/copyright/)、[API文档](https://www.animetrace.com/api-docs/)。基础识别与Agent的处理规则不同；LemonVN没有接入Agent。
- Bangumi：[版权与开发者协议](https://bgm.tv/about/copyright)、[User-Agent规范](https://github.com/bangumi/api/blob/master/docs-raw/user%20agent.md)。条目采用页面所述CC BY-SA原则且既有版权素材另按来源处理，不宜把平台可显示的封面当作自己可任意再许可的素材。个人ZIP备份与公开转载不是同一使用场景。
- Microsoft：[Segoe Fluent Icons说明](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font)、[.NET支持周期](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。字体可用于开发不等于允许在所有平台任意再分发；本项目附带字体文件的出处/适用条款仍须核实。

Trace条款、AnimeTrace官方版权页、Bangumi版权页及UA原文均在本轮以普通匿名GET重新取得200；仅保存URL、时间、状态、SHA，不复制整页文案，见 [文档抓取摘要](verification/M6A-official-document-fetch.json)。没有向作者发信、申请账户、购买许可或上传用户图片。

## 当前实际保护与剩余边界

- `LICENSE`与审查上游内容一致；新增`NOTICE`说明修改来源，`THIRD_PARTY_NOTICES.md`明确仍需许可复核，两者随本地构建复制。**摘要索引不能代替完整第三方许可包**。
- [全部NuGet清单](THIRD_PARTY_LICENSES.md)包含编译/构建期依赖；[原始清单](verification/M6A-release-inventory.json)用源文件哈希匹配实际携带项。原生系统/运行时、素材及数据不能仅靠NuGet许可字段覆盖。
- LemonVN注册/页面白名单继续隔离旧游戏、账户、同步、插件和遥测。启动时点观察未加载已筛选的旧组件且TCP为0；这是时点证据，不是持续抓包或所有代码路径不可达的形式证明。
- 旧上游临时PFX仍存在于上游Git索引/历史，工作树稀疏排除；本轮增加`export-ignore`并通过`git check-attr`验证，未生成归档、读取/导入/使用证书，Release输出没有PFX或用户库。完整Git克隆仍保留上游历史，后续源码交付要明确边界，不重写历史。见[导出规则检查](verification/M6A-source-export-policy.json)。
- 日文新增文案仍有英文回退；这属于本地化质量限制，不代表法律条款已翻译或已同意。

在上述事项完成前，只能确认本轮工程审查与测试的事实，不能声明“所有公开分发／商业用途均已授权”。M6-B需用户审核后另行授权。

## M6-B1 补充：免费开源目标与内部候选边界

用户已验收 M6-A 工程结果并授权 M6-B1；计划免费提供、个人非商业维护、优先 Apache-2.0。该目标不替第三方授权，也不缩减 Apache-2.0 本身的商业使用许可。当前既有历史及二进制均未获公开准入。

- **AutoMapper 16.1.1**：固定包 LICENSE 指向 RPL-1.5／Lucky Penny 协议。官方 FAQ 提供 RPL 免费路径及有条件的 Community License 注册路径；没有因为项目免费或个人维护就自动完成注册/授予。RPL 1.5 的 Personal Use/Research 与 Deploy 定义不同，当前候选限维护者个人本地研究，不扩大到组织内部部署或转发测试。保留 Apache-2.0 的公开发行方案仍需处理其互惠义务、适用授权或安全移除，不能自行重许用户/上游代码。当前 CharacterMapper/StaffMapper 继承 Profile，PvnService/PvnSyncTask 等签名引用 IMapper，旧 PvnServiceTest 也依赖它；LemonVN DI 不注册这些服务，但它们仍编译在同一程序集。没有仅删除 AutoMapper.dll、改 deps.json 或降级来规避问题。
- **LGPL**：SevenZipSharp 是未修改的独立动态托管 DLL，当前 native 7za 由不可达旧游戏代码按路径载入。候选排除无用 7za，但保留托管包装库，附原包 license 及标准 LGPL/GPL 文本与源仓库元数据。公开前仍需核实对应版本完整源码获取/交付、版权、用户替换/调试修改的权利、嵌入 SFX 等组成物，不能把公开 LemonVN 源码或放一个链接当作全部义务已满足。没有宣称其静态链接进 LemonVN 或已经完成法务清关。
- **素材**：候选只保留自有 LemonPlaceholder PNG/ICO；旧游戏数据、字体、样图、品牌资源仅从候选排除，源码/历史保留。当前真正运行页面只用系统 SymbolThemeFontFamily；封面占位与窗口图标指向 LemonPlaceholder。派生 DLL、PRI 与已编译 XBF 保留，需独立 GUI 验证。README 仅用合成收藏/自有占位图的既有截图，旧文档/历史中其他截图仍需公开前处理。
- **许可材料**：候选收集实际匹配 NuGet 及运行时包中的原始 license/notice/nuspec，另含 SPDX 标准文本。INDEX 明确哪些包缺少精确的原包通知；标准模板不代替作者版权声明或原生源码义务。它是个人测试材料，不是可向第三方散发的完整合规包。
- **Trace.moe**：当前条款和额度文档仍允许无 Key 的客户端直接请求，文档给出分布式客户端直连的建议；100 次/滚动24h、并发1，另有每 IP 每分钟100请求限制。不经过 LemonVN 云端中转、不嵌入共享密钥。非商业客户端集成有文档依据，但不是获得商业许可；未来商业衍生使用者需自行核实/申请，不把该服务限制写成代码许可证限制。尚未取得额外书面商业批准，没有擅自停用或替换功能。
- **AnimeTrace**：2026-07-03 API 文档为 POST /v1/search，可省略 model 由服务默认选择，当前实现没有硬编码模型。官方版权页允许有条件的个人/商用即时调用，禁止未经许可的系统性规模访问；图片、Logo、网页文案权利独立，没有可承诺的固定免费日额度。当前客户端没有接入 Agent/角色图服务。
- **Bangumi**：重新核对 OpenAPI：公开搜索不声明强制认证，详情/章节为可选认证；现客户端只请求搜索/详情，章节不写观看记录。当前批准的 UA 为 38huangyukong38/LemonVN/0.1.0 (Windows)。独立公开主页尚未存在，公开开源前必须补真实 URL；不创建虚构仓库、不冒用上游地址或要求用户交出 Token。资料/封面归属仍按官方版权约定。

官方补充来源：[AutoMapper FAQ](https://luckypennysoftware.com/faq)、[RPL 1.5](https://opensource.org/license/RPL-1.5)、[LGPL 3](https://www.gnu.org/licenses/lgpl-3.0.html)、[Trace 限额](https://github.com/soruly/trace.moe-api/blob/master/docs/limits.md)、[AnimeTrace API](https://www.animetrace.com/api-docs/)、[Bangumi OpenAPI](https://github.com/bangumi/api/blob/master/open-api/v0.yaml)。精确的 Git/运行/公开准入结论见 GITHUB_OPEN_SOURCE_AUDIT.md 和 M6_B1_DEPLOYMENT_REPORT.md。

## 2026-10-17 方案A技术准备更新

维护者选择保留WinUI/.NET10/SDK DLL，只授权技术准备，不是合同批准。当前解析AppSDK2.4.0/WinUI2.3.6，以包内修正原文替代旧受影响版本作为当前材料；SDK原RTF未修改，MIT WinRT运行库独立原文补齐。SDK第2.a.ii责任、普通AppSDK第3.b责任（包括其仅基于未修改分发代码索赔的例外）、正式外部用户文案和版权/图标权利仍待本人审定。首启确认是工程机制，不证明保护程度已满足或微软已批准。未Push/Tag/Release/公开ZIP。
