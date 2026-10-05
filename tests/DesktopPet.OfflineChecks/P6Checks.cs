using DesktopPet.Animations;
using DesktopPet.Controllers;
using DesktopPet.Models;

internal static class P6Checks
{
    // Persisted enum values: existing values 0..12 must retain their meanings.
    private const PetState RunningLeft = (PetState)13;
    private const PetState RunningRight = (PetState)14;
    private const AnimationKind RunLeft = (AnimationKind)13;
    private const AnimationKind RunRight = (AnimationKind)14;

    public static void Run(Action<bool, string> check)
    {
        CheckMovement(check);
        CheckAnimationContracts(check);
        CheckLoader(check);
    }

    private static void CheckMovement(Action<bool, string> check)
    {
        foreach (var (direction, name) in new[] { (RunningLeft, "left"), (RunningRight, "right") })
        {
            foreach (var enabled in new[] { true, false })
            {
                var machine = new PetStateMachine(enabled);
                check(machine.TryBeginUserMovement(direction) && machine.State == direction,
                    $"P6 user run {name} starts with auto behavior {enabled}");
                machine.CompleteWalking();
                check(machine.State == (enabled ? PetState.Idle : PetState.Paused),
                    $"P6 run {name} completion restores auto behavior {enabled}");
            }

            var natural = new PetStateMachine();
            check(!natural.TryBeginWalk(direction) && natural.State == PetState.Idle,
                $"P6 autonomous walking rejects running {name}");
            check(natural.TryBeginWalk(PetState.WalkingLeft) && natural.State == PetState.WalkingLeft,
                $"P6 natural walking remains available after rejecting run {name}");

            var dragging = new PetStateMachine();
            dragging.TryBeginUserMovement(direction);
            dragging.BeginDrag();
            dragging.CompleteWalking();
            check(dragging.State == PetState.Dragging && !dragging.TryBeginUserMovement(direction),
                $"P6 dragging interrupts run {name} and rejects movement");
            dragging.SetAutoBehaviorEnabled(false);
            dragging.EndDrag();
            check(dragging.State == PetState.Paused, $"P6 run {name} drag release observes pause preference");

            var hidden = new PetStateMachine();
            hidden.TryBeginUserMovement(direction);
            hidden.SetVisible(false);
            hidden.CompleteWalking();
            check(hidden.State == PetState.Paused && !hidden.TryBeginUserMovement(direction),
                $"P6 hiding cancels run {name} and rejects movement");
            hidden.SetVisible(true);
            check(hidden.State == PetState.Idle, $"P6 showing after run {name} starts idle");

            var paused = new PetStateMachine();
            paused.TryBeginUserMovement(direction);
            paused.SetAutoBehaviorEnabled(false);
            paused.CompleteWalking();
            check(paused.State == PetState.Paused, $"P6 disabling behavior cancels run {name}");
            paused.SetAutoBehaviorEnabled(true);
            check(paused.State == PetState.Idle, $"P6 enabling behavior after run {name} starts idle");

            foreach (var interaction in new[] { PetState.Petting, PetState.Feeding, PetState.Happy })
            {
                var machine = new PetStateMachine();
                machine.TryBeginUserMovement(direction);
                check(machine.TryBeginInteraction(interaction), $"P6 {interaction} interrupts run {name}");
                machine.CompleteWalking();
                check(machine.State == interaction, $"P6 stale run {name} completion preserves {interaction}");
                machine.CompleteTemporaryAction(interaction);
                check(machine.State == PetState.Idle, $"P6 {interaction} completes after run {name}");
            }
        }

        var switching = new PetStateMachine(false);
        check(switching.TryBeginUserMovement(RunningLeft) && switching.TryBeginUserMovement(RunningRight)
            && switching.State == RunningRight, "P6 user chase reverses running direction while paused");
        check(switching.TryBeginUserMovement(PetState.WalkingRight) && switching.State == PetState.WalkingRight,
            "P6 user home/follow movement can replace running with walking");
        switching.CompleteWalking();
        check(switching.State == PetState.Paused, "P6 walking replacement completes back to paused");

        var unchanged = new PetStateMachine();
        var changes = new List<PetState>();
        unchanged.StateChanged += changes.Add;
        unchanged.TryBeginUserMovement(RunningLeft);
        unchanged.SetVisible(true);
        unchanged.SetAutoBehaviorEnabled(true);
        unchanged.TryBeginUserMovement(RunningLeft);
        check(unchanged.State == RunningLeft && changes.SequenceEqual([RunningLeft]),
            "P6 repeated direction and preferences keep the active running animation");
        check(!unchanged.TryBeginUserMovement(PetState.Idle) && unchanged.State == RunningLeft,
            "P6 user movement rejects nonmovement states without interrupting run");
        check(!unchanged.TryBeginNaturalAction(PetState.Yawning), "P6 natural actions cannot overlap running");
        unchanged.TryBeginUserSleeping();
        unchanged.CompleteWalking();
        check(unchanged.State == PetState.Sleeping, "P6 home sleep interrupts run and survives stale movement completion");
    }

