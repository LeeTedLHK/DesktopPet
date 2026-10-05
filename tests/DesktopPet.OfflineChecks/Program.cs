using System.Text.Json;
using DesktopPet.Animations;
using DesktopPet.Behaviors;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    passed++;
    Console.WriteLine("PASS: " + name);
}

if (args.Contains("--p17-only"))
{
    P17MotionChecks.Run(Check);
    Console.WriteLine($"{passed} P17 motion checks passed.");
    return;
}
if (args.Contains("--p15-focus-only"))
{
    P15FocusChecks.Run(Check);
    Console.WriteLine($"{passed} P15 focus checks passed.");
    return;
}

if (args.Contains("--p15-journal-only"))
{
    P15JournalQueryChecks.Run(Check);
    Console.WriteLine($"{passed} P15 journal-query checks passed.");
    return;
}

if (args.Contains("--p14-reminders-only"))
{
    P14ReminderChecks.Run(Check);
    Console.WriteLine($"{passed} P14 reminder checks passed.");
    return;
}
if (args.Contains("--p14-diary-only"))
{
    P14JournalExportChecks.Run(Check);
    Console.WriteLine($"{passed} P14 diary checks passed.");
    return;
}

if (args.Contains("--p9-names-only"))
{
    P9NameChecks.Run(Check);
    Console.WriteLine($"{passed} P9 name checks passed.");
    return;
}

if (args.Contains("--p13-core-only"))
{
    P13Checks.Run(Check);
    Console.WriteLine($"{passed} P13 Core checks passed.");
    return;
}

var visibility = new PetVisibility();
visibility.RequestVisible(true);
Check(visibility.ShouldShow, "manual show");
visibility.SetFullscreenSuppressed(true);
Check(!visibility.ShouldShow && visibility.RequestedVisible, "fullscreen keeps user intent");
visibility.RequestVisible(false);
visibility.SetFullscreenSuppressed(false);
Check(!visibility.ShouldShow, "manual hide survives fullscreen exit");
visibility.RequestVisible(true);
visibility.SetFullscreenSuppressed(true);
visibility.SetFullscreenSuppressed(false);
Check(visibility.ShouldShow, "fullscreen exit restores desired visibility");
visibility.RequestVisible(false);
visibility.SetFullscreenSuppressed(true);
visibility.RequestVisible(true);
Check(!visibility.ShouldShow, "manual show obeys fullscreen suppression");

foreach (var hour in new[] { 22, 23, 0, 6 })
    Check(CompanionPolicy.IsSleepTime(hour, 22, 7), "night hour " + hour);
foreach (var hour in new[] { 7, 12, 21 })
    Check(!CompanionPolicy.IsSleepTime(hour, 22, 7), "awake hour " + hour);
Check(CompanionPolicy.IsSleepTime(13, 12, 15), "non-wrapping sleep interval");
Check(!CompanionPolicy.IsSleepTime(12, 12, 12), "equal sleep hours disable interval");
Check(!CompanionPolicy.IsSleepTime(-1, 22, 7), "invalid hours are safe");
var monitor = new WorkArea(-1920, 0, 1920, 1080);
Check(CompanionPolicy.IsFullscreen(monitor, monitor), "negative-coordinate monitor fullscreen");
Check(!CompanionPolicy.IsFullscreen(new(-1920, 0, 1920, 1040), monitor), "maximized work area is not fullscreen");
Check(!CompanionPolicy.IsFullscreen(new(0, 0, 0, 0), monitor), "empty window is not fullscreen");
Check(CompanionPolicy.LookDirection(new(100, 100), new(0, 100)) == PetState.LookingLeft, "look left");
Check(CompanionPolicy.LookDirection(new(100, 100), new(200, 100)) == PetState.LookingRight, "look right");
Check(CompanionPolicy.LookDirection(new(100, 100), new(1000, 100)) is null, "ignore distant pointer");

