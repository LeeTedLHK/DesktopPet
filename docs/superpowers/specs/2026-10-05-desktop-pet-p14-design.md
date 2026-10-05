# DesktopPet P14 design

The user wants useful improvements implemented together without another approval round. Preserve the reference-faithful dog and existing saved preferences/diary. This release improves existing flows; no new generated art, dependencies or remote services.

## Gameplay continuity

Neutral settings (name, topmost, speech, care/journal, reminders, focus, power saving and prop-size preferences) must retain the current world session and challenge progress. Ball/frisbee size changes apply to the next throw; an active feeding bowl still resizes immediately. Geometry, selected skin, animation speed, movement speed, automatic behavior, home-target changes and disabling active toys cancel a game consistently. Hidden/fullscreen/dragging/explicit Stop still end play.

Opening either menu pauses the world motion clock and movement/fetch animations without clearing the session. Both menus must be closed before resuming. Thirty-second cursor-follow time excludes menu pauses. Challenge pauses preserve caught count and a pending next round; no callbacks/rewards occur merely from opening/closing a menu. Sleeping/napping behavior from P12 remains intact.

## Focus and reminders

New AppSettings.FocusQuietEnabled defaults false. During the running Focus phase (not paused, Break, Completed or Stopped), this option suspends automatic movement/attention behavior and health reminders; explicit pet interactions and manual games remain available. Existing sleep is preserved. Resuming normal behavior respects open menus, manual sessions and visibility.

Health reminders can be snoozed for15/30/60 minutes, or resumed early. Snooze affects Water/Move/Rest only, clears their pending notices, never changes Focus/Break timing or notices, and never pulls a previously later reminder forward. Snooze survives unrelated preference edits during the session and clears on restart. Focus quiet suppresses health notices without accumulating a burst. UI shows a local resume time and makes this state easy to clear.

## Diary export and quick access

Export the current normalized journal/care snapshot to a human-readable UTF-8 text file with pet name, generated timestamp, companion totals and chronological entries. Export uses the existing SaveFileDialog style and a temporary file followed by replacement; failed export retains an existing target. The live profile is untouched. UI provides the entry count/status without changing existing ZIP backup behavior.

Tray adds an explicit summon action placing the shown pet near the cursor within the allowed activity area. If cross-monitor dragging is allowed, select the cursor monitor; otherwise retain the selected monitor. Clamp the entire pet, preserve click-through and automatic-behavior preferences, save the actual position. Existing fullscreen visibility suppression remains in force.

Tray tooltip includes a safely bounded pet name, visibility/interaction state, and active focus/rest countdown or paused state. Respect NotifyIcon's63-character limit without splitting surrogate pairs or text elements; do not replace its icon or menu entries.

## Delivery and constraints

Version0.9.5/P14. No Git repository; retain source snapshot artifacts/verification/p14-source-clean-before.zip, previous releases and all175 pet assets byte-for-byte. Root owns serialized builds/tests. Tests use temporary data only; no artificial real profile edits. Validate focused RED/GREEN, full offline/journal/skin/native checks, independent review, package hashes, private-runtime startup and real-data continuity; start the release.
