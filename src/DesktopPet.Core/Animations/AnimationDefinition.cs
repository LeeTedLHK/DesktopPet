using DesktopPet.Models;

namespace DesktopPet.Animations;

public static class AnimationDefinition
{
    public static AnimationKind ForState(PetState state) => state switch
    {
        PetState.WalkingLeft => AnimationKind.WalkLeft,
        PetState.WalkingRight => AnimationKind.WalkRight,
        PetState.RunningLeft => AnimationKind.RunLeft,
        PetState.RunningRight => AnimationKind.RunRight,
        PetState.JumpingLeft => AnimationKind.JumpLeft,
        PetState.JumpingRight => AnimationKind.JumpRight,
        PetState.PickingUpLeft => AnimationKind.PickupLeft,
        PetState.PickingUpRight => AnimationKind.PickupRight,
        PetState.CarryingLeft => AnimationKind.CarryLeft,
        PetState.CarryingRight => AnimationKind.CarryRight,
        PetState.DroppingLeft => AnimationKind.DropLeft,
        PetState.DroppingRight => AnimationKind.DropRight,
        PetState.Celebrating => AnimationKind.Happy,
        PetState.Happy => AnimationKind.Happy,
        PetState.Sleeping => AnimationKind.Sleep,
        PetState.Petting => AnimationKind.Petting,
        PetState.Feeding => AnimationKind.Feeding,
        PetState.Stretching => AnimationKind.Stretching,
        PetState.Yawning => AnimationKind.Yawning,
        PetState.Dragging => AnimationKind.Dragging,
        PetState.LookingLeft => AnimationKind.LookLeft,
        PetState.LookingRight => AnimationKind.LookRight,
        _ => AnimationKind.Idle,
    };

    public static AnimationClip CreateClip(AnimationKind kind,
        IReadOnlyList<AnimationFrame> frames, bool isFallback = false)
    {
        var (framesPerSecond, playback) = kind switch
        {
            // Eight video-guided phases span a 16/30-second reference stride.
            AnimationKind.WalkLeft or AnimationKind.WalkRight => (15d, AnimationPlayback.Loop),
            AnimationKind.Walk => (10d, AnimationPlayback.Loop),
            AnimationKind.RunLeft or AnimationKind.RunRight => (12d, AnimationPlayback.Loop),
            AnimationKind.CarryLeft or AnimationKind.CarryRight => (10d, AnimationPlayback.Loop),
            AnimationKind.StartLeft or AnimationKind.StartRight or AnimationKind.StopLeft or AnimationKind.StopRight or
                AnimationKind.TurnLeft or AnimationKind.TurnRight or AnimationKind.PickupLeft or AnimationKind.PickupRight or
                AnimationKind.DropLeft or AnimationKind.DropRight => (8d, AnimationPlayback.Once),
            AnimationKind.JumpLeft or AnimationKind.JumpRight => (12d, AnimationPlayback.Once),
            AnimationKind.Happy => (5d, AnimationPlayback.Once),
            AnimationKind.Sleep => (4d, AnimationPlayback.Loop),
            AnimationKind.Petting or AnimationKind.Feeding => (4d, AnimationPlayback.Once),
            AnimationKind.Stretching or AnimationKind.Yawning => (2d, AnimationPlayback.Once),
            AnimationKind.Dragging or AnimationKind.LookLeft or AnimationKind.LookRight =>
                (4d, AnimationPlayback.Loop),
            _ => (5d, AnimationPlayback.Loop),
        };
        return new AnimationClip(kind, frames, framesPerSecond, playback, isFallback);
    }
}
