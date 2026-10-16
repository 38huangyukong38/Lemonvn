# LemonVN third-party notices and distribution status

Project: https://github.com/38huangyukong38/Lemonvn

The unchanged upstream LICENSE and modification/source attribution in NOTICE
cover the corresponding PotatoVN/LemonVN code. GoldenPotato137 and contributors'
copyright and applicable original notices remain. The personal/non-commercial
operating goal does not restrict Apache-2.0's commercial-use permissions.

## Source and binary scope

The reviewed source snapshot contains application source, tests, owned placeholder
artwork, and documentation. It does not vendor NuGet/runtime DLLs, SDKs, signing
keys, user data, old game datasets, third-party sample images or bundled fonts.
Package references do not relicense those libraries under Apache-2.0.

AutoMapper is removed from the LemonVN client graph and explicit compatibility
mapping preserves the measured legacy conversion behavior. Optional upstream
server projects remain in the original development checkout, outside this source
snapshot and the LemonVN client build; they have not been relicensed or cleared.

## Binary redistribution remains a separate gate

- Squid-Box.SevenZipSharp 1.6.1.23 is an unchanged dynamically referenced NuGet
  library declared LGPL-3.0-only. Its original license, GPL/LGPL texts and source
  provenance must accompany applicable distribution. Source availability,
  replacement/relinking rights and embedded components still require review.
- The inactive native 7za DLLs, old game data, sample artwork, PotatoVN branding
  and the manually bundled Segoe font are excluded from current client output.
  This does not resolve the obligations of other DLLs still present.
- .NET, Windows App SDK, SkiaSharp/HarfBuzz and all other actual binary components
  have their own copyright and native notices. Exact versioned notice collection
  and metadata are evidence, not complete redistribution clearance.

See docs/THIRD_PARTY_LICENSES.md, docs/THIRD_PARTY_COMPONENTS.md and
docs/API_AND_LICENSE_RISKS.md. Complete available license text files are retained
without altering their terms. No commercial license was purchased or key embedded.

## Third-party services

Bangumi, Trace.moe and AnimeTrace are independent services. Requests identify
LemonVN and its actual project URL. Anonymous availability and free/non-commercial
operation do not grant image rights, commercial service rights or unlimited use.
Trace.moe restricts unapproved commercial use; AnimeTrace permits conditional
real-time API calls and restricts systematic scraping. No account sync or video
download service is provided. See docs/PRIVACY.md and the official terms linked
in the API audit. This notice does not authorize a public binary release.
