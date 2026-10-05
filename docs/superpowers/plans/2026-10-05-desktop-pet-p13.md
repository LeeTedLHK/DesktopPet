# DesktopPet P13 Implementation Plan

> For agentic workers: use superpowers:executing-plans for integration and dispatching-parallel-agents for the two disjoint implementation domains below.

**Goal:** Complete the four previously proposed improvements: reference-faithful feeding choreography, pet-relative toys, independent prop sizes, and optional idle/sleep power saving.

**Architecture:** Existing Core geometry remains deterministic; WPF world uses actual fitted pet dimensions for prop sizes. Existing animation manager receives a live power-saving callback. Settings additions are backward compatible. Six feeding frames retain the existing 256px format and ground baseline.

**Tech stack:** C#/.NET 10, WPF, offline checks, native Windows integration checks, built-in imagegen and mechanical raster packing.

**Spec / global constraints:** User has explicitly authorized continuing improvements without waiting for approval. Preserve original reference photos, unrelated animations, saved data, and all prior releases. Source snapshot: artifacts/verification/p13-source-clean-before.zip. No Git repository, no commits. Root alone runs shared builds/tests. Never edit the live profile to manufacture validation.

## Contracts

- AppSettings: BallScale, FrisbeeScale, FeedingBowlScale default 1; Normalize finite values to 0.6–1.5 with invalid values falling back to 1. PowerSavingEnabled defaults false.
- ToySimulation retains Size=32 for compatibility; add Diameter and optional final constructor argument diameter=Size, bounded to its physics area. FetchSequence adds optional final toyDiameter=ToySimulation.Size and uses its simulation Diameter everywhere.
- World computes toy diameter = 32 * (actual pet width - 8) / 160 * per-kind scale, bounded to activity area/ground physics area. Bowl keeps width = actual pet width * .30 * FeedingBowlScale and its current aspect ratio.
- AnimationManager adds optional final Func<bool> powerSavingEnabled callback. Only Idle and Sleep loops are capped at 4 and 2 fps respectively. Hidden/static/disposed timers stay stopped; all interaction timing and focus timers retain their behavior.
- Settings UI names: BallScaleSlider / BallScaleLabel, FrisbeeScaleSlider / FrisbeeScaleLabel, FeedingBowlScaleSlider / FeedingBowlScaleLabel, PowerSavingCheck. Sliders expose 60–150% with 5% steps and load/save without spurious edits.

## Tasks

1. Core domain (independent worker): AppSettings.cs, ToySimulation.cs, FetchSequence.cs, new P13 offline checks and registration in OfflineChecks/Program.cs. Verify default/invalid settings, JSON compatibility, diameter-aware collision/attachment/settling, fetch completion across pet sizes and small/offset areas. Tests before implementation where meaningful; root executes checks.
2. UI domain (independent worker): SettingsWindow.xaml and .xaml.cs only. Add three live percentage sliders and optional power-saving checkbox within existing scrollable tabs, accessible names, preservation of unrelated preferences. Root supplies Core contracts and tests actual controls.
3. Root: PetWorldService.cs, AnimationManager.cs, PetWindow.xaml.cs, PetController.cs, new P13 native integration checks. Update all hardcoded prop dimensions, rotate centers and mouth offsets. Keep feeding bowl anchored in X rather than following chewing noise. Validate live resize, no-clipping physics/attachments, hidden/static/rest/action animation cadence, skin reload and UI roundtrip.
4. Root assets: inspect both originals and existing feeding style; generate one six-cell transparent feeding sequence (approach/lower/eat/chew/lift/lick), package whole cells uniformly to six 256px PNGs with feet at y228. Inspect contact sheet, composite bowl alignment and 160px animation. Record exact prompt and generation mode in docs/assets. Update feeding anchors if needed; no unrelated raster modifications.
5. Integration: run focused red/green checks, full offline/skin/journal/native Windows checks; independent review, fix material findings. Version 0.9.4; publish artifacts/publish/p13-win-x64 and DesktopPet-P13-win-x64.zip. Preserve P6–P12 ZIP hashes, verify packaged assets/runtime/startup and live data continuity from fresh snapshots. Update README and verification report, start the release and confirm responding.

**Review focus:** Geometry must use the same instance diameter for visuals, physics, catches and drops; sliders must not lose existing settings. Power saving must affect only restful loop intervals and survive skin switches. Feeding identity/foot placement and bowl contact are visually reviewed; original photos and other actions stay byte-identical. Report actual commands/results and any limits.

## Execution ledger

- Core domain complete: meaningful RED observed before production edits; 121 P13 Core checks pass and are included in 428 full offline checks.
- UI domain complete: native loading/event/value-preservation probes pass; independent worker edited only the two SettingsWindow files.
- Root integration complete: same physical pixel diameter used for toys; real 80%/200% left/right ball/frisbee fetches complete. Rest-only cadence and skin replacement checked. Initial native RED, focused GREEN41, full Windows GREEN356 recorded.
- Assets complete: built-in image_gen with the two inspected originals; six intact generated cells packed/reviewed and installed, foot baseline228; 168 other assets unchanged. Exact prompt and mechanical packing script retained.
- Review complete: no unresolved Critical/Important findings. Found fractional-DPI ceiling issue, observed corrected boundary RED, fixed epsilon and reran focused/full GREEN. Actual monitor150%;175% verified at the sizing-helper boundary.
- Delivery complete: version0.9.4, ZIP and private runtime verified; PID7212 responds. Real 48 old diary entries preserved, one genuine startup Home entry verified; configuration/new defaults and normal elapsed-time care validated. P6–P12 ZIP hashes unchanged.
- No Git or worktree was available; the retained source ZIP and source-diff manifest provide the baseline. No live data restoration or manual overwrite occurred. Details and remaining validation limits are in docs/2026-10-05-p13-verification.md.
