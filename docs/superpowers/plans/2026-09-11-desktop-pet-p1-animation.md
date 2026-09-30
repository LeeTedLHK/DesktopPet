# DesktopPet P1 Animation Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the existing DesktopPet P0 from a static moving image to cached PNG sequence-frame Idle, Walk, Happy, and Sleep animations while preserving all P0 behavior.

**Architecture:** Keep `DesktopPet.Core` free of WPF and make it own animation kinds, state mapping, clip metadata, natural sorting, and filesystem fallback resolution. Add a WPF `AnimationManager` that decodes and freezes resolved files once, caches `BitmapImage` instances, advances one `DispatcherTimer`, and updates the existing `PetWindow` Image through a small callback. Extend the existing state machine/controller/behavior flow only where necessary for Happy and Sleeping.

**Tech Stack:** C# / .NET 10, WPF `DispatcherTimer` and `BitmapImage`, existing WinForms tray integration, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-11-desktop-pet-p1-animation-design.md`

## Global Constraints

- Preserve the existing transparent WPF window, drag/click/right-click distinction, tray, settings, position persistence, single instance, logging, and clean exit.
- Use PNG sequence frames under `src/DesktopPet/Assets/Pet/{Idle,Walk,Happy,Sleep}` and retain `pet.jpg` as the fallback image.
- Never load an image from disk on every frame; freeze and cache decoded images by absolute path.
- Use `DispatcherTimer`; do not add `Thread.Sleep`, `while(true)`, or a busy UI loop.
- Missing/empty/corrupt animation resources and malformed/old settings must fall back without crashing.
- Keep production dependencies unchanged; do not add AI, networking, database, third-party UI, or WPF UI automation.
- Run restore, build, test, and publish using the repository's local .NET SDK setup.

---

### Task 1: Add Core animation contracts, natural sorting, and fallback loader

**Files:**
- Create: `src/DesktopPet.Core/Animations/AnimationKind.cs`
- Create: `src/DesktopPet.Core/Animations/AnimationPlayback.cs`
- Create: `src/DesktopPet.Core/Animations/AnimationFrame.cs`
- Create: `src/DesktopPet.Core/Animations/AnimationClip.cs`
- Create: `src/DesktopPet.Core/Animations/AnimationDefinition.cs`
- Create: `src/DesktopPet.Core/Animations/NaturalSort.cs`
- Create: `src/DesktopPet.Core/Animations/AnimationLoader.cs`
- Create: `tests/DesktopPet.Tests/NaturalSortTests.cs`
- Create: `tests/DesktopPet.Tests/AnimationLoaderTests.cs`
- Create: `tests/DesktopPet.Tests/AnimationDefinitionTests.cs`

**Interfaces:**
- `AnimationKind` values: `Idle`, `Walk`, `WalkLeft`, `WalkRight`, `Happy`, `Sleep`; `Walk` is the generic fallback.
- `AnimationPlayback` values: `Loop`, `Once`.
- `AnimationFrame` is an immutable record containing `string Path` and `int Index`.
- `AnimationClip` exposes `AnimationKind Kind`, `IReadOnlyList<AnimationFrame> Frames`, `double FramesPerSecond`, `AnimationPlayback Playback`, `bool IsFallback`, and a calculated `TimeSpan FrameInterval`.
- `AnimationDefinition.ForState(PetState)` maps Idle/Dragging/Paused to Idle, WalkingLeft to WalkLeft, WalkingRight to WalkRight, Happy to Happy, and Sleeping to Sleep; `CreateClip` supplies 5/10/9/4 FPS and Happy Once, all others Loop.
- `NaturalSort.Order(IEnumerable<string>)` returns a materialized list ordered by numeric filename chunks (`walk_2` before `walk_10`) while ignoring case.
- `AnimationLoader(string assetRoot, Action<string>? log = null).Load()` returns one clip per `AnimationKind`, scanning only top-level `.png` files in each action directory and applying `WalkLeft/WalkRight → Walk → Idle → fallback`, `Walk/Happy/Sleep → Idle → fallback`, and `Idle → fallback`.

- [ ] **Step 1: Write the failing tests**

  Add assertions for `walk_1`, `walk_2`, `walk_10` order; clip metadata and state mapping; direct PNG selection; ignored `.jpg`/subdirectory files; missing action fallback to Idle; missing Idle fallback to `pet.jpg`; and empty directories producing a safe clip with no exception.

- [ ] **Step 2: Run the focused tests to verify they fail for missing production types**

  Run: `. ./scripts/use-dotnet.ps1; dotnet test tests/DesktopPet.Tests/DesktopPet.Tests.csproj --filter "FullyQualifiedName~NaturalSortTests|FullyQualifiedName~AnimationLoaderTests|FullyQualifiedName~AnimationDefinitionTests"`

  Expected: compilation failure because the Core animation contracts do not exist yet.

