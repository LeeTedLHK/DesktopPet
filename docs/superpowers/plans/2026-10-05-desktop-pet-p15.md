# DesktopPet P15 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for root integration and dispatching-parallel-agents for independent Core query/export and focus domains. Steps track actual evidence. User authorization bypasses approval pauses; no Git commits/worktree are possible.

**Goal:** Make diaries easy to find/export and current focus progress easy to read without disrupting sessions.

**Architecture:** A pure diary query is shared by WPF and filtered export. Focus details expose the existing actual timer snapshot. Root owns UI/cache/application orchestration and serialized validation.

**Tech Stack:** C#/.NET10 WPF; offline/native check executables; no dependencies.

**Spec:** docs/superpowers/specs/2026-10-05-desktop-pet-p15-design.md

## Global constraints

- Version0.9.6/P15; all175 assets, existing preferences and real diary retained; no new defaults.
- Source ZIP baseline; no Git. All builds/tests by root, serialized; temporary profiles only.
- Existing JournalExportService.Create4/BuildText3 stay compatible. No overload named Create.

## Tasks / exact ownership

- [x] Query/export worker: owns new Core/Journal/JournalQuery.cs (types may share file), existing Core/Services/JournalExportService.cs, new OfflineChecks/P15JournalQueryChecks.cs ONLY. Write reflection-compatible checks first; root registers --p15-journal-only and confirms missing-query RED, then implement spec APIs. Pin whitespace/Unicode/type AND, absolute ordering, stored-local-date ranges/endpoints/future, minimum-date saturation/invalid enums, immutable snapshot, filtered counts/criteria/totals/chronological export, full compatibility and locked-target preservation. Root runs GREEN and full offline.
- [x] Focus worker: owns Core/Companions/FocusTimer.cs and new OfflineChecks/P15FocusChecks.cs ONLY. Write reflection-compatible RED for Duration/EstimatedEndUtc/Progress, then root signals implementation. Check Focus/Break progress endpoints, pause/resume retained progress and new deadline, Stop/Completed, rollback, edited next-session preferences independence. Root registers --p15-focus-only and full suite; no worker builds.
- [x] Root UI: add P15DiaryFocusChecks.cs, register --p15-only and full native; baseline checks probe controls/event behavior reflectively to fail semantically. Implement query controls/date-kind choices/clear/empty/count, immutable CurrentJournalFilter and selection-preserving cache in SettingsWindow. Add Action<JournalFilter> ExportFilteredJournalRequested and ApplicationService SaveFileDialog/CreateFiltered binding. Add preset handlers guarded by _loading producing one SettingsEdited event; SetFocusDetails and current-session progress/end readout. Test actual filtered entries, no matching entries, retained ItemsSource/selection on focus ticks, rollover refresh, query retention on new snapshots, clear, captured export filter and preset preserving current focus/fetch. Render nonempty/empty journal and running/paused focus.
- [x] Review/delivery: fresh read-only review; fix material issues, bump version/scripts/README to0.9.6/P15, full OfflineChecks/JournalChecks/native checks and appropriate release hashes. Snapshot before stop, publish/startcheck, preserve all previous archives and assets, compare actual data and final responding runtime. Complete verification report and this checklist only with evidence.

## Review focus

- Midnight local-date rollover with stable/disabled diary must refresh relative filters without rebuilding each second; native test uses an explicit date-provider seam or controlled previous-date field.
- Search text/type/date selection persists when new profile objects arrive; result ordering uses actual timestamp even when local date/offset order differs.
- UI/export share filter and captured date; no lifetime counters accidentally become filtered totals and no zero-result export overwrites a meaningful file through an enabled button.
- Preset clicks during paused/running focus or a paused game cannot restart current timers; the progress denominator is session duration.
- Query/filter controls cannot emit SettingsEdited; partial initialization cannot access missing controls. Failed filtered export shares tested replacement cleanup and leaves live profile immutable.
