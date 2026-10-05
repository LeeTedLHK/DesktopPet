# DesktopPet P3 implementation plan

Status: completed. Evidence: 94 Core + 53 WPF checks, clean build, fresh review findings fixed, P3 publish/asset hash/private-runtime startup checks passed. See ../../2026-10-03-p3-verification.md and the execution ledger.

> Execution: inline, following the user's instruction to handle 4/5/6. No Git; source snapshot substitutes for worktree/commits. Preserve P2 binaries.

Spec: ../specs/2026-10-03-desktop-pet-p3-design.md

1. **Core settings and care** — Add enums/settings normalization to Models/AppSettings.cs, Models/PetProfile.cs, Companions/CareTracker.cs, Services/PetProfileService.cs. Extend offline checks with legacy defaults, corrupt recovery, reward throttling, disabled/no offline penalties, awake/sleep deltas. First run must fail for missing APIs; implement and run all offline checks green.
2. **Reminder and focus state** — Add Companions/ReminderScheduler.cs and FocusTimer.cs with explicit DateTimeOffset inputs. Tests: deadline, pause/resume, single transition after long gap, stop clears, reminder merge/expiry/spacing/disable. Run offline checks red then green.
3. **Personality and activity geometry** — Add Behaviors/PersonalityPolicy.cs, Services/ActivityAreaPolicy.cs. Connect screen boundary calculations and behavior manager. Tests: movement probabilities/wait/cursor cooldown, low energy, custom rectangle/negative origin/small area and scale. Run offline red/green.
4. **WPF integration and settings** — CompanionService drives Core and profile saving; PetController connects rewards, delivery and focus controls. Add companion tab with live state and controls, tray and pet menu entries, ApplicationService event lifecycle. Extend actual WPF checks: edit controls propagates settings; pet/feed update profile; reminders respect hide/busy; focus pause/stop; region clamps; disposal. Run WindowsIntegration red then green and render both tabs.
5. **Review and delivery** — Fresh review per executing-plans/requesting-code-review. Fix verified issues with regression cases. Version 0.3.0; publish.ps1 defaults P3 paths and verify-release parameterized by output/archive. README and QA records updated. Run full offline+Windows checks, build/publish, release check/start check. State exact validation limits.

## Review focus

- Settings edits must not overwrite newer care values, and interval edits must not reset unrelated running focus.
- Suppressed/expired reminders cannot reveal manually hidden pet or pile up on exit from fullscreen.
- Pausing at a phase deadline, time reversal and long suspension must not create repeated focus completions.
- A small region must still contain the complete pet after scale/display changes.
- All timer/events/profile writes must be stopped safely on disposal.