- [ ] **Step 3: Implement the minimal Core contracts and loader**

  Use immutable records, `Directory.EnumerateFiles` guarded by expected IO/security exceptions, `Path.GetExtension(...).Equals(".png", OrdinalIgnoreCase)`, and a natural comparer that compares digit runs numerically. Resolve fallback files from `assetRoot/pet.jpg` without assuming the process working directory. Log skipped directories/files and return empty clips only when even the default image is unavailable.

- [ ] **Step 4: Run the focused tests to verify they pass**

  Run the same focused `dotnet test` command. Expected: all new tests pass with no production warnings.

- [ ] **Step 5: Commit the Core animation slice when repository permissions allow**

  Run: `git add src/DesktopPet.Core/Animations tests/DesktopPet.Tests/NaturalSortTests.cs tests/DesktopPet.Tests/AnimationLoaderTests.cs tests/DesktopPet.Tests/AnimationDefinitionTests.cs; git commit -m "feat: add core animation clips and fallback loading"`

### Task 2: Extend state machine and behavior policy for Happy and Sleeping

**Files:**
- Modify: `src/DesktopPet.Core/Models/PetState.cs`
- Modify: `src/DesktopPet.Core/Controllers/PetStateMachine.cs`
- Modify: `src/DesktopPet.Core/Behaviors/BehaviorPolicy.cs`
- Modify: `tests/DesktopPet.Tests/PetStateMachineTests.cs`
- Modify: `tests/DesktopPet.Tests/BehaviorPolicyTests.cs`

**Interfaces:**
- Add `PetState.Happy` and `PetState.Sleeping`.
- Add `PetStateMachine.TryBeginHappy()`, `CompleteHappy()`, `TryBeginSleeping()`, and `CompleteSleeping()`; each only enters from visible, enabled Idle and returns to Idle/Paused according to existing visibility and auto-behavior preferences.
- Add `BehaviorPolicy.NextAction(Random)` as an existing three-action-compatible API and a new `BehaviorPolicy.NextIdleAction(Random)` that includes a small Sleeping probability; keep movement distance/speed contracts unchanged.

- [ ] **Step 1: Write failing state/policy tests**

  Cover Happy and Sleeping only starting from Idle, completion returning to Idle, hidden/paused/dragging rejection, dragging deferring the auto-behavior preference, and the new policy returning only Idle/WalkingLeft/WalkingRight/Sleeping across a deterministic sample.

- [ ] **Step 2: Run focused tests and observe the expected failures**

  Run: `. ./scripts/use-dotnet.ps1; dotnet test tests/DesktopPet.Tests/DesktopPet.Tests.csproj --filter "FullyQualifiedName~PetStateMachineTests|FullyQualifiedName~BehaviorPolicyTests"`

- [ ] **Step 3: Implement the smallest state/policy changes**

  Preserve existing transition semantics and make `CompleteHappy`/`CompleteSleeping` no-ops outside their matching state. Use a weighted `Random.Next(100)` branch with a low Sleeping chance while leaving the existing public `NextAction` behavior stable for old tests.

- [ ] **Step 4: Run all Core tests**

  Run: `. ./scripts/use-dotnet.ps1; dotnet test tests/DesktopPet.Tests/DesktopPet.Tests.csproj`

- [ ] **Step 5: Commit the state/policy slice when possible**

  Run: `git add src/DesktopPet.Core/Models/PetState.cs src/DesktopPet.Core/Controllers/PetStateMachine.cs src/DesktopPet.Core/Behaviors/BehaviorPolicy.cs tests/DesktopPet.Tests; git commit -m "feat: add happy and sleeping pet states"`

### Task 3: Add animation settings with backward-compatible normalization

**Files:**
- Modify: `src/DesktopPet.Core/Models/AppSettings.cs`
- Modify: `tests/DesktopPet.Tests/AppSettingsTests.cs`
- Modify: `tests/DesktopPet.Tests/SettingsServiceTests.cs`

**Interfaces:**
- `AppSettings.EnableAnimations` defaults to `true`.
- `AppSettings.AnimationSpeedMultiplier` defaults to `1.0` and `Normalize()` clamps finite values to `0.25–3.0`, using `1.0` for NaN/infinity.

- [ ] **Step 1: Add failing default, normalization, and legacy JSON tests**

  Deserialize a JSON document containing only the P0 fields and assert new defaults; assert finite bounds and non-finite fallback; serialize/deserialize new fields through the real `SettingsService`.

