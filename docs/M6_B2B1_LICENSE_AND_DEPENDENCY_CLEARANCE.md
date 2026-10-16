# M6-B2-B1 许可证与依赖清理

2026-10-16。本地待审更新，未Push/签名/创建Release/提供公开ZIP或开始B2-B2。首次已公开源码提交仍5eb46a0；原开发完整历史不进入公开仓库。

SevenZipSharp1.6.1.23仅为未注册旧游戏解压任务的native后端。现在执行与恢复在I/O前拒绝，去实际native调用及引用，保留类型/旧字段与非原生代码；没有重接游戏或删除已验收功能。Anime备份仍用.NET ZIP，模型/schema/Revision/EpisodeRecord/ZIP V1/V2、UI/API行为均不变。依赖图156→155，其他包版本无变化。

原树及文件级更新后的快照x64 Release/测试build均0警告错误，完整离线各513/513；原509按类名/参数名保留、新4。定向56/56，单实例1/1。原开发脚本Python16/16（原12+新4）；公开快照增加对应4项许可校验工具测试。本机真实GUI正常启动/关闭/重启，两主题、首页/库/详情返回/分类/识番/设置可操作；3条合成记录及14项只读检查通过，无真实用户数据操作、图片上传或手工覆盖恢复。

当前324个DLL/EXE含4个本项目产物，其余320个匹配包/运行时资产。普通155包之外发现Microsoft.Windows.SDK.NET.Ref10.0.22621.56隐式投影包；当前材料110项，276份原文/通知/nuspec/补充许可哈希可校验。原37项中32项材料覆盖，5项仍待版本许可/归属确认：GalFingerPrint.Client、PotatoVN.Client、PotatoVnDBMapper及Models、Uno.Diagnostics.Eventing。SDK的再分发范围/发布者及最终用户条款追加为第6项，未代维护者接受义务。详见[材料清单](licenses/README.md)、[审核JSON](licenses/REVIEW.json)、[组件](THIRD_PARTY_COMPONENTS.md)。

SQLite实际3.53.3/source-id与官方对应，public-domain引擎与Apache包装分别说明；当前输出/PRI/Uno字体DLL无字体载荷，补实际DLL的Apache正文。原SevenZip对应LGPL载荷已排除，不等于对旧库完成LGPL履约或可以转发旧内部候选。其他组件条款及服务商业限制继续适用。

**A源码仍可安全审核维护；B公开二进制义务尚未全部满足；C可准备私有干净系统验证，但须用户另行批准。** 建议先确定[.NET10迁移](M6_B2B1_DOTNET10_ASSESSMENT.md)与剩余许可处理，避免在过渡框架上重复最终[Runtime部署验收](M6_B2B1_RUNTIME_DEPLOYMENT_PLAN.md)。本轮保留net8，没有.NET10实测或体积结论，未卸载/安装Runtime，不冒称DPI/其他Windows/MSIX已验证。

剩余许可处理推荐：先保留现有运行组合，由维护者核实上游声明/微软条款及告知方式；如选择继续隔离旧指纹、映射与同步SDK，先批准独立的编译边界分析/特征测试，不在本轮大规模重构或购买授权。没有向上游自动发送请求消息。
