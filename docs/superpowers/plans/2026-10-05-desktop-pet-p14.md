# DesktopPet P14 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for root integration and dispatching-parallel-agents for the disjoint reminder and diary domains. Steps track actual checks; the user has explicitly authorized proceeding without approval prompts.

**Goal:** Deliver six usability improvements without altering the dog's art or losing saved data.

**Architecture:** Extend existing deterministic reminder scheduling and world clocks, add a standalone diary exporter, and keep UI/controller orchestration in the root domain. Neutral settings refresh appearance without restarting games; menus pause clocks and preserve challenge state.

**Tech Stack:** C#/.NET10, WPF, Windows Forms tray, offline and native checks.

**Spec:** docs/superpowers/specs/2026-10-05-desktop-pet-p14-design.md

## Global constraints

- Six scoped features only; no new raster art or dependencies. All175 assets preserved.
- AppSettings.FocusQuietEnabled defaultfalse; all existing preference defaults retained.
- Source snapshot, no Git/worktree. Preserve all previous ZIPs and real data. Root alone runs all builds/tests.

## Shared contracts / ownership

Reminder worker owns AppSettings.cs, ReminderScheduler.cs, CompanionService.cs, new OfflineChecks/P14ReminderChecks.cs ONLY. Add FocusQuietEnabled. Scheduler: HealthSnoozedUntil nullable UTC; SnoozeHealth(now,minutes), ResumeHealth(now); Tick(now,canDisplay,bool healthAllowed=true). Companion: HealthSnoozedUntil forwarding property; IsQuietFocusActive computed from option+Focus phase+paused; SnoozeHealthReminders(int minutes), ResumeHealthReminders(), with Changed event. Root owns all controllers/UI and OfflineChecks/Program.cs registration.

Diary worker owns new Core/Services/JournalExportService.cs and new OfflineChecks/P14JournalExportChecks.cs ONLY. Public static Create(string destination,string petName,PetProfile profile,DateTimeOffset createdUtc) writes a .txt snapshot; public BuildText(...) may expose deterministic formatting. Validate suffix, UTF-8 Chinese/emoji, chronological normalized entries, totals, empty journal and preservation on failed replacement. No live-data reading/writing.

Root owns new Core/Services/SettingsChangePolicy.cs, PetWorldService.cs, PetController.cs, SettingsWindow.xaml/.cs, ApplicationService.cs, TrayIconService.cs, P14 native tests and all registration/release/docs files.

## Tasks

- [x] Reminder domain: write checks against compatible reflection/JSON probes, root observes RED; implement, root runs GREEN. Preserve original delivery spacing, pending expiry and focus priority.
- [x] Diary domain: tests first with existing-compatible probes, root observes RED; implement atomic readable export, root runs GREEN.
- [x] Gameplay continuity: root adds semantic failing native tests for settings/menu world identity, phase/count/pause retention, overlapping menus, follow deadline, cancellation triggers; implement neutral policy, appearance refresh and pause/resume clocks. Update only historical expectations that explicitly conflict with the new contract.
- [x] UI/integration: focus quiet toggle, reminder snooze/resume controls/status, diary export event/dialog/status, summon event/tray command and safe tooltip. Meaningful native checks for focus/menu/manual interactions, settings events, summon bounds/preferences, UTF-8 export and tooltip limits. All newly introduced options are opt-in/manual.
- [x] Verify/review/deliver: version0.9.5, focused/full checks, fresh read-only review, fix material findings, publish P14 and startup/data checks from fresh before-stop snapshots. All175 assets and P6–P13 ZIPs unchanged. Update README/verification report and confirm release responding.

**Review focus:** paused sessions cannot advance or complete/reward; clocks exclude pause duration; neutral edits cannot refresh a world destructively. Quiet focus must not cancel explicit play or block focus completion, and snooze must not reset on unrelated settings. Export failure never overwrites a valid target; tooltip must meet native length limits with emoji names. Preserve rest/menu/hidden/dispose behavior from existing releases.
