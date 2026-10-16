# .NET10 LTS 迁移可行性评估

2026-10-16。本轮仅分析，保留所有项目net8，未下载/安装.NET10 SDK、未迁移或修改UI/数据格式。

**建议A：V0.1公开ZIP前升级到.NET10 LTS，但先由维护者批准单独迁移任务。** .NET8和9均于2026-11-10结束支持，.NET10 LTS至2028-11-14；本机仍用SDK8.0.426/运行时8.0.32。当前许可与干净部署尚未收尾，发布后很快再迁移的维护成本较高。B（暂留8）只适合作为当前可回退工程基线，若选择需明确不晚于支持结束的迁移安排，不能无期限推迟。依据[Microsoft周期](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。

## 可行性与风险

官方当前WinUI教程采用.NET10，说明平台方向可行；现WinAppSDK2.1.3分解WinUI2.1.0资产目标net6，可被更高框架选择。其余依赖包含netstandard/net6/net8资产，从TFM选择上没有已确认的整体阻碍；**这是静态推断，不等于本项目已在.NET10编译或运行通过**。仍需验证Windows投影、XAML编译器、CommunityToolkit生成器、Refit、旧反射/序列化代码、NUnit/testhost及原生Skia/HarfBuzz/SQLite加载。不得仅依赖NuGet computed compatibility列。

## 建议改动范围（待批准）

1. 在新分支/受控副本安装局部.NET10 SDK，保留8工具链与本轮检查点。先迁客户端相关4项目TFM：客户端/WinApp.Base/测试用net10-windows，Core用net10；旧服务器不因公开客户端迁移自动升级。
2. 保留当前Windows平台版本/最低OS声明，另核.NET10支持的目标Windows版本，不能照新模板随手抬高或声称旧17763仍已实测。
3. 修复ProjectTest、SingleInstance/UpgradeTestData等硬编码net8输出路径与构建/清单脚本；逐项验证SDK包/生成器和测试工具。必要的包升级单独记录，继续NUnit，不重写测试框架。
4. 维持普通JIT、不引入NativeAOT/裁剪/单文件优化；原反射、JSON、LiteDB及WinRT路径先保持。WinAppSDK是否升级与TFM升级分别决定，不能同时把问题混为一步。
5. LiteDB app_info/schema仍1，EpisodeRecord与Revision语义不变，ZIP V1/V2合同不变。TFM升级本身不要求数据迁移，但必须使用旧BSON/两种ZIP、个人字段、失败恢复与跨目录回归证明，并备份后验证关闭重启。
6. 同配置x64 Release/自包含下对比文件清单、运行时版本、未压缩大小及测试ZIP压缩体积；本轮没有.NET10构建，**不能报告具体增减数值**。.NET自包含更新会改变携带运行时和通知，需重新收集许可/漏洞资料。
7. 更新SDK/运行要求、Build-Release、Runtime说明与公开文件级同步清单。公开仓库当前无CI；未来只新增经审查的只读构建CI，不重新启用旧上游签名/发布工作流。

成功门槛：完整离线/单实例/GUI关闭重启、备份V1/V2与数据保护全部通过，新运行时/原生载荷/许可证重新审查；失败立即退回独立分支，不重写旧历史或降级编辑用户数据。

官方依据：[WinUI教程](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)、[官方模板](https://github.com/microsoft/WindowsAppSDK/blob/main/dev/Templates/Dotnet/README.md)、[固定WinUI包](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.WinUI/2.1.0)。教程不是本项目依赖组合的认证。本轮没有执行上述迁移。
