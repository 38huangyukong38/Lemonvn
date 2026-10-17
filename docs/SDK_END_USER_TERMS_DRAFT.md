# LemonVN 随附微软组件最终用户条款

**维护者确认版本：0.1.0 / 2026-10-17-draft2。维护者于2026-10-17通过文字指示确认采用。本文件沿用历史文件名SDK_END_USER_TERMS_DRAFT.md以保持读取路径兼容；本段仅更新审核/生效状态。**

本文采用维护者确认的组件告知与保护文案，不是微软出具的新授权或法律认证。各组件适用其完整原始许可及强制法律，不保证在所有地区已满足全部法律要求。第1～5节、下方确认文案及确认机制与维护者审阅的draft2保持一致。

## 1. 三类组件与独立许可

- `Microsoft.Windows.SDK.NET.dll`：来自Microsoft.Windows.SDK.NET.Ref 10.0.22621.57，适用随附的Windows SDK原始许可和REDIST条件。
- Windows App SDK 2.4.0及其当前实际随附的绑定/Bootstrapper等组件（包括WinUI 2.3.6的`Microsoft.WinUI.dll`及Windows ML 2.1.74依赖投影；未因此启用新的产品功能）：适用随附的Windows App SDK原始许可及相应第三方通知。包内材料详见`licenses/components/INDEX.json`。
- `WinRT.Runtime.dll`：与官方Microsoft.Windows.CsWinRT 2.2.0的net8.0运行库逐字节一致，另按该包的MIT许可及通知提供；不把Windows SDK的组件限制自动加到这份MIT许可上。

LemonVN自身及适用PotatoVN代码继续按根目录`LICENSE`的Apache-2.0提供。本文不改变这些源码的复制、修改、重建、再分发和商业使用权，也不重写其他第三方库、服务或素材的许可。微软及其他权利人的组件仍由各自权利人享有权利。

## 2. 随程序使用及保留权利

在发布者依法有权分发、适用原许可允许的范围内，受微软自定义许可约束的组件可作为LemonVN的一部分在Microsoft Windows上运行。没有授予超出相应原文的SDK源代码、商标背书或其他权利。

应保留适用的版权、专利、商标和许可通知，不规避技术限制，不以微软商标暗示背书，不将组件用于恶意、欺骗或违法程序。逆向工程、反编译、反汇编以及排除性许可等限制仅在对应微软原文和强制法律允许的范围内适用；不据此限制LemonVN的Apache代码或MIT运行库，不排除依法不能排除的行为和权利。

## 3. 复制及再次分发

本文不另行授予独立转售、出租或任意复制受限SDK的权利。再次分发Windows SDK对象代码须满足SDK第2.a节和REDIST；再次分发Windows App SDK代码须满足其第3节，包括显著主要功能、适用平台、通知和外部用户/分发者保护条件。不要把整个LemonVN的开源代码改按微软专有许可发布。

如果用户另行成为包含这些组件的软件分发者，应自行核实相应条件。普通运行用户不会因使用LemonVN而被虚构为已替维护者作出发布承诺。LemonVN发布者对微软的赔偿、抗辩及相关费用责任不能自动转嫁给普通最终用户，也不能由本机同意记录替代。

## 4. 免责声明、责任限制和法定权利

受限组件的微软免责声明、支持范围及责任限制依其各自原文在适用法律允许的范围内执行。Windows SDK原文第9、13～15节和Windows App SDK的相应条款不得由简短摘要取代；微软不因本文保证LemonVN可用或提供支持。

Apache-2.0及MIT代码的免责声明仍以各自原文为准；微软SDK的金额上限或限制不自动覆盖这些开源代码。不排除当地法律不得排除的消费者权利及法定救济。

## 5. 完整未经修改的原文

- Windows SDK：`licenses/supplemental/Microsoft.Windows.SDK.NET.Ref/10.0.22621.57/sdk_license.rtf`，EULAID `WIN10SDK.RTM.AUG_2018_en-US`。
- Windows App SDK：`licenses/components/microsoft.windowsappsdk/2.4.0/license.txt`；其他实际包的独立通知与原文在同一components目录中。
- Windows ML：`licenses/components/Microsoft.Windows.AI.MachineLearning/2.1.74/license.txt`及同目录ThirdPartyNotices.txt。
- C#/WinRT MIT：`licenses/supplemental/Microsoft.Windows.CsWinRT/2.2.0/LICENSE`及同目录NOTICE.txt。
- LemonVN/上游Apache-2.0：根目录`LICENSE`和`NOTICE`。

首启“阅读全文”提供这些适用许可的本地只读入口。本文不替代完整原文；若文案与对应原文不一致，须先由维护者修正审核，不能据此宣称额外权利或公开准入。

同意这些组件条款不等于同意图片上传，也不授权账户、收藏或设备信息上传。Bangumi、Trace.moe、AnimeTrace仍由用户主动操作及各自服务条件管理。

## 6. 用户确认方式

确认文案：“我已阅读本程序随附的微软组件适用条款及完整原文，并同意就相应组件遵守适用条件。我知道LemonVN源码继续按Apache-2.0提供，C#/WinRT运行库按MIT提供。”

提供“阅读全文”“同意并继续”“不同意并退出”；没有默认勾选、超时或自动同意。拒绝/关闭正常退出，不打开收藏数据库；明确同意并成功保存后才初始化业务。独立本地记录只有格式版本、应用身份、内容哈希和UTC时间，不上传、不进入收藏数据库或备份。许可身份/正文变化重新确认。

维护者的SDK、Windows App SDK及Windows ML发布者责任、版权与图标分发权利已由维护者另行明确确认；这不把责任转交给普通最终用户。本机确认记录仅表示用户对相应组件条款的选择，不是维护者签名、逐条阅读证明或微软认可。原始draft2和正文一致性记录保留于源码的release-review及verification目录。
