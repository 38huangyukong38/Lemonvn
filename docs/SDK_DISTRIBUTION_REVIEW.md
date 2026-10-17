# Windows SDK 再分发复核与发布者确认单

当前发布准备状态（2026-10-17）：维护者通过本轮文字指示明确确认SDK、Windows App SDK/Windows ML适用发布者责任、代码署名/图标公开分发权利及draft2条款/首启机制。第1～5节与确认机制未作实质修改。记录不构成签名、阅读证明、微软或法律认证；实际Push、Tag、Release和公开ZIP仍等待“确认正式发布”。以下待审/内部候选描述保留为历史记录，最新状态以RELEASE_CHECKLIST末尾和release-review/MAINTAINER_CONFIRMATIONS.json为准。

状态：**供维护者审核的发布准备材料，尚未接受发布者义务，尚未取得公开ZIP准入**。日期2026-10-17；只复核SDK，已解决的五项开源组件保持原结论。

## 实际组件与原文

LemonVN V0.1 / .NET10内部ZIP实际携带 `Microsoft.Windows.SDK.NET.dll` 和 `WinRT.Runtime.dll`。两者来源为隐式包 `Microsoft.Windows.SDK.NET.Ref 10.0.22621.57` 的 `lib/net8.0/`，用于WinRT调用。包名有Ref不代表这些DLL只供编译；编译器/analyzers/winmd/SDK工具没有因此成为ZIP内容。逐字节来源核对见 `verification/SDKICON-sdk-binary-and-terms.json`。

