# Desktop Pet P7 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans task by task.

**Goal:** Give the reference bichon a consistent set of poses, smooth motion transitions, and a complete ground-based fetch loop.

**Architecture:** Core `FetchSequence` computes deterministic ground movement and fetch phases. WPF world applies snapshots and owns toy rendering; the animation manager consumes presentation transitions internally before returning to the requested clip.

**Tech Stack:** C#, .NET 10, WPF, built-in imagegen, Pillow for whole-frame packing and QA only.

**Spec:** `docs/superpowers/specs/2026-10-04-desktop-pet-p7-design.md`

## Global constraints

- Append enum values; preserve existing values, old skins and user data.
- Preserve P6 WalkLeft/Right and RunLeft/Right; no procedural body deformation or mirrored replacements.
- Separate agent-owned files and staged assets. Root is sole owner of shared builds and runtime installation.
- No Git repository: preserve clean source ZIP instead of commits/worktree.

## Review focus

- Cancellation in pickup, jump, carrying or dropping never awards credit or overwrites Dragging/Petting.
- Missing or corrupt new clips cannot strand a fetch phase or resume stale animation.
- Return uses initial X and selected ground plane, including negative-origin and very narrow work areas.
- Changed speed/visibility and direction during presentation transitions resolve to the newest requested action.
- Fetch challenge waits for complete delivery; normal home/follow behavior stays available.

### Task 1: Reference-consistent base assets

**Files:** Stage `artifacts/imagegen/p7/base/packaged/{Idle,Happy,Sleep,Petting,Feeding,Stretching,Yawning,Dragging,LookLeft,LookRight}`; create `scripts/prepare-p7-base-assets.py` and prompt manifest. Root installs after review.

- [x] Generate separate action atlases, 8 frames for Idle/Happy/Sleep, 6 for Petting/Feeding/Stretching/Yawning/Dragging, 3 for directional looks.
- [x] Pack 256 RGBA preserving alpha; inspect full-size and 160px contacts and GIFs for identity, action, clipping and continuity.

### Task 2: Transition and fetch assets

**Files:** Stage `artifacts/imagegen/p7/motion/packaged/{StartLeft,StartRight,StopLeft,StopRight,TurnLeft,TurnRight,JumpLeft,JumpRight,PickupLeft,PickupRight,CarryLeft,CarryRight,DropLeft,DropRight}`; own packer and prompt manifest.

- [x] Generate 4-frame direction-specific start/stop/turn/pickup/drop and 8-frame jump/carry actions.
- [x] Dog only: no baked toy. Return poses provide stable mouth attachment anchors. Root reviews generated contacts before installation.

### Task 3: Deterministic fetch engine

**Files:** Create `src/DesktopPet.Core/Services/FetchSequence.cs`, `tests/DesktopPet.OfflineChecks/P7Checks.cs`. Agent owns only these files; root registers tests.

**Interfaces:** `FetchSequence(ToyKind kind, PetPoint origin, PetSize petSize, WorkArea area, bool facingLeft, double speed, double animationSpeed = 1)`; properties `Phase`, `PetPosition`, `ToyPosition`, `ToyAngle`, `FacingLeft`, `Simulation`, `IsComplete`, `IsCancelled`; `Advance(double seconds)` and `Cancel()`. `FetchPhase`: Chasing, Jumping, PickingUp, Returning, Dropping, Celebrating, Complete, Cancelled.

- [x] Write deterministic checks for ground-only ball chase, accessible air catch, jump bounds, mouth attachment, return origin, drop persistence, cancellation, and tiny/negative-origin geometry.
- [x] Capture RED compilation/check evidence before implementing.
- [x] Implement bounded movement and finite phase durations, respecting speed multiplier. Complete only after drop/celebration.
- [x] Root runs GREEN offline checks when shared build slot is available.

### Task 4: WPF presentation and fetch integration

**Files:** Modify Core enums/state/animation definition/loader; `Animations/AnimationManager.cs`, `Views/PetWindow.xaml.cs`, `Services/PetWorldService.cs`, `Controllers/PetController.cs`; create `tests/DesktopPet.WindowsChecks/P7WindowsChecks.cs` and register checks.

- [x] Add failing native checks for transitions, interrupted queues and complete delivery before implementing.
- [x] Append jump/pickup/carry/drop states with directional fallbacks; movement transition completions remain internal to animation manager.
- [x] Apply engine snapshots each world tick; attach toy at mouth, expose fetch phase for status and diagnostics, award only after complete delivery.
- [x] Verify ordinary home/follow, challenge, visibility/drag/interaction interruption and old skin fallback.

### Task 5: Deliver portable P7

**Files:** Version/build/publish/verify scripts, README, asset provenance and verification document.

- [x] Install reviewed staged assets; clean stale copied files and run full offline + native WPF checks.
- [x] Independent review; address findings and repeat only affected checks.
- [x] Back up latest settings/profile/journal, stop verified owned P6 process, publish 0.7.0 to new folder/ZIP, verify source assets/ZIP/runtime/start and preserved user data.
- [x] Leave verified P7 application running and report final artifact paths and test results.

## Implementation notes

- Turn-v1 was rejected for clipping; Turn-v2 uses whole cells cut through irregular transparent gutters at one shared pixel scale. No body or alpha manipulation.
- Root recovered completed agent outputs and generated the remaining four base atlases plus replacement Turn-v2 after agent usage limits interrupted delivery.
- Full first run: 612 checks. Independent review had no Critical/Important findings. Two malformed-anchor regressions added; all 213 native Windows checks pass after the schema fix, bringing final coverage to 614.
- See docs/2026-10-04-p7-verification.md for actual artifacts, timing exception and validation limits.
- Published and startup-verified 0.7.0: 175 exact asset hashes, 668 ZIP entries, private runtime 10.0.12, unchanged P6 ZIP, preserved user data; live P7 PID 2632.
