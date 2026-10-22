# Current client dependency and license evidence

当前发布准备状态（2026-10-17）：维护者通过本轮文字指示明确确认SDK、Windows App SDK/Windows ML适用发布者责任、代码署名/图标公开分发权利及draft2条款/首启机制。第1～5节与确认机制未作实质修改。记录不构成签名、阅读证明、微软或法律认证；实际Push、Tag、Release和公开ZIP仍等待“确认正式发布”。以下待审/内部候选描述保留为历史记录，最新状态以RELEASE_CHECKLIST末尾和release-review/MAINTAINER_CONFIRMATIONS.json为准。

Plan A .NET10 Preview: 121 ordinary NuGet packages; implicit runtime packs are listed separately. Actual AppSDK2.4.0 / WinUI2.3.6 package licensing is corrected; source and binary scope are separate, and publisher/end-user approvals remain pending.

| Package | Version | Declared license metadata | Actual matching output files |
| --- | --- | --- | --- |
| CommunityToolkit.Common | 8.2.1 | MIT | 1 |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | 1 |
| CommunityToolkit.WinUI.Animations | 8.1.240916 | MIT | 1 |
| CommunityToolkit.WinUI.Behaviors | 8.1.240916 | MIT | 1 |
| CommunityToolkit.WinUI.Collections | 8.2.250402 | MIT | 1 |
| CommunityToolkit.WinUI.Controls.Primitives | 8.1.240916 | MIT | 1 |
| CommunityToolkit.WinUI.Controls.Sizers | 8.2.241112-preview1 | MIT | 1 |
| CommunityToolkit.WinUI.Controls.TokenizingTextBox | 8.1.240916 | MIT | 1 |
| CommunityToolkit.WinUI.Converters | 8.2.241112-preview1 | MIT | 1 |
| CommunityToolkit.WinUI.Extensions | 8.2.250402 | MIT | 1 |
| CommunityToolkit.WinUI.Helpers | 8.2.250402 | MIT | 1 |
| CSharpMarkup.WinUI.LiveChartsCore.SkiaSharpView | 2.4.18 | MIT | 1 |
| CSharpMarkup.WinUI | 2.4.18 | MIT | 1 |
| DependencyPropertyGenerator | 1.4.0 | MIT | 0 |
| GalFingerPrint.Client | 1.0.2 | MIT | 1 |
| H.GeneratedIcons.System.Drawing | 2.0.118 | MIT | 1 |
| H.NotifyIcon.WinUI | 2.0.118 | MIT | 1 |
| H.NotifyIcon | 2.0.118 | MIT | 1 |
| HalfFullWidth | 2.0.0 | MIT | 1 |
| HarfBuzzSharp.NativeAssets.macOS | 7.3.0 | https://go.microsoft.com/fwlink/?linkid=868514 | 0 |
| HarfBuzzSharp.NativeAssets.Win32 | 7.3.0 | https://go.microsoft.com/fwlink/?linkid=868514 | 1 |
| HarfBuzzSharp | 7.3.0 | https://go.microsoft.com/fwlink/?linkid=868514 | 1 |
| HtmlAgilityPack | 1.11.46 | https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE | 1 |
| InputSimulatorPlus.NetStandard | 1.0.8 | MS-PL | 1 |
| JsonSubTypes | 2.0.1 | MIT | 1 |
| KnowledgePicker.WordCloud | 1.3.1 | MIT | 1 |
| Lib.Harmony | 2.4.2 | LICENSE | 1 |
| LiteDB | 5.0.21 | MIT | 1 |
| LiveChartsCore.Behaviours | 2.0.0-rc2 | MIT | 1 |
| LiveChartsCore.SkiaSharpView.Uno.WinUI | 2.0.0-rc2 | MIT | 1 |
| LiveChartsCore.SkiaSharpView | 2.0.0-rc2 | MIT | 1 |
| LiveChartsCore | 2.0.0-rc2 | MIT | 1 |
| Microsoft.AppCenter.Analytics | 5.0.6 | MIT | 1 |
| Microsoft.AppCenter.Crashes | 5.0.2 | MIT | 1 |
| Microsoft.AppCenter | 5.0.6 | MIT | 1 |
| Microsoft.Extensions.Configuration.Abstractions | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Configuration.Binder | 7.0.3 | MIT | 1 |
| Microsoft.Extensions.Configuration.CommandLine | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Configuration.FileExtensions | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Configuration.Json | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Configuration.UserSecrets | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Configuration | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.DependencyInjection | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.FileProviders.Abstractions | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.FileProviders.Physical | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.FileSystemGlobbing | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Hosting.Abstractions | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Hosting | 7.0.1 | MIT | 1 |
| Microsoft.Extensions.Logging.Abstractions | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Logging.Configuration | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Logging.Console | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Logging.Debug | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Logging.EventLog | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Logging.EventSource | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Logging | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Options.ConfigurationExtensions | 7.0.0 | MIT | 1 |
| Microsoft.Extensions.Options | 7.0.1 | MIT | 1 |
| Microsoft.Extensions.Primitives | 7.0.0 | MIT | 1 |
| Microsoft.Web.WebView2 | 1.0.3719.77 | LICENSE.txt | 1 |
| Microsoft.Win32.SystemEvents | 7.0.0 | MIT | 1 |
| Microsoft.Windows.AI.MachineLearning | 2.1.74 | license.txt | 2 |
| Microsoft.Windows.CsWin32 | 0.3.257 | MIT | 0 |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.251221100 | sdk_license.txt | 0 |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.4948 | https://aka.ms/WinSDKLicenseURL | 0 |
| Microsoft.Windows.SDK.Win32Docs | 0.1.42-alpha | https://aka.ms/WinSDKLicenseURL | 0 |
| Microsoft.Windows.SDK.Win32Metadata | 65.0.8-preview | sdk_license.txt | 0 |
| Microsoft.Windows.WDK.Win32Metadata | 0.13.25-experimental | sdk_license.txt | 0 |
| Microsoft.WindowsAppSDK.AI | 2.4.4 | license.txt | 7 |
| Microsoft.WindowsAppSDK.Base | 2.0.4 | license.txt | 0 |
| Microsoft.WindowsAppSDK.DWrite | 2.1.0 | license.txt | 0 |
| Microsoft.WindowsAppSDK.Foundation | 2.3.9 | license.txt | 21 |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.1.6 | license.txt | 1 |
| Microsoft.WindowsAppSDK.ML | 2.1.74 | license.txt | 0 |
| Microsoft.WindowsAppSDK.Runtime | 2.4.0 | license.txt | 0 |
| Microsoft.WindowsAppSDK.Search | 2.4.4 | license.txt | 1 |
| Microsoft.WindowsAppSDK.Widgets | 2.0.5 | license.txt | 1 |
| Microsoft.WindowsAppSDK.WinUI | 2.3.6 | license.txt | 1 |
| Microsoft.WindowsAppSDK | 2.4.0 | license.txt | 0 |
| Microsoft.Xaml.Behaviors.WinUI.Managed | 2.0.9 | MIT | 2 |
| Newtonsoft.Json | 13.0.3 | MIT | 1 |
| Polly.Core | 8.1.0 | BSD-3-Clause | 1 |
| Polly | 8.1.0 | BSD-3-Clause | 1 |
| PotatoVN.Client | 1.0.11 | MIT | 1 |
| PotatoVnDBMapper.Models | 1.1.1 | MIT | 1 |
| PotatoVnDBMapper | 1.1.1 | MIT | 1 |
| Refit.Newtonsoft.Json | 8.0.0 | MIT | 1 |
| Refit | 8.0.0 | MIT | 1 |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 | 1 |
| Serilog | 4.2.0 | Apache-2.0 | 1 |
| SharpCompress | 0.48.0 | MIT | 1 |
| SkiaSharp.HarfBuzz | 2.88.6 | https://go.microsoft.com/fwlink/?linkid=868514 | 1 |
| SkiaSharp.NativeAssets.macOS | 2.88.6 | https://go.microsoft.com/fwlink/?linkid=868514 | 0 |
| SkiaSharp.NativeAssets.Win32 | 2.88.6 | https://go.microsoft.com/fwlink/?linkid=868514 | 1 |
| SkiaSharp.Views.WinUI | 2.88.6 | https://go.microsoft.com/fwlink/?linkid=868514 | 1 |
| SkiaSharp | 2.88.6 | https://go.microsoft.com/fwlink/?linkid=868514 | 1 |
| sqlite-net-pcl | 1.9.172 | LICENSE.txt | 1 |
| SQLitePCLRaw.bundle_green | 2.1.7 | Apache-2.0 | 1 |
| SQLitePCLRaw.core | 2.1.7 | Apache-2.0 | 1 |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.13 | Apache-2.0 | 1 |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.7 | Apache-2.0 | 1 |
| System.CodeDom | 6.0.0 | MIT | 1 |
| System.Configuration.ConfigurationManager | 6.0.0 | MIT | 1 |
| System.Diagnostics.EventLog | 7.0.0 | MIT | 2 |
| System.Drawing.Common | 7.0.0 | MIT | 1 |
| System.Linq.Async | 6.0.3 | MIT | 1 |
| System.Management | 6.0.0 | MIT | 1 |
| System.Numerics.Tensors | 9.0.0 | MIT | 1 |
| System.Runtime.InteropServices.WindowsRuntime | 4.3.0 | http://go.microsoft.com/fwlink/?LinkId=329770 | 1 |
| System.Security.Cryptography.ProtectedData | 6.0.0 | MIT | 1 |
| System.Security.Permissions | 6.0.0 | MIT | 1 |
| System.Windows.Extensions | 6.0.0 | MIT | 1 |
| TinyPinyin | 1.1.0 | MIT | 1 |
| Uno.Diagnostics.Eventing | 2.0.1 |  | 1 |
| Uno.Fonts.Fluent | 2.3.0 | Apache-2.0 | 1 |
| Uno.Foundation.Logging | 5.2.108 | Apache-2.0 | 1 |
| Uno.WinUI | 5.2.108 | Apache-2.0 | 0 |
| ValveKeyValue | 0.20.0.417 | MIT | 1 |
| WanaKanaShaapu | 2.0.2 | MIT | 1 |
| WinUIEx | 2.5.0 | MIT | 1 |
| microsoft.netcore.app.runtime.win-x64 | 10.0.13 | MIT | 187 |
| microsoft.windows.sdk.net.ref | 10.0.22621.57 | https://aka.ms/WinSDKLicenseURL | 2 |

[Exact runtime inventory](verification/PLAN-A24-release-inventory.json), [notice decisions](licenses/REVIEW.json), [current release gate](RELEASE_CHECKLIST.md).

## r2 build-only launcher tooling

Zig0.15.2 (https://ziglang.org/download/) is used only to compile the Apache-2.0 LemonVN launcher. Its compiler, headers, libraries and cache are not shipped. The helper uses -nostdlib with no CRT/builtins and its PE imports are limited to KERNEL32.dll/USER32.dll; icon, version and asInvoker manifest are retained. No new NuGet/runtime dependency is added. Existing component license texts move with the unchanged runtime payload into app/licenses/.