- [ ] **Step 2: Run the focused tests and verify the missing-field failures**

  Run: `. ./scripts/use-dotnet.ps1; dotnet test tests/DesktopPet.Tests/DesktopPet.Tests.csproj --filter "FullyQualifiedName~AppSettingsTests|FullyQualifiedName~SettingsServiceTests"`

- [ ] **Step 3: Implement properties and normalization**

  Keep the existing JSON options, atomic save, corrupt backup, and case-insensitive deserialization unchanged.

- [ ] **Step 4: Run all Core tests**

  Run: `. ./scripts/use-dotnet.ps1; dotnet test tests/DesktopPet.Tests/DesktopPet.Tests.csproj`

- [ ] **Step 5: Commit settings changes when possible**

  Run: `git add src/DesktopPet.Core/Models/AppSettings.cs tests/DesktopPet.Tests/AppSettingsTests.cs tests/DesktopPet.Tests/SettingsServiceTests.cs; git commit -m "feat: add animation settings"`

### Task 4: Package assets and implement the WPF AnimationManager

**Files:**
- Create: `src/DesktopPet/Animations/AnimationManager.cs`
- Modify: `src/DesktopPet/DesktopPet.csproj`
- Modify: `src/DesktopPet/Views/PetWindow.xaml.cs`

**Interfaces:**
- `AnimationManager` is constructed with `Dispatcher`, `ImageSource` frame sink, asset root, fallback source, logger, and a `Func<double>` speed multiplier.
- `SetState(PetState state, bool visible, bool enabled)` resolves the Core clip, leaves authored directional frames unmirrored, and avoids resetting an identical clip.
- `Stop(bool showStableFrame = true)` stops the timer and leaves the current cached frame visible.
- `Dispose()` stops the timer, unhooks events, and releases the manager-owned timer/cache references.
- `PlaybackCompleted` is an event carrying the completed `AnimationKind`.

- [ ] **Step 1: Add the resource packaging change**

  Replace the single `Resource Include` with explicit `Content Include="Assets/Pet/pet.jpg"` and `Content Include="Assets/Pet/**/*.png"`, both `CopyToOutputDirectory="PreserveNewest"` and `CopyToPublishDirectory="PreserveNewest"`, so editable folders are available beside the EXE in build and publish output.

- [ ] **Step 2: Implement manager loading and caching**

  Use `AnimationLoader(AppContext.BaseDirectory + "Assets/Pet")`; load each resolved path via `BitmapImage` with `CacheOption=OnLoad`, freeze it, cache by normalized full path, catch image decode/IO exceptions per frame, and use the fallback image cache if a clip has no decodable frames. Configure a single DispatcherTimer; interval is `clip.FrameInterval / multiplier` bounded to a safe minimum. On Once completion, show the last frame, stop, and raise `PlaybackCompleted` exactly once.

- [ ] **Step 3: Keep PetWindow sizing stable and delegate frame changes**

  Initialize the manager after `InitializeComponent`, use the fallback image as the initial source, and make `SetPetScale` derive dimensions from a stable `BitmapSource` rather than whatever transient frame is visible. Expose `UpdateAnimation` as the existing public entry point but let it delegate to the manager plus the existing breathing/walking transforms. Add a `SetAnimationsEnabled`/speed update path and stop manager playback when hidden or dragging.

- [ ] **Step 4: Build the WPF project**

  Run: `. ./scripts/use-dotnet.ps1; dotnet build src/DesktopPet/DesktopPet.csproj -c Debug`

  Expected: successful WPF compilation and copied animation directories under the output directory.

- [ ] **Step 5: Commit WPF manager/assets when possible**

  Run: `git add src/DesktopPet/Animations src/DesktopPet/DesktopPet.csproj src/DesktopPet/Views/PetWindow.xaml.cs; git commit -m "feat: add cached WPF sequence frame playback"`

### Task 5: Wire clicks, states, movement direction, pause, and sleep into animation playback

**Files:**
- Modify: `src/DesktopPet/Controllers/PetController.cs`
- Modify: `src/DesktopPet/Behaviors/BehaviorManager.cs`
- Modify: `src/DesktopPet/Views/PetWindow.xaml.cs`
- Modify: `src/DesktopPet/Views/PetWindow.xaml`

**Interfaces:**
- `PetController` handles `AnimationManager.PlaybackCompleted`: Happy completion calls `CompleteHappy`; Sleeping is ended by the behavior timer; no click is fired after a drag.
- `BehaviorManager` raises/uses the existing state machine transitions, starts a sleep timer for a bounded several-second duration, and stops all timers on Suspend/Dispose.

- [ ] **Step 1: Add a controller-level click regression test if a pure seam is available**

  Otherwise retain the existing Core click/state tests and validate the WPF integration by build and manual launch; do not add brittle WPF automation.

