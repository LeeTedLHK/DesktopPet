using DesktopPet.Models;

namespace DesktopPet.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_AreSafeAndUseful()
    {
        var settings = new AppSettings();

        Assert.True(settings.Topmost);
        Assert.True(settings.AutoBehaviorEnabled);
        Assert.True(settings.ShowPetOnStartup);
        Assert.Equal(1d, settings.PetScale);
        Assert.Equal(MovementSpeed.Normal, settings.MoveSpeed);
        Assert.Equal(BehaviorFrequency.Normal, settings.BehaviorFrequency);
        Assert.True(settings.EnableAnimations);
        Assert.Equal(1d, settings.AnimationSpeedMultiplier);
        Assert.Null(settings.LastPetX);
        Assert.Null(settings.LastPetY);
    }

    [Fact]
    public void Normalize_ReturnsSanitizedCloneWithoutMutatingOriginal()
    {
        var original = new AppSettings
        {
            PetScale = 7,
            MoveSpeed = (MovementSpeed)99,
            BehaviorFrequency = (BehaviorFrequency)(-1),
            LastPetX = double.NaN,
            LastPetY = 12,
        };

        var normalized = original.Normalize();

        Assert.NotSame(original, normalized);
        Assert.Equal(7, original.PetScale);
        Assert.Equal(2, normalized.PetScale);
        Assert.Equal(MovementSpeed.Normal, normalized.MoveSpeed);
        Assert.Equal(BehaviorFrequency.Normal, normalized.BehaviorFrequency);
        Assert.Null(normalized.LastPetX);
        Assert.Null(normalized.LastPetY);
    }

    [Theory]
    [InlineData(0.1, 0.8)]
    [InlineData(0.8, 0.8)]
    [InlineData(1.4, 1.4)]
    [InlineData(2.0, 2.0)]
    [InlineData(4.0, 2.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void Normalize_ClampsOrDefaultsScale(double input, double expected)
    {
        Assert.Equal(expected, new AppSettings { PetScale = input }.Normalize().PetScale);
    }

    [Fact]
    public void WithExpression_CanCreateModifiedRecord()
    {
        var original = new AppSettings();
        var modified = original with { Topmost = false, PetScale = 1.5 };

        Assert.True(original.Topmost);
        Assert.False(modified.Topmost);
        Assert.Equal(1.5, modified.PetScale);
    }

    [Theory]
    [InlineData(0.1, 0.25)]
    [InlineData(0.25, 0.25)]
    [InlineData(1.75, 1.75)]
    [InlineData(3.0, 3.0)]
    [InlineData(5.0, 3.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void Normalize_ClampsOrDefaultsAnimationSpeed(double input, double expected)
    {
        Assert.Equal(expected, new AppSettings { AnimationSpeedMultiplier = input }
            .Normalize().AnimationSpeedMultiplier);
    }
}