foreach (var action in new[] { PetState.Petting, PetState.Feeding, PetState.Happy })
{
    var machine = new PetStateMachine(false);
    Check(machine.TryBeginInteraction(action), "interaction while paused: " + action);
    machine.CompleteTemporaryAction(action);
    Check(machine.State == PetState.Paused, "interaction returns to paused: " + action);
    machine.SetVisible(false);
    Check(!machine.TryBeginInteraction(action), "hidden interaction rejected: " + action);
}
var state = new PetStateMachine();
state.TryBeginSleeping();
Check(state.TryBeginInteraction(PetState.Petting), "petting wakes sleeping dog");
Check(!state.TryBeginInteraction(PetState.Idle), "reject invalid interaction");
state.BeginDrag();
Check(!state.TryBeginInteraction(PetState.Feeding), "drag blocks feeding");
state.CompleteTemporaryAction(PetState.Petting);
Check(state.State == PetState.Dragging, "stale completion cannot cancel dragging");
state.EndDrag();
Check(state.TryBeginNaturalAction(PetState.Stretching), "natural action starts from idle");
Check(!state.TryBeginNaturalAction(PetState.Yawning), "natural actions do not overlap");
state.CompleteTemporaryAction(PetState.Stretching);
Check(state.State == PetState.Idle, "natural action completes");
state.TryBeginNaturalAction(PetState.LookingLeft);
state.CancelNaturalAction();
Check(state.State == PetState.Idle, "suspending cancels observation");
state.SetAutoBehaviorEnabled(false);
Check(!state.TryBeginNaturalAction(PetState.Yawning), "paused blocks autonomous action");

foreach (var (petState, animation) in new[] {
    (PetState.Petting, AnimationKind.Petting), (PetState.Feeding, AnimationKind.Feeding),
    (PetState.Stretching, AnimationKind.Stretching), (PetState.Yawning, AnimationKind.Yawning),
    (PetState.Dragging, AnimationKind.Dragging), (PetState.LookingLeft, AnimationKind.LookLeft),
    (PetState.LookingRight, AnimationKind.LookRight) })
    Check(AnimationDefinition.ForState(petState) == animation, "animation mapping: " + petState);
foreach (var kind in new[] { AnimationKind.Petting, AnimationKind.Feeding, AnimationKind.Stretching, AnimationKind.Yawning })
    Check(AnimationDefinition.CreateClip(kind, []).Playback == AnimationPlayback.Once, "one-shot action: " + kind);

var settings = JsonSerializer.Deserialize<AppSettings>("{\"PetScale\":1.5}")!;
Check(settings.FullscreenAutoHide && !settings.ClickThrough && settings.NightSleepEnabled, "legacy JSON receives defaults");
settings = (settings with { NightSleepStartHour = -1, NightSleepEndHour = 99 }).Normalize();
Check(settings.NightSleepStartHour == 22 && settings.NightSleepEndHour == 7, "invalid sleep times normalized");
var roundTrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings with { ClickThrough = true }))!;
Check(roundTrip.ClickThrough && roundTrip.SpeechBubblesEnabled, "new settings round trip");
var root = Path.Combine(Path.GetTempPath(), "DesktopPet.P2." + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(Path.Combine(root, "Idle"));
    File.WriteAllText(Path.Combine(root, "Idle", "idle_1.png"), "test");
    var clips = new AnimationLoader(root).Load();
    Check(clips.Count == Enum.GetValues<AnimationKind>().Length, "all new animation kinds load");
    Check(clips[AnimationKind.Feeding].IsFallback && clips[AnimationKind.Feeding].Playback == AnimationPlayback.Once,
        "missing new asset retains completion and fallback");
}
finally { Directory.Delete(root, true); }
P3Checks.Run(Check);
P4Checks.Run(Check);
P6Checks.Run(Check);
P7AnimationChecks.Run(Check);
P7Checks.Run(Check);
P8DataChecks.Run(Check);
P9NameChecks.Run(Check);
P13Checks.Run(Check);
P14ReminderChecks.Run(Check);
P14JournalExportChecks.Run(Check);
P15FocusChecks.Run(Check);
P15JournalQueryChecks.Run(Check);
P17MotionChecks.Run(Check);
Console.WriteLine($"{passed} checks passed.");
