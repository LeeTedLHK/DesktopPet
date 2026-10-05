using DesktopPet.Animations;
using DesktopPet.Controllers;
using DesktopPet.Models;

internal static class P7AnimationChecks
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var (from, to, expected) in new[] {
            (AnimationKind.Idle, AnimationKind.WalkLeft, AnimationKind.StartLeft),
            (AnimationKind.Idle, AnimationKind.RunRight, AnimationKind.StartRight),
            (AnimationKind.WalkLeft, AnimationKind.Idle, AnimationKind.StopLeft),
            (AnimationKind.RunRight, AnimationKind.Idle, AnimationKind.StopRight),
            (AnimationKind.WalkRight, AnimationKind.RunLeft, AnimationKind.TurnLeft),
            (AnimationKind.CarryLeft, AnimationKind.CarryRight, AnimationKind.TurnRight) })
            check(MovementTransitions.ForChange(from, to) == expected, $"P7 {from} to {to} selects {expected}");
        check(MovementTransitions.ForChange(AnimationKind.PickupRight, AnimationKind.CarryLeft) == AnimationKind.TurnLeft,
            "P7 picking up a toy flows through a turn before carrying it back");
        check(MovementTransitions.ForChange(AnimationKind.RunLeft, AnimationKind.RunLeft) is null,
            "P7 repeated direction does not restart movement");
        check(MovementTransitions.ForChange(AnimationKind.RunLeft, AnimationKind.Petting) is null,
            "P7 interaction immediately interrupts motion presentation");
        check(MovementTransitions.ForChange(AnimationKind.PickupLeft, AnimationKind.CarryLeft) is null,
            "P7 mouth attachment remains continuous from pickup to carrying");
        check(MovementTransitions.ForChange(AnimationKind.Idle, AnimationKind.Idle) is null,
            "P7 stable paused pose has no redundant transition");

        foreach (var state in new[] { PetState.JumpingLeft, PetState.JumpingRight, PetState.PickingUpLeft,
            PetState.PickingUpRight, PetState.CarryingLeft, PetState.CarryingRight, PetState.DroppingLeft,
            PetState.DroppingRight })
        {
            var machine = new PetStateMachine(false);
            check(machine.TryBeginFetchAction(state), $"P7 {state} works while automatic behavior is paused");
            machine.CompleteFetchAction();
            check(machine.State == PetState.Paused, $"P7 {state} completes to paused preference");
            machine.TryBeginFetchAction(state); machine.BeginDrag(); machine.CompleteFetchAction();
            check(machine.State == PetState.Dragging, $"P7 cancellation of {state} preserves dragging");
            machine.EndDrag(); machine.SetVisible(false);
            check(!machine.TryBeginFetchAction(state), $"P7 hidden {state} is rejected");
        }
        var rejected = new PetStateMachine();
        check(!rejected.TryBeginFetchAction(PetState.Happy), "P7 fetch entry rejects unrelated interaction");

        var root = Path.Combine(AppContext.BaseDirectory, "p7-animation-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "WalkLeft"));
            File.WriteAllText(Path.Combine(root, "WalkLeft", "walk_0.png"), "loader fixture");
            Directory.CreateDirectory(Path.Combine(root, "WalkRight"));
            File.WriteAllText(Path.Combine(root, "WalkRight", "walk_0.png"), "loader fixture");
            foreach (var kind in new[] { AnimationKind.JumpLeft, AnimationKind.PickupLeft, AnimationKind.CarryLeft,
                AnimationKind.DropLeft, AnimationKind.JumpRight, AnimationKind.PickupRight, AnimationKind.CarryRight,
                AnimationKind.DropRight })
            {
                var clip = new AnimationLoader(root).Load()[kind];
                var direction = kind.ToString().EndsWith("Left") ? "WalkLeft" : "WalkRight";
                check(clip.IsFallback && clip.Frames.Count == 1 && Path.GetFileName(Path.GetDirectoryName(clip.Frames[0].Path)) == direction,
                    $"P7 missing {kind} uses the same direction from an old skin");
                check(clip.Playback == (kind is AnimationKind.CarryLeft or AnimationKind.CarryRight ? AnimationPlayback.Loop : AnimationPlayback.Once),
                    $"P7 missing {kind} preserves its playback contract");
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
