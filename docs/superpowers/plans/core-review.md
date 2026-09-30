# Core independent review

Date: 2026-09-11

Reviewed the completed current filesystem under `src/DesktopPet.Core` and `tests/DesktopPet.Tests` against `docs/superpowers/plans/core-brief.md`. This was a bounded static review; no application or test code was changed, and the unchanged tests were not rerun. The primary agent separately reported a successful Release build with zero warnings/errors and all 51 tests passing.

## Actionable findings

No substantive correctness defects or contract violations identified.

## Coverage

- `AppSettings`: required mutable record properties and defaults, cloned normalization, finite scale handling, inclusive scale bounds, enum validation, and all-or-nothing coordinate pairs.
- `PetStateMachine`: valid walk targets and visible/enabled/idle preconditions, preference changes during dragging, hiding during walking, drag completion, protection against stale walking completion, and suppression of duplicate state events.
- `ScreenGeometry`: negative work-area origins, full-window clamping, oversized-window alignment, default margins, partial-intersection restore versus fully offscreen restore, exact edges, and sanitization of nonfinite inputs.
- `BehaviorPolicy`: inclusive decision-wait ranges, bounded distances, approximately uniform action selection, fixed speed mapping, and seeded range/distribution tests.
- `SettingsService`: readable enum serialization, normalized load/save, missing-file defaults, malformed/null JSON backup and recovery, backup-error containment, unique same-directory temporary files, flush before replacement, failed-write preservation of the existing configuration, and temporary-file cleanup.
- `AppLogger`: process-wide lock around appending, daily filenames, exception details, closed file handles after each write, and best-effort failure containment.
- Test isolation and project structure: unique temporary directories and cleanup; real persistence I/O; no user configuration writes; net10.0 Core and test targets; only the requested test-package dependencies; no production NuGet dependencies in Core.

The review evaluated error branches directly from source as well as the provided tests. It does not represent new runtime verification and does not infer WPF interaction coverage from the pure Core tests.
