# 第三方 API 和许可边界

2026-10-16，客户端源代码和二进制分别审查。本项目免费、个人非商业维护；这不限制 Apache-2.0 代码本身允许的商业使用，也不授予服务、数据、图像或第三方库的全部权利。

| 项目 | 当前事实与剩余义务 |
| --- | --- |
| 原作来源 | 保留 GoldenPotato137 / PotatoVN、固定基线、原 LICENSE/NOTICE 和修改告知；没有继承商店、品牌或 SignPath 资格。 |
| AutoMapper | 客户端图和输出已安全移除，不需要为本客户端购买许可证；旧服务器不在快照中。未用降级、删除 DLL 或削弱测试规避。 |
| SevenZipSharp1.6.1.23 | 未修改的独立托管 LGPL-3.0-only 库仍由旧源码编译引用。原生7za不供应；精确对应源码、嵌入SFX、版权与用户替换/调试方案仍待落实，公开ZIP不通过。 |
| 其他二进制 | 156包还原图含构建期；当前材料索引110项中37项未取得精确包内正文。已有原文/NOTICE/nuspec按版本收集，标准正文不能替代具体版权/源码。详见 [组件](THIRD_PARTY_COMPONENTS.md)。 |
| 旧素材 | 无用字体、数据库、样图和品牌图片不在源码/当前输出中。只有自有占位图和合成截图；用户封面不作为发行素材。 |
| .NET8 | 当前8.0.32支持于2026-11-10结束；后续单独评估.NET10 LTS及 WinUI 工具链兼容，不在本阶段迁移。 |

Bangumi 公开搜索不声明强制认证、作品详情支持可选认证，已有匿名实测。客户端不保存 Token、不开账户 API；UA 为 `38huangyukong38/LemonVN/0.1.0 (Windows) (https://github.com/38huangyukong38/Lemonvn)`。资料/封面依其版权来源处理，不能把可查询等同可任意转载或再许可。[官方UA](https://github.com/bangumi/api/blob/master/docs-raw/user%20agent.md)、[版权](https://bgm.tv/about/copyright)。

Trace.moe 文档允许分布式客户端直接匿名调用，当前文档100次/滚动24h、并发1，另有每IP每分钟100请求限制；服务实际返回和变更为准。**未经明确批准不得用于商业目的**，免费/开源/赞助额度不等于商业批准。本客户端直连、无共享Key、不提供识别云服务，未获得额外商业书面授权。[条款](https://github.com/soruly/trace.moe-api/blob/master/docs/terms.md)、[限额](https://github.com/soruly/trace.moe-api/blob/master/docs/limits.md)。

AnimeTrace 当前公开 POST /v1/search 可匿名即时调用；官方版权页允许受条件约束的个人和商用API调用，禁止未经许可的系统性规模抓取。无可承诺的固定免费日额度；角色图片、Logo、Agent和网页素材权利独立，本客户端未接入它们。[API](https://www.animetrace.com/api-docs/)、[官方版权页](https://ai.animedb.cn/tw/copyright/)。

图片仅主动确认后上传去元数据副本；不默认落盘，不自动改库，取消不能撤回已发送内容。服务免费可调用、非商业运营、分发客户端源码和商业衍生运营是不同事项。实际用途改变前应重新核对官方条款/许可；开源代码不是服务许可转授。见 [隐私](PRIVACY.md)。

当前没有“所有用途/所有二进制已获授权”的结论。没有联系作者、购买、注册许可或上传用户图片来换取授权。
