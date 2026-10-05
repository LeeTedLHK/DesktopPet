# P8 设置、皮肤与数据 Implementation Plan

> Use superpowers:executing-plans and focused parallel agents for independent files; root owns shared builds and final integration.

**Goal:** Complete all five listed improvements and ship a verified 0.8.0 portable build.
**Architecture:** Existing SettingsWindow events lead to ApplicationService actions; SkinCatalog handles skin archives; a Core data backup helper serializes immutable current snapshots.
**Tech Stack:** C#, .NET10, WPF, ZIP, offline checks.
**Spec:** docs/superpowers/specs/2026-10-04-desktop-pet-p8-design.md

## Global constraints

- User explicitly authorized uninterrupted execution. No approval handoff.
- No Git repository: preserve clean source ZIP and before-user-data copies instead of worktree/commits.
- Root alone runs shared dotnet builds. Agents request RED/GREEN runs before proceeding.
- No raster edits or new dependencies. Preserve P7 references/assets and old release.

## Review focus

- Unknown valid numeric speeds survive unrelated edits, not only the new presets.
- Mouth metadata cannot be counted as a PNG or accidentally rewritten as skin.json.
- Export/backup failures never damage an existing destination; own temporary files removed.
- Hidden/toys-disabled/home-disabled menus match the actions that actually work.
- Backup snapshots retain journal, normalize invalid numbers and do not mutate current user data.

## Tasks

- [x] Task 1: SettingsWindow.xaml/.cs and P8WindowsChecks.cs: numeric animation speeds, immediate challenge gating, help/data page, BackupRequested/OpenDataRequested/SkinExportRequested events, assembly version. RED existing speed reset and disabled challenge; GREEN native verification/render.
- [x] Task 2: SkinCatalog.cs and SkinChecks/Program.cs: import optional bounded anchors; ExportZip(string id,string destination); tests for folder/ZIP anchors, unchanged payload, exported built-in/imported skin reimport, atomic target failure and limits. RED captured; final 42/42 skin checks GREEN.
- [x] Task 3: DataBackupService.cs and P8DataChecks.cs: Create(string destination,AppSettings settings,PetProfile profile,DateTimeOffset createdUtc) snapshots to three-entry ZIP, source unchanged, exact journal, normalized values and atomic target preservation. RED absent interface, then GREEN. Root registers Core checks.
- [x] Task 4: ApplicationService.cs and TrayIconService.cs: wire new settings actions; actual ZIP save dialogs, data directory, skin export; fields gate toy/home menus; native tray verification. Root registers native checks.
- [x] Task 5: version/publish/verify scripts, full regression and read-only review; backup latest real user data and stop verified P7 process for native tests; publish P8 independently; verify data/assets/runtime/process, update docs and leave new app running.

## Ledger

- Design and plan written from current source; user asked direct execution. Optional restoration and unrelated animation changes excluded from this round's listed scope.
- Task 3 complete: missing DataBackupService API RED, then all 299 Core checks GREEN (10 new backup checks).
- Settings/skin/tray RED reproduced actual bugs; latest persisted user data backed up and verified P7 PID2632 stopped for native checks.
- Tasks 1/4 complete: 24 settings + 6 tray + 5 real exported/imported default skin native checks GREEN; help top/bottom stress renders inspected by root and UI agent.
- Skin narrow fixes RED/GREEN 3/3: extended path source collisions, wrong ZIP suffix, locked target replacement.
- Independent review minor feedback addressed: backup temp ownership guard; fullscreen wording qualification.
- Full final offline suite with Windows integration passed: 299 Core + 42 skin + 83 journal + 248 native Windows = 672 checks. Focused checks are included in these totals, not counted twice.
- Independent read-only review completed with no remaining findings. Root then completed the final regression and delivery verification.
- Published 0.8.0: 668 ZIP entries, 88,662,529 bytes; all 175 P7 asset hashes unchanged. P6/P7 archives retained byte-for-byte; published assemblies match the Release build.
- Startup verified private .NET/WPF 10.0.12, second instance reveals the first and exits 0; responding P8 PID10124 remains running.
- Latest pre-restart settings and all 19 journal entries preserved; affection27 and interaction counts 2/3/9/0/1 unchanged. Care deltas 0.04509/0.09018 reflect the running timer.
- Evidence: docs/2026-10-04-p8-verification.md, p8-all-checks-final.log, p8-release-check.log, p8-delivery-check.log, p8-user-migration.json and p8-release-metadata.json.
