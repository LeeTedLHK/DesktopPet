using DesktopPet.Models;

namespace DesktopPet.Animations;

public static class AnimationDefinition
{
    public static AnimationKind ForState(PetState state) => state switch
    {
        PetState.WalkingLeft => AnimationKind.WalkLeft,
        PetState.WalkingRight => AnimationKind.WalkRight,
        PetState.Happy => AnimationKind.Happy,
        PetState.Sleeping => AnimationKind.Sleep,
        _ => AnimationKind.Idle,
    };

    public static AnimationClip CreateClip(AnimationKind kind,
        IReadOnlyList<AnimationFrame> frames, bool isFallback = false)
    {
        var (framesPerSecond, playback) = kind switch
        {
            AnimationKind.Walk or AnimationKind.WalkLeft or AnimationKind.WalkRight =>
                (10d, AnimationPlayback.Loop),
            AnimationKind.Happy => (9d, AnimationPlayback.Once),
            AnimationKind.Sleep => (4d, AnimationPlayback.Loop),
            _ => (5d, AnimationPlayback.Loop),
        };
        return new AnimationClip(kind, frames, framesPerSecond, playback, isFallback);
    }
}
