# LemonVN third-party notices and distribution status

当前发布准备状态（2026-10-17）：维护者通过本轮文字指示明确确认SDK、Windows App SDK/Windows ML适用发布者责任、代码署名/图标公开分发权利及draft2条款/首启机制。第1～5节与确认机制未作实质修改。记录不构成签名、阅读证明、微软或法律认证；实际Push、Tag、Release和公开ZIP仍等待“确认正式发布”。以下待审/内部候选描述保留为历史记录，最新状态以RELEASE_CHECKLIST末尾和release-review/MAINTAINER_CONFIRMATIONS.json为准。

Project: https://github.com/38huangyukong38/Lemonvn

SDK follow-up (2026-10-17): the unchanged SDK license and official REDIST have
been rechecked against the two actual DLL bytes. The publisher review and
component-scoped end-user terms DRAFT are supplied as SDK_DISTRIBUTION_REVIEW.md
and SDK_END_USER_TERMS_DRAFT.md (under docs/ in source). No publisher liability
has been accepted or end-user consent recorded by the assistant. The proposed
copyright notice and final wording remain pending maintainer review. The
internal test consent window records only an explicit local acknowledgement
before business initialization, never publisher approval or responsibility. These drafts do not relicense LemonVN's Apache-2.0 source code.

The unchanged upstream LICENSE and attribution in NOTICE retain GoldenPotato137
and PotatoVN contributors' notices and the fixed upstream baseline. LemonVN code
uses Apache-2.0; a personal/non-commercial operating goal does not restrict that
license's commercial permissions. Third-party code, services and data retain
their own terms and are not relicensed by this statement.

## M6-B2-B1 current client scope

NET10 Preview follow-up (2026-10-16): this supersedes the historical counts and
pending-case descriptions below. The client now uses .NET10.0.13; the current
ordinary graph has120 packages. Five open-source package notice/grant questions
are covered without removing any accepted feature. Exact GalFingerPrint.Client
1.0.2 and PotatoVN.Client1.0.11 packages declare MIT for generated OpenAPI clients;
their original OpenAPI / No Copyright metadata and full MIT text are preserved.
No new copyright holder is invented. The fixed PotatoDBMapper source's shared
Directory.Build.props explicitly declares MIT for BOTH1.1.1 library packages;
retain that original declaration, package metadata and repository Apache text.
Uno.Diagnostics.Eventing2.0.1 is covered by its official project's unchanged
Apache-2.0 License.md, present since initial2019 commit; original nventive
copyright and the stale heading are preserved unchanged.

Microsoft.Windows.SDK.NET.Ref10.0.22621.57 is an implicit targeting/runtime pack.
Only Microsoft.Windows.SDK.NET.dll and WinRT.Runtime.dll are distributed from it,
not SDK build tools, winmd or analyzers. Microsoft explicitly lists these DLLs at
https://learn.microsoft.com/en-us/legal/windows-sdk/redist . Redistribution scope
is confirmed, subject to the linked SDK terms. Publisher responsibilities and
external end-user terms still require maintainer review/implementation before
public binary distribution; this document is not agreement on their behalf.
The unchanged SDK RTF and current .NET runtime original notices are supplied.
See docs/licenses/REVIEW.json and docs/RELEASE_CHECKLIST.md for the current gate.

The remainder of this section records the earlier M6-B2-B1 .NET8 audit.

AutoMapper and Squid-Box.SevenZipSharp are absent from the current client graph,
compiled assembly references and output. The dormant native game archive task
rejects execution and recovery before file operations. Anime ZIP backup/restore
continues to use System.IO.Compression, with existing V1/V2 contracts unchanged.
No native7za, old font, game database or sample artwork is carried in output.
Historical license evidence and old internal candidates do not describe a new
binary release; do not redistribute those old candidates.

The current ordinary NuGet graph has155 packages. Implicit runtime/targeting
packs must also be considered: Microsoft.Windows.SDK.NET.Ref10.0.22621.56 supplies
Microsoft.Windows.SDK.NET.dll and WinRT.Runtime.dll. Do not automatically apply
the application license, or a repository's MIT label, to all SDK redistributables.

## Versioned notice materials and remaining binary gate

See docs/licenses/components/INDEX.json and docs/licenses/REVIEW.json. The original
versioned package notices and supplementary upstream license bodies are preserved
without changing their terms. scripts/verify_license_materials.py verifies bytes;
it does not grant publication permission. Its current-only staged bundle is a
preparation artifact with unresolved decisions, not an approved public ZIP.

Of the37 historical package-body absences,32 now have supplementary notice
material. Five still need package/source scope or attribution confirmation:
GalFingerPrint.Client1.0.2; PotatoVN.Client1.0.11; PotatoVnDBMapper1.1.1 and its
Models1.1.1; Uno.Diagnostics.Eventing2.0.1. A further SDK targeting-pack case
requires confirmation of applicable redistributable scope and publisher/end-user
terms. Merely including the SDK RTF is not acceptance or proof of compliance.

SQLite's actual native core identifies as3.53.3 with the source ID matching the
official release; its public-domain declaration and fixed-version license text
are supplied separately from the Apache-2.0 SQLitePCLRaw wrapper/package terms.
SharpCompress's MIT body and upstream reference/unrar notice are retained with
their scopes distinguished. No RAR creation feature is provided. .NET,
Windows App SDK, SkiaSharp/HarfBuzz and other native components retain their
own original notices; the material index is not universal legal clearance.

## Third-party services

Bangumi, Trace.moe and AnimeTrace remain independent services, identified with
LemonVN's real developer/version/project User-Agent. Anonymous access, free
operation, publishing client code and commercial service permission are separate.
Trace.moe restricts unapproved commercial use; AnimeTrace permits conditional
real-time calls and restricts systematic scraping. Image rights remain separate.
No shared key, account sync, video download or automatic image upload is added.

See docs/API_AND_LICENSE_RISKS.md, docs/PRIVACY.md and
docs/M6_B2B1_LICENSE_AND_DEPENDENCY_CLEARANCE.md. No license was purchased and no
publisher obligation was accepted on the maintainer's behalf. Public binary
distribution remains unapproved pending the identified decisions and deployment.

## Plan A current SDK selection (pending publisher approval)

The client resolves Microsoft.WindowsAppSDK 2.4.0 and Microsoft.WindowsAppSDK.WinUI
2.3.6. The corrected exact package license is included; the earlier Engineering
Preview license is not used as the current redistribution basis. Microsoft
Windows SDK projections remain in the payload under the applicable SDK terms.
WinRT.Runtime.dll additionally matches the official MIT-licensed CsWinRT 2.2.0
net8.0 asset byte-for-byte; its original LICENSE and NOTICE are included.
Windows SDK and Windows App SDK publisher/end-user conditions remain distinct
from Apache-2.0 application code and the MIT runtime. No maintainer approval,
indemnity acceptance, or public binary clearance is inferred from functional tests.