    private static void CheckAnimationContracts(Action<bool, string> check)
    {
        foreach (var (state, kind, name) in new[] { (RunningLeft, RunLeft, "left"), (RunningRight, RunRight, "right") })
        {
            check(AnimationDefinition.ForState(state) == kind, $"P6 running {name} selects directional run animation");
            var clip = AnimationDefinition.CreateClip(kind, [new("frame.png", 0)]);
            check(clip.FramesPerSecond == 12 && clip.Playback == AnimationPlayback.Loop,
                $"P6 running {name} plays at 12 fps and loops");
            check(clip.FrameInterval == TimeSpan.FromSeconds(1d / 12), $"P6 running {name} has an 83 ms frame interval");
        }
        foreach (var (state, kind) in new[] { (PetState.WalkingLeft, AnimationKind.WalkLeft),
                     (PetState.WalkingRight, AnimationKind.WalkRight) })
        {
            var clip = AnimationDefinition.CreateClip(AnimationDefinition.ForState(state), []);
            var expectedFps = 15;
            check(clip.Kind == kind && clip.FramesPerSecond == expectedFps && clip.Playback == AnimationPlayback.Loop,
                $"P6 {state} keeps directional looping at {expectedFps} fps");
        }
        check(Enum.GetName(RunningLeft) == "RunningLeft" && Enum.GetName(RunningRight) == "RunningRight"
            && (int)PetState.LookingRight == 12 && (int)PetState.WalkingLeft == 1,
            "P6 running states append after existing persisted state values");
        check(Enum.GetName(RunLeft) == "RunLeft" && Enum.GetName(RunRight) == "RunRight"
            && (int)AnimationKind.LookRight == 12 && (int)AnimationKind.WalkLeft == 2,
            "P6 running animations append after existing animation values");
    }

    private static void CheckLoader(Action<bool, string> check)
    {
        foreach (var (kind, runFolder, walkFolder, oppositeWalk) in new[]
                 { (RunLeft, "RunLeft", "WalkLeft", "WalkRight"), (RunRight, "RunRight", "WalkRight", "WalkLeft") })
        {
            WithAssets(root =>
            {
                foreach (var number in new[] { 10, 2, 1, 4, 3, 6, 5, 7 }) WriteFrame(root, runFolder, $"run_{number}.png");
                WriteFrame(root, runFolder, "notes.txt");
                WriteFrame(root, walkFolder, "walk.png");
                WriteFrame(root, "Walk", "generic.png");
                WriteFrame(root, "Idle", "idle.png");
                var clip = new AnimationLoader(root).Load()[kind];
                check(!clip.IsFallback && clip.Kind == kind && clip.Frames.Select(f => Path.GetFileName(f.Path))
                    .SequenceEqual(["run_1.png", "run_2.png", "run_3.png", "run_4.png", "run_5.png", "run_6.png", "run_7.png", "run_10.png"]),
                    $"P6 {runFolder} loads eight authored frames in natural order before fallback");
            });

            foreach (var (fallbackFolder, frameName) in new[]
                     { (walkFolder, "directional.png"), ("Walk", "generic.png"), ("Idle", "idle.png"), ("", "pet.jpg") })
            {
                WithAssets(root =>
                {
                    WriteFrame(root, runFolder, "notes.txt");
                    WriteFrame(root, oppositeWalk, "opposite.png");
                    var expectedPath = WriteFrame(root, fallbackFolder, frameName);
                    if (fallbackFolder == walkFolder) WriteFrame(root, "Walk", "lower_priority.png");
                    if (fallbackFolder is not ("Idle" or "")) WriteFrame(root, "Idle", "lower_priority.png");
                    if (fallbackFolder != "") WriteFrame(root, "", "pet.jpg");
                    var clip = new AnimationLoader(root).Load()[kind];
                    check(clip.IsFallback && clip.Frames.Count == 1 && clip.Frames[0].Path == expectedPath,
                        $"P6 missing {runFolder} chooses {fallbackFolder + "/" + frameName} without inverting direction");
                    check(clip.Kind == kind && clip.FramesPerSecond == 12 && clip.Playback == AnimationPlayback.Loop,
                        $"P6 {runFolder} fallback retains requested running playback via {fallbackFolder + "/" + frameName}");
                });
            }

            WithAssets(root =>
            {
                var preferred = WriteFrame(root, "", "pet.png");
                WriteFrame(root, "", "pet.jpg");
                var clip = new AnimationLoader(root).Load()[kind];
                check(clip.IsFallback && clip.Frames.Count == 1 && clip.Frames[0].Path == preferred,
                    $"P6 missing {runFolder} prefers the transparent default image");
            });

            WithAssets(root =>
            {
                WriteFrame(root, oppositeWalk, "opposite.png");
                var clip = new AnimationLoader(root).Load()[kind];
                check(clip.IsFallback && clip.Frames.Count == 0,
                    $"P6 missing {runFolder} returns safe empty fallback rather than opposite direction");
            });
        }
    }

    private static string WriteFrame(string root, string folder, string name)
    {
        var directory = Path.Combine(root, folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "loader fixture");
        return path;
    }

    private static void WithAssets(Action<string> test)
    {
        var fixtureParent = Path.GetFullPath(AppContext.BaseDirectory);
        var root = Path.Combine(fixtureParent, "p6-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally
        {
            if (!Path.GetFullPath(root).StartsWith(Path.Combine(fixtureParent, "p6-test-"),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture cleanup escaped the owned test output.");
            // Windows can briefly deny deletion just after file inspection.
            // Retry cleanup of this owned fixture only; persistent failures still fail the check.
            for (var attempt = 0; ; attempt++)
            {
                try { Directory.Delete(root, recursive: true); break; }
                catch (IOException) when (attempt < 20) { Thread.Sleep(100); }
                catch (UnauthorizedAccessException) when (attempt < 20) { Thread.Sleep(100); }
            }
        }
    }
}
