# LemonVN public snapshot implementation record

Fixed PotatoVN baseline and attribution: see UPSTREAM_PROVENANCE.json, LICENSE and NOTICE.

2026-10-17 Preview preparation: maintained independent source history and copied reviewed files only; no development .git, certificates, user databases, private logs, caches or internal screenshot folders. Existing anonymized fixtures and per-file modification notices were preserved.

Current product: .NET10 LTS, WinUI3 / WindowsAppSDK2.4.0 with corrected WinUI2.3.6, offline Anime/LiteDB/Revision/EpisodeRecord and ZIP V1/V2, optional public Bangumi and explicit image recognition. No legacy game/cloud/account services re-enabled.

Prior development baseline: x64 Release0 warnings/errors, offline541/541, single-instance1/1, packaging24/24 and real isolated ZIP GUI including consent and restart. This preparation changes review/effective-state text and labels only; draft2 sections1-5, confirmation text and mechanism match the reviewed bytes. Scoped final validation is recorded separately; do not claim every historical test was rerun.

Maintainer explicitly confirmed SDK and AppSDK/WindowsML responsibilities, new-code attribution, icon public-distribution rights and draft2 acknowledgement mechanism. The record is a human instruction, not a signature, reading attestation, Microsoft approval or legal certification.

Push, tag creation and public GitHub Pre-release are not performed. Await the separate phrase “确认正式发布”.

## v0.1.1-preview maintenance

Fixed Bangumi metadata linking for title-only manual collections: new manual entries protect supplied values rather than blank form defaults. A recognized v0.1.0 six-field blanket protection signature can be proposed for blank-field completion only; the UI exposes the fields and warning before explicit confirmation. Nonempty manual values and independent intentional clears remain protected. The immutable preview still uses Revision and commits into the same local GUID; personal records and EpisodeRecord are not provider data. No database or ZIP V1/V2 format change.

The shared add/link confirmation action is outside the scroll viewer at the preview's top right. Owned/cache file-URI decoding can recover through a bounded local image stream without new HTTP requests. Existing layouts, navigation and framework versions are retained.

Build/test/GUI and release checks are recorded in the original development audit; publication is authorized by the maintainer only after these pass. Preserve v0.1.0-preview, its source tag and all assets. No old development Git history, test data, credentials or workflows were copied.

## v0.2.0-preview — 2026-10-21

The maintainer accepted the local five-part UX update and explicitly authorized its GitHub release as 0.2.0. Preserve earlier release tags and assets; continue the existing Preview channel.

Reviewed features: native shelf/company arrow paging; staged status/tag filters with button/Enter and explicit popup focus; EpisodeRecord-derived Planned/Watching/Completed with manual OnHold/Dropped exceptions; cached Bangumi results and native back navigation; a manifest-verified uninstall launcher with keep-data default and a second confirmation. The launcher renders through the original LemonVN resource identity before business activation.

No persisted model, LiteDB schema, Revision or ZIP V1/V2 format change. No new NuGet dependency, altered component terms, copied development Git history, user data, private screenshots, credentials or legacy assets. Retain the public project's asset allow-list and per-file modification notices.

Previously verified local implementation: full offline607/607 (all prior595 cases retained), final UI-focus-only targeted106/106, single-instance1/1, packaging29/29; isolated synthetic GUI and maintainer acceptance. The exact public version is rebuilt and checked before publication; actual publication results are recorded separately. Narrow-window/DPI-matrix and clean-system compatibility are not claimed.

## v0.2.0 Preview r2 — 2026-10-22

Maintainer requested strict arrow-only horizontal paging and a clean ZIP root, with application version0.2.0 unchanged and GitHub update after verification. Retain the initial0.2.0 tag/assets; the package revision uses v0.2.0-preview-r2 to pin its own build commit without moving an existing tag.

Shelf/company ListViews disable native scrolling and display only the current page of original card/group references, with responsive page sizes and boundary arrows. Click commands, section counts and native lists remain. A native no-CRT/asInvoker x64 launcher starts app/LemonVN.exe; the same root Uninstall.exe adds the explicit uninstall mode and exits. Root has8 items. Complete WinUI resources and component license texts stay together in app/; docs are grouped separately.

Uninstall validates the distribution's fixed app/ layout and deletes only manifest-owned files, while retaining legacy flat-layout compatibility, default keep-data, second confirmation, owner/hash/path/link/process safeguards and no elevation or execution-policy bypass. Data roots, LiteDB, Revision, EpisodeRecord, ZIP V1/V2, component terms and dependency versions are unchanged. NOTICE only gains the new location explanation.

Prior isolated development validation: Release0 warnings/errors, targeted65/65, packaging37/37; real native relay tests for both modes with Unicode/spaces and foreign working directory; synthetic GUI verifies wheel does not change pages, arrows/boundaries, vertical scrolling, detail/back and uninstall-root confirmation/cancel. All12 raw records and the source/guard remain unchanged. Exact public source is rebuilt/tested before publication; results are recorded separately. No development history, private evidence, compiler binaries or unreviewed assets copied.
