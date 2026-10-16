# 第三方 API 与许可边界

M6-B2-B1本地待审更新（2026-10-16）：首次main源码5eb46a0已公开。本阶段仅在独立分支逐文件准备更新，未再次Push/发布。SevenZipSharp已从实际编译/运行图排除，原509用例保留、新4，共513/513；单实例1/1，合成GUI关闭重启通过。历史37项中32项补充材料、5待确认，另有隐式SDK再分发条件待决。源码与二进制仍分别准入，见[M6-B2-B1](M6_B2B1_LICENSE_AND_DEPENDENCY_CLEARANCE.md)。

软件代码保留Apache-2.0、原作者与修改告知。当前SevenZipSharp/AutoMapper不分发；完整依赖许可及待决范围见[组件审查](THIRD_PARTY_COMPONENTS.md)和[逐项材料](licenses/README.md)。

Bangumi 公开搜索不声明强制认证、作品详情支持可选认证，已有匿名实测。客户端不保存 Token、不开账户 API；UA 为 `38huangyukong38/LemonVN/0.1.0 (Windows) (https://github.com/38huangyukong38/Lemonvn)`。资料/封面依其版权来源处理，不能把可查询等同可任意转载或再许可。[官方UA](https://github.com/bangumi/api/blob/master/docs-raw/user%20agent.md)、[版权](https://bgm.tv/about/copyright)。

Trace.moe 文档允许分布式客户端直接匿名调用，当前文档100次/滚动24h、并发1，另有每IP每分钟100请求限制；服务实际返回和变更为准。**未经明确批准不得用于商业目的**，免费/开源/赞助额度不等于商业批准。本客户端直连、无共享Key、不提供识别云服务，未获得额外商业书面授权。[条款](https://github.com/soruly/trace.moe-api/blob/master/docs/terms.md)、[限额](https://github.com/soruly/trace.moe-api/blob/master/docs/limits.md)。

AnimeTrace 当前公开 POST /v1/search 可匿名即时调用；官方版权页允许受条件约束的个人和商用API调用，禁止未经许可的系统性规模抓取。无可承诺的固定免费日额度；角色图片、Logo、Agent和网页素材权利独立，本客户端未接入它们。[API](https://www.animetrace.com/api-docs/)、[官方版权页](https://ai.animedb.cn/tw/copyright/)。

图片仅主动确认后上传去元数据副本；不默认落盘，不自动改库，取消不能撤回已发送内容。服务免费可调用、非商业运营、分发客户端源码和商业衍生运营是不同事项。实际用途改变前应重新核对官方条款/许可；开源代码不是服务许可转授。见 [隐私](PRIVACY.md)。

当前没有“所有用途/所有二进制已获授权”的结论。没有联系作者、购买、注册许可或上传用户图片来换取授权。