[微软REDIST清单](https://learn.microsoft.com/en-us/legal/windows-sdk/redist)的Microsoft.Windows.SDK.NET.Ref小节明确列出上述两文件，可以未修改形式作为应用的一部分分发。它仍以适用SDK条款为条件，不是无条件授权，也不能因免费Preview而忽略。

完整未修改SDK原文：仓库 `docs/licenses/supplemental/Microsoft.Windows.SDK.NET.Ref/10.0.22621.57/sdk_license.rtf`；ZIP内为 `licenses/supplemental/Microsoft.Windows.SDK.NET.Ref/10.0.22621.57/sdk_license.rtf`。EULAID为 `WIN10SDK.RTM.AUG_2018_en-US`，SHA256为 `DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`。2026-10-17重新从包所指[官方地址](https://aka.ms/WinSDKLicenseURL)下载，字节相同。本文是履约映射，不替换原文或修改其拼写。

## 第2.a节逐项映射

| 原文要求 | 对当前ZIP的适用情况 | 当前状态 |
| --- | --- | --- |
| 2.a.i：REDIST中的对象代码及随应用分发 | 两文件均在官方清单，原样保留 | 权利范围已确认，仍须满足条件 |
| 2.a.ii：程序具有显著主要功能 | 离线番剧收藏、进度、备份等；不是单独再售SDK | 工程事实已具备 |
| 2.a.ii：.lib只分发链接结果 | 当前仅两个DLL，不分发SDK.lib | 该分支不适用 |
| 2.a.ii：setup中的代码仅随setup且未修改 | 当前为ZIP，无setup载荷 | 不引入安装器；该分支无载荷 |
| 2.a.ii：分发者与外部最终用户须同意足够保护SDK的条款 | 需要受限于SDK组件的用户条款和有效同意流程 | 已准备草案及内部测试确认功能；文案未批准，不视为发布者同意 |
| 2.a.ii：Windows Performance Toolkit整体分发条件 | 当前无该工具包 | 该分支不适用 |
| 2.a.ii：展示自己的有效版权声明 | 拟用LemonVN新增代码署名见下文 | 待维护者核实权利及署名 |
| 2.a.ii：向微软作出赔偿、抗辩及免责承诺（含相关律师费） | 不因免费、GitHub、Preview或不进商店而豁免 | 必须维护者本人阅读决定；AI未代接受 |
| 2.a.iii：保留通知/商标，Windows平台，不恶意使用，不以排除性许可约束SDK | 原DLL/原文保留；仅Windows x64；SDK与Apache应用代码分开 | 工程/文档已安排，发布者仍须确认遵守 |

2.b.i的商店认证仅在进入Windows Store时适用；本项目当前不进商店。它不取消2.a的ZIP分发条件。SDK原文第7、8、13～15节的适用限制、出口、强制法律权利、微软免责声明/责任限制也不能被一段简短摘要替代。

## 版权与素材声明草案

拟用于LemonVN自有新增代码（须确认）：

`Copyright (c) 2026 38huangyukong38 and LemonVN contributors.`

这不是对PotatoVN、微软、其他第三方组件或图片的权属声明。上游GoldenPotato137及贡献者声明、原LICENSE和NOTICE继续保留。新图标来自维护者提供的`lemonvn图标.png`，仅做尺寸/格式转换；请维护者确认其可随公开程序分发的权利，不推定Apache代码许可自动覆盖该图片。

`NOTICE`中会标注此拟用行尚待维护者批准，不能据此把“有效版权声明”勾为完成。确认后才去掉草案标记，并放入最终程序分发资料。

## 最终用户条款与确认流程（内部测试功能已实现，文案待审核）

配套文案为 `SDK_END_USER_TERMS_DRAFT.md`。它仅保护上述SDK二进制及适用第三方权利，LemonVN源代码继续按原`LICENSE`中的Apache-2.0提供；不禁止用户按Apache许可修改/重建/商业使用LemonVN代码。

建议的明确同意流程：

1. 发布者先审定原SDK条款、配套草案、版权署名及本提案；没有正式批准时，所有候选继续标记内部测试。
2. 将批准的全文与原RTF完整随ZIP提供，在README/安装说明/Release Notes明确作用范围和版本。下载或解压本身不在工程记录中当作已同意。
3. 维护者本轮仅批准功能开发：实际在WinAppSDK Bootstrap及Application.Start之后、业务激活/数据库/网络服务初始化之前，以WinUI窗口显示范围、内容哈希和“阅读全文”“同意并继续”“不同意并退出”。不设置默认同意；没有默认勾选、超时自动同意或暗中替用户点击。旧的强行Bootstrap前提案由本条取代。
4. 只有明确同意才继续；本地同意记录包含应用身份、条款版本/全文哈希和UTC时间，不含账户、图片、设备标识或网络上传。不同意不打开收藏数据库、不改进度。此记录独立于收藏与ZIP备份；SDK/条款版本变化重新确认。
5. 对再次分发者另要求核对原始许可和延续适用最终用户条件；维护者/最终用户/再次分发者的责任分别确认，不相互冒充。
6. 内部测试涵盖拒绝/关闭/阅读/同意/重启与条款内容变化；完成工程验证不等于批准草案、满足全部法律要求或接受发布者赔偿责任。审批记录仍保持未填写。

若维护者选择其他确认方式，应说明如何取得并保留真实同意；仅把RTF放在ZIP、写“下载即同意”或默认配置为true，不能作为本次工程验收中“同意流程已落实”的证据。本提案不声称草案已经与微软协议等效，需审核措辞及法律效力。

## 本人审批记录（均未填写）

- [ ] 已读完整SDK原文，特别2.a.ii发布者责任，并决定是否承担。
- [ ] 核实拟用LemonVN版权署名和新图标的公开分发权利。
- [ ] 审定正式最终用户条款及确认文案；本轮功能实现/测试已单独授权，不代表本项批准。
- [ ] 对最终源码、ZIP哈希及限制说明审核后，另行授权同步/Push/Tag/Release。

没有本人确认时不填写姓名/签字、不代勾选、不生成虚假的“同意时间”。本文件准备完成不表示上述批准已经发生。

## 首启功能工程验证（2026-10-17）

实现da924a8及全文多行修复615050d已经验证：x64 Release 0警告/错误，535/535离线（旧514全部保留、新21）、单实例1/1、工具21/21。SDK原RTF/Apache LICENSE未改。真实独立ZIP的浅/深色窗口、本地全文、拒绝退出、明确选择后进入业务、重启不重复及正文变化重新提示均通过；改变正文后的标题栏关闭未改旧同意记录或收藏。只在合成根记录测试选择，绝非维护者签名/批准。见[一致性证据](verification/CONSENT-final-consistency.json)、[浅色](verification/CONSENT-gui-light.jpg)、[深色](verification/CONSENT-gui-dark.jpg)、[全文](verification/CONSENT-gui-full-text.jpg)。

公开发布依然等待本人审定：SDK第2.a.ii赔偿/抗辩及相关费用等发布者责任、NOTICE拟用版权与图标分发权利、最终用户文案和适用保护程度。技术验证不会自动勾选上述审批记录。

## 方案A最新适用范围（2026-10-17，未批准合同）

方案A选择只授权技术准备。当前组件应分别审阅：Windows SDK投影继续使用上述原始RTF/REDIST依据；WinRT.Runtime与官方CsWinRT2.2.0 MIT包精确同字节，另按MIT原文/通知提供；Windows App SDK2.4.0及WinUI2.3.6使用当前普通包许可。旧WinUI2.1.0误附预览许可的工程问题由升级处理，不等于普通AppSDK义务免除。

补充文案更新为draft2，覆盖上述范围与独立许可，不把SDK限制施加到Apache/MIT代码。首启全文增加AppSDK及MIT原文，内容身份变化应重新确认。SDK第2.a.ii的赔偿/抗辩/律师费等，与AppSDK第3.b.iii（含仅基于未修改可分发代码索赔的例外）必须分别由本人审核；本机记录不能转交或代替这些责任。当前文案是否足够保护相应组件及在当地有效仍须审定，不能自动转为正式条款。本文件既有本人审批清单继续全部未勾选。

## 方案A工程验收补充（2026-10-17）

当前实现eddb014已完成最终x64 Release、541项离线、单实例1项及24项工具测试；独立新ZIP实际验证完整原文入口、拒绝正常退出、明确测试继续后进入五页面和重启不重复提示。SDK原文未改，WinUI2.3.6许可文件问题已修复，AppSDK和独立MIT运行库各自材料可读；所有同意操作仅作用合成测试根，publisherApprovalRecorded保持false。

这证明确认机制能工作，不证明draft2已经满足全部法律要求。draft2仍保留草案名/未生效标识，未移除本人审批栏；成为正式最终用户条款前，须本人审核其保护程度、当地适用性、发布者责任、署名和图标权利。用户本轮“方案A”选择仅技术授权。当前SDK及AppSDK均仍有待审条件，不能把许可证文件修复等同接受赔偿。最终校验见verification/PLAN-A24-final-consistency.json。
