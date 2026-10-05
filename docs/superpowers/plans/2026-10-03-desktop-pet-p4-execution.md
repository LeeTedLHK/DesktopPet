# P4 execution ledger

Task 0: Ruling: no Git — P4 source snapshot and isolated output stand in for branch/worktree. P3 binaries remain available. User authorized all seven features; root proceeds without redundant approval stages. Independent modules use distinct file ownership, no concurrent shared build directories.

Task 1: Completed safe skin catalog + isolated checks, 28/28. WPF preflight decodes every frame before switch; failures preserve the active skin. Import artifacts remain separate from the live user's managed skins.

Task 2: Completed monitor/native coordinates, DPI manifest, precise alpha hit testing, startup registry/instance event/recovery, screens/skins settings. First group actual WPF checks passed before world integration.

Task 3: Journal moved to DesktopPet.Journal/JournalTracker. Ruling: the service composes `_care.Profile with { Journal = Journal.Profile.Journal }`; no CareTracker mutation needed, preventing independent tracker snapshots from overwriting each other. Journal receives local-offset times; care/focus retain UTC. 83/83 isolated checks.

Task 4: Completed ball/frisbee motion/chase, three home themes/relative position, manual and night home sleep, journal/achievement UI and menu/tray connections. Built-in bichon frames reused; props use WPF vector drawing.

Task 5: Fresh read-only review found cancellation leaving automatic behavior suspended, home placement remaining on old monitor, and unequal PNG DPI hit mismatch. All fixed and regression-verified. Also fixed explicit disconnected-monitor primary fallback, corrupt preview error handling, early instance signal registration and synchronous world cancellation when opening the pet menu. The menu regression holds the next world tick while WPF dispatches actual Popup open/close events, exposing the timer race rather than relying on a sleep past it.

Task 6: Final evidence: Core 113 (`p4-final-core.log`), WPF 89 (`p4-final-windows.log`), skin 28 and journal 83 (`p4-all-checks.log` module sections). Total 313; the all-checks log's older Core/WPF totals are superseded. Migration checks exposed saves skipped when notification events had no listeners. Settings and profile now save unconditionally before optional notification; both red/green file readback regressions pass. Actual single 150% screen, synthetic monitor math/window fixture; mixed physical screens, real login and external-app mouse clicks remain hardware QA limitations.

Task 7: Version 0.4.0, P4 release output/ZIP, private-runtime/asset-hash/duplicate-IPC checks and user-state migration. Final publish evidence and exact package metadata are recorded in docs/2026-10-03-p4-verification.md.
