# Windows App Runtime 后续部署方案

2026-10-16。仅制定可执行的M6-B2-B2方案，没有卸载/安装本机Runtime、改安全设置、执行安装器或开始干净系统验收。

## 已核对的当前要求

- .NET自包含8.0.32；程序用户无需.NET SDK、Visual Studio、构建工具或数据库服务。
- Windows App SDK2.1.3稳定版，WinAppRuntime框架家族`Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe`、x64、最低2.1.3.0。值来自固定NuGet的WindowsAppSDK-VersionInfo.json，Bootstrap参数MajorMinor=0x00020001、VersionTag为空。
- 当前是WinAppSDK框架依赖部署，手动Bootstrap.Initialize；多个自动初始化属性被关闭。.NET自包含不等于WinAppRuntime自包含。此阶段保留现方案。
- 当前工程最低OS声明17763，实测本机Windows11 build26200；不是对所有Windows10/DPI的已验证承诺。

## 建议的ZIP前置依赖流程

1. 在全新的受支持Windows x64虚拟机建立普通用户，准备可回滚VM快照，无个人收藏。最终.NET版本应先确定，避免在过渡基线上重复最终验收。
2. 复制完整私有测试目录到用户可写新路径，使用随附`scripts/Check-Runtime.ps1`及runtime-requirements.json做当前用户只读注册检查；或手动`Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.*' | Select Name,Version,Architecture,PackageFamilyName`。不要求改变执行策略。
3. 注册阳性只是预检；DDLM/Main/Singleton/Framework及Bootstrap必须实际正常。先在缺Runtime时运行EXE，确认清楚提示、正常退出、无默认数据破坏/自动重建；保留截图/退出结果。当前只审查过这条错误路径，未在本机卸载组件来假装干净机。
4. 引导用户到[Microsoft官方Runtime下载页](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)选择匹配架构和兼容版本的官方部署程序，按官方流程安装/修复，再以普通用户复核注册与GUI启动。由用户处理系统安装提示，不绕过安全提示，也不承诺所有组织策略下安装都无需管理员。应用本身保持asInvoker。
5. V0.1建议ZIP+官方链接引导，暂不捆绑安装器；若以后捆绑，先复核其签名、固定版本SHA、再分发条款和安装权限。不能把开发SDK或Visual Studio当运行前提。
6. 用少量合成收藏做首页/详情/设置、进度、备份V1/V2导入/导出、正常关闭/重启、不同程序目录及数据隔离验证。不向第三方发送图片/个人数据，不在真实库清空测试。安装前后均记录实际加载依赖。

## 自包含备选

Runtime安装与应用自带SDK投影DLL的许可是两条线；用户安装官方Runtime不会自动解决Microsoft.Windows.SDK.NET.Ref等实际随应用载荷的再分发范围及最终用户条款要求。

WindowsAppSDKSelfContained可把所需框架二进制带到输出，减少共享Runtime安装前置，但增加体积与维护责任。当前入口会无条件在非打包时Bootstrap.Initialize，若选择自包含，必须协调入口条件、UndockedRegFreeWinRT与部署初始化配置、资源/native完整性及单实例测试；不能只切一个属性。含Singleton依赖的功能另审查，当前无账户/通知扩展需求。

本轮没有做该切换或测量其体积，也不引入单文件/裁剪优化。优先维持框架依赖完成B2-B2；如普通用户安装前置确实不可接受，再由维护者批准独立自包含实验。官方依据：[部署比较](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview)、[自包含指南](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)、[部署架构](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deployment-architecture)。

**工程上可准备私有干净机验证，公开ZIP仍需先解决许可/条款与最终框架决策。** 不把本机已装Runtime的成功启动或999版本负诊断当作B2-B2通过。
