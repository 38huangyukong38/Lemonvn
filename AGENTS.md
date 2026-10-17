# LemonVN source development

This is the independent WinUI3/MVVM/LiteDB client source snapshot based on PotatoVN.
Read README.md, NOTICE and docs/IMPLEMENTATION_LOG.md first. Keep offline Anime
features, Revision and EpisodeRecord semantics, ZIP V1/V2 and isolated data roots.
Do not re-enable legacy games, accounts, sync, plugins or upstream services.

Build unsigned x64: scripts/Build-Release.ps1 -RunTests (installed .NET10 SDK).
All GalgameManager.Test source files and anonymized TestData are preserved.
Phraser tests call unsupported legacy external game services; E2E needs a desktop.
Use separate synthetic data roots. Never use real collections for destructive tests.
Use file-scoped C# namespaces, four spaces, existing encodings/newlines and MVVM.
Edit/read .resw values only through GalgameManager/Strings/resw_tool.py; preserve
the ResX schema and use its validate/audit-snapshot commands.
No existing release/signing/upload workflow is included. No push, publication,
history rewriting, real credentials, signing key, account or license purchase
without explicit maintainer authorization. Preserve upstream attribution.
