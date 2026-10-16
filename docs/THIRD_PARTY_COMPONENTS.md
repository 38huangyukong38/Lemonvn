# 实际依赖、原文材料与分发边界

项目主页：https://github.com/38huangyukong38/Lemonvn 。本说明区分源代码快照和未来的 Windows 二进制；不是全部第三方权利的保证。

## 当前客户端

M6-B2-A从客户端移除AutoMapper引用、两处旧角色自动映射和无用注入，保留全部客户端测试与类型转发。旧服务器仅留在原开发仓库，不进入公开客户端快照；不能据此宣称原开发仓库的每个项目都已移除AutoMapper。当前客户端NuGet图为156包，包含构建期组件，不能把156当作运行DLL数量。

移除该依赖还去掉4个IdentityModel包，并使5个共享扩展包解析回已有Hosting7所需版本。完整图与变更记录是验证依据；没有把它描述成只删一个DLL。当前NuGet查询未报告已知漏洞，不代表无漏洞或所有组件仍处于支持期。.NET8与扩展包家族的统一维护需后续单独评估。

源码快照不包含NuGet/runtime二进制或SDK，编译时通过NuGet.Config中的公开源还原。包引用不把第三方库改许为Apache-2.0，编译/使用者仍须遵守对应条款。实际已有原包license/notice/nuspec及运行时材料见[组件索引](licenses/components/INDEX.json)；标准全文见[标准许可目录](licenses/standard/)。只缺包正文但有SPDX元数据，不等于“无许可证”，也不等于已补齐版权通知。

## SevenZipSharp

- NuGet：Squid-Box.SevenZipSharp **1.6.1.23**，nuspec声明 **LGPL-3.0-only**，作者元数据Joel Ahlgren，项目源为 [squid-box/SevenZipSharp](https://github.com/squid-box/SevenZipSharp)。官方仓库已归档，不能把当前dev分支等同于该精确包的源提交。
- 托管DLL独立动态引用，未修改包内代码；当前输出DLL哈希与固定NuGet资产匹配。源码中仍保留旧压缩服务及测试，LemonVN启动白名单不注册该业务，Anime备份使用.NET ZIP。
- 旧手工携带的x64/x86 native7za从当前输出及源码快照排除；这不证明包装库的内嵌SFX等内容已无义务。原开发仓库资源/历史保留，不使用原签名材料。
- 原包license及nuspec见 [版本材料](licenses/components/Squid-Box.SevenZipSharp/1.6.1.23/)，同时提供LGPL3及其引用的GPL3完整标准文本，均保留原文。元数据与哈希用于识别来源，不代替完整版权/构建来源。
- 若未来分发DLL，须明确告知库名/版本/适用LGPL，保留全部适用版权/许可；审查LGPL第4节的Application/Combined Work边界、修改后调试/替换权利和相应源码交付路径。
- 外置DLL的共享库机制可作为评估方向；未来须实际确认兼容修改版本可替换使用、没有阻止调试修改的许可条款。不能以“库未在启动加载”“公开了LemonVN源码”或“放了GitHub链接”代替完整义务。
- **待完成**：该包的精确源提交/构建对应关系、嵌入内容的版权/源码、适用的替换与源码提供方案。当前源参考仓库和包来源已标识，但不冒认已获取所有精确对应源码。公开二进制仍不通过。

## 其他运行与原生组件

.NET8.0.32与Windows App SDK2.1.3、SkiaSharp2.88.6/HarfBuzz7.3.0、LiteDB5.0.21、MVVM、Serilog及其他实际携带库各有条款。原包中可用的MIT/Microsoft等正文、.NET和Skia原生第三方通知均按版本收集，不只附一个SPDX名称。

当前材料索引有110个包/运行时项目，仍有37项没有精确包内正文可直接取得；nuspec、表达式和标准全文仅为补充。正式ZIP前仍需检查具体版权、原生构建、对应源码和可再分发组件范围。旧游戏数据库、样图、字体、上游品牌资产不再随当前输出或干净源码快照携带；用户封面从不作为软件发行素材。

## 服务与代码许可

Bangumi、Trace.moe、AnimeTrace是独立服务；匿名可用、免费运营、开源代码和商业服务授权是不同事项。使用真实开发者/版本/主页User-Agent，不嵌入共享密钥。Trace商业用途需明确批准；AnimeTrace实时调用有条件允许，规模抓取和图像权利另外处理。详见[API风险](API_AND_LICENSE_RISKS.md)和[隐私](PRIVACY.md)。Apache-2.0代码许可没有添加非商业禁令。