- [ ] **Step 2: Implement click-to-Happy without changing drag detection**

  In the existing non-drag click branch call `TryBeginHappy()` before `PlayClickFeedback()`. Resume automatic behavior only after Happy completes or when the existing click path is otherwise idle; right-click menu handlers remain unchanged.

- [ ] **Step 3: Implement movement-to-Walk and stop-to-Idle synchronization**

  Keep `TryBeginWalk` and `CompleteWalking` as the source of state transitions; `PetController.OnStateChanged` already drives `PetWindow.UpdateAnimation`, and `BehaviorManager.StopTimers` must not leave a walking state behind.

- [ ] **Step 4: Implement low-probability sleep behavior**

  On an Idle decision, select Sleeping through `NextIdleAction`; call `TryBeginSleeping`, stop decision timer, start a DispatcherTimer for 5–12 seconds, and complete sleeping back to Idle before rescheduling. If animation playback is a fallback clip, state behavior still remains safe and bounded.

- [ ] **Step 5: Verify pause, hide, drag, and movement semantics**

  Run the full test suite and build. Inspect that Suspend stops decision/movement/sleep timers, Dragging stops frame progression, and setting `AutoBehaviorEnabled=false` transitions to Paused after any temporary state completes.

- [ ] **Step 6: Commit controller/behavior integration when possible**

  Run: `git add src/DesktopPet/Controllers/PetController.cs src/DesktopPet/Behaviors/BehaviorManager.cs src/DesktopPet/Views/PetWindow.xaml src/DesktopPet/Views/PetWindow.xaml.cs; git commit -m "feat: connect pet states to sequence animations"`

### Task 6: Add the light settings UI and update documentation

**Files:**
- Modify: `src/DesktopPet/Views/SettingsWindow.xaml`
- Modify: `src/DesktopPet/Views/SettingsWindow.xaml.cs`
- Modify: `README.md`
- Modify: `docs/verification.md`

**Interfaces:**
- Settings UI exposes `EnableAnimations` and `AnimationSpeedMultiplier` without changing existing control semantics.
- README documents actual resource locations, natural filename ordering, transparent PNG guidance, recommended dimensions/frame counts, enable/disable behavior, fallback rules, build/test/publish commands, and only real limitations.

- [ ] **Step 1: Add settings controls**

  Add one CheckBox and one small ComboBox/Slider; load from `AppSettings`, include both values in `ApplyChanges`, and preserve the existing `_loading` guard and immediate save flow.

- [ ] **Step 2: Connect settings to PetWindow**

  `PetController.ApplySettings` passes both settings to the window/manager while retaining scale, topmost, behavior, and position handling.

- [ ] **Step 3: Replace P0 README animation claims with P1 documentation**

  Explain that `Assets/Pet` is copied beside the executable, list the four directories and `pet.jpg`, document zero/one/many frame behavior and fallback chain, and update known limitations to mention no pixel hit testing, main-display movement, and no signed installer.

- [ ] **Step 4: Build and inspect documentation paths**

  Run: `. ./scripts/use-dotnet.ps1; dotnet build DesktopPet.sln -c Release`

### Task 7: Full verification and delivery artifacts

**Files:**
- Modify: `docs/verification.md` with observed P1 commands/results and GUI limitations.
- Generated: `artifacts/publish/win-x64/`.

- [ ] **Step 1: Restore**

  Run: `. ./scripts/use-dotnet.ps1; dotnet restore DesktopPet.sln`

- [ ] **Step 2: Build**

  Run: `. ./scripts/use-dotnet.ps1; dotnet build DesktopPet.sln -c Release --no-restore`

- [ ] **Step 3: Test**

  Run: `. ./scripts/use-dotnet.ps1; dotnet test DesktopPet.sln -c Release --no-build`

  Record the exact passed/failed/skipped counts from output.

- [ ] **Step 4: Publish**

  Run: `. ./scripts/use-dotnet.ps1; ./scripts/publish.ps1`

  Verify `artifacts/publish/win-x64/DesktopPet.exe` exists and the four animation directories plus fallback image are present in the publish output.

- [ ] **Step 5: Launch the published EXE and inspect runtime evidence**

  Start the EXE from its absolute path, confirm the process remains responsive, confirm a second launch exits as the existing single-instance behavior requires, inspect logs for resource-loading errors, and close the test process through the app's normal exit path where GUI access permits.

- [ ] **Step 6: Run a final requirements and diff review**

  Run `git diff --stat`, `git diff --check`, `rg -n "Thread\.Sleep|while\s*\(" src tests`, and inspect the final README/verification report. Report any GUI interaction that could not be performed instead of inferring it from process survival.
