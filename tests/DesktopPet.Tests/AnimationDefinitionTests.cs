using DesktopPet.Animations;
using DesktopPet.Models;

namespace DesktopPet.Tests;

public sealed class AnimationDefinitionTests
{
    [Theory]
    [InlineData(PetState.Idle, AnimationKind.Idle)]
    [InlineData(PetState.Dragging, AnimationKind.Idle)]
    [InlineData(PetState.Paused, AnimationKind.Idle)]
    [InlineData(PetState.WalkingLeft, AnimationKind.WalkLeft)]
    [InlineData(PetState.WalkingRight, AnimationKind.WalkRight)]
    [InlineData(PetState.Happy, AnimationKind.Happy)]
    [InlineData(PetState.Sleeping, AnimationKind.Sleep)]
    public void ForState_ReturnsTheVisualAnimationKind(PetState state, AnimationKind expected)
    {
        Assert.Equal(expected, AnimationDefinition.ForState(state));
    }

    [Theory]
    [InlineData(AnimationKind.Idle, 5d, AnimationPlayback.Loop)]
    [InlineData(AnimationKind.Walk, 10d, AnimationPlayback.Loop)]
    [InlineData(AnimationKind.Happy, 9d, AnimationPlayback.Once)]
    [InlineData(AnimationKind.Sleep, 4d, AnimationPlayback.Loop)]
    public void CreateClip_UsesActionDefaults(AnimationKind kind, double expectedFps,
        AnimationPlayback expectedPlayback)
    {
        var clip = AnimationDefinition.CreateClip(kind, [new AnimationFrame("frame.png", 0)]);

        Assert.Equal(kind, clip.Kind);
        Assert.Equal(expectedFps, clip.FramesPerSecond);
        Assert.Equal(expectedPlayback, clip.Playback);
        Assert.Equal(TimeSpan.FromSeconds(1d / expectedFps), clip.FrameInterval);
    }
}
