using DesktopPet.Behaviors;
using DesktopPet.Models;

namespace DesktopPet.Tests;

public sealed class BehaviorPolicyTests
{
    [Theory]
    [InlineData(BehaviorFrequency.Low, 10, 25)]
    [InlineData(BehaviorFrequency.Normal, 5, 15)]
    [InlineData(BehaviorFrequency.High, 3, 8)]
    public void NextWait_StaysWithinInclusiveRange(BehaviorFrequency frequency, int minimum, int maximum)
    {
        var random = new Random(20260911);
        var values = Enumerable.Range(0, 2_000)
            .Select(_ => BehaviorPolicy.NextWait(random, frequency).TotalSeconds)
            .ToArray();

        Assert.All(values, value => Assert.InRange(value, minimum, maximum));
        Assert.Contains((double)minimum, values);
        Assert.Contains((double)maximum, values);
    }

    [Fact]
    public void NextDistance_StaysWithinRequiredRange()
    {
        var random = new Random(87);
        var values = Enumerable.Range(0, 2_000).Select(_ => BehaviorPolicy.NextDistance(random));

        Assert.All(values, value => Assert.InRange(value, 50d, 200d));
    }

    [Fact]
    public void SleepDuration_StaysWithinShortBoundedRange()
    {
        var random = new Random(20260911);
        var values = Enumerable.Range(0, 2_000)
            .Select(_ => BehaviorPolicy.SleepDuration(random).TotalSeconds)
            .ToArray();

        Assert.All(values, value => Assert.InRange(value, 5d, 12d));
        Assert.Contains(5d, values);
        Assert.Contains(12d, values);
    }

    [Fact]
    public void NextAction_ReturnsAllAllowedActionsWithReasonableDistribution()
    {
        var random = new Random(9182);
        var counts = Enumerable.Range(0, 6_000)
            .Select(_ => BehaviorPolicy.NextAction(random))
            .GroupBy(state => state)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(3, counts.Count);
        Assert.All(new[] { PetState.Idle, PetState.WalkingLeft, PetState.WalkingRight }, state =>
        {
            Assert.True(counts.TryGetValue(state, out var count));
            Assert.InRange(count, 1_700, 2_300);
        });
    }

    [Fact]
    public void NextIdleAction_ReturnsSleepAlongsideExistingIdleActions()
    {
        var random = new Random(20260911);
        var actions = Enumerable.Range(0, 10_000)
            .Select(_ => BehaviorPolicy.NextIdleAction(random))
            .ToHashSet();

        Assert.All(actions, action => Assert.Contains(action,
            new[] { PetState.Idle, PetState.WalkingLeft, PetState.WalkingRight, PetState.Sleeping }));
        Assert.Contains(PetState.Sleeping, actions);
    }

    [Theory]
    [InlineData(MovementSpeed.Slow, 25)]
    [InlineData(MovementSpeed.Normal, 45)]
    [InlineData(MovementSpeed.Fast, 75)]
    [InlineData((MovementSpeed)99, 45)]
    public void PixelsPerSecond_MapsSpeed(MovementSpeed speed, double expected)
    {
        Assert.Equal(expected, BehaviorPolicy.PixelsPerSecond(speed));
    }
}
