using DesktopPet.Services;

namespace DesktopPet.Tests;

public sealed class ScreenGeometryTests
{
    private static readonly WorkArea Area = new(-1000, -200, 1000, 800);
    private static readonly PetSize Pet = new(160, 120);

    [Theory]
    [InlineData(-1200, -400, -1000, -200)]
    [InlineData(100, 700, -160, 480)]
    [InlineData(-1000, -200, -1000, -200)]
    [InlineData(-160, 480, -160, 480)]
    public void Clamp_FitsPetInsideWorkArea(double x, double y, double expectedX, double expectedY)
    {
        Assert.Equal(new PetPoint(expectedX, expectedY), ScreenGeometry.Clamp(new PetPoint(x, y), Pet, Area));
    }

    [Fact]
    public void Clamp_AlignsOversizedPetToWorkAreaStart()
    {
        var result = ScreenGeometry.Clamp(new PetPoint(50, 50), new PetSize(1200, 900), Area);

        Assert.Equal(new PetPoint(-1000, -200), result);
    }

    [Fact]
    public void DefaultPosition_UsesRightAndBottomMargins()
    {
        Assert.Equal(new PetPoint(-210, 460), ScreenGeometry.DefaultPosition(Pet, Area));
    }

    [Theory]
    [InlineData(double.NaN, 10)]
    [InlineData(10, double.PositiveInfinity)]
    public void Clamp_InvalidPositionUsesDefault(double x, double y)
    {
        Assert.Equal(ScreenGeometry.DefaultPosition(Pet, Area), ScreenGeometry.Clamp(new PetPoint(x, y), Pet, Area));
    }

    [Theory]
    [InlineData(-1050, 0, -1000, 0)]
    [InlineData(-50, 0, -160, 0)]
    [InlineData(-160, 480, -160, 480)]
    public void Restore_ClampsSavedPositionThatIntersectsWorkArea(double x, double y, double expectedX, double expectedY)
    {
        Assert.Equal(new PetPoint(expectedX, expectedY), ScreenGeometry.Restore(x, y, Pet, Area));
    }

    [Theory]
    [InlineData(null, 0.0)]
    [InlineData(0.0, null)]
    [InlineData(-1160.0, 0.0)]
    [InlineData(0.0, 600.0)]
    [InlineData(double.NaN, 0.0)]
    public void Restore_DefaultsMissingInvalidOrFullyOffscreenPosition(double? x, double? y)
    {
        Assert.Equal(ScreenGeometry.DefaultPosition(Pet, Area), ScreenGeometry.Restore(x, y, Pet, Area));
    }

    [Fact]
    public void GeometryHandlesInvalidSizesWithoutReturningNonfiniteCoordinates()
    {
        var result = ScreenGeometry.Clamp(
            new PetPoint(25, 30),
            new PetSize(double.NaN, double.NegativeInfinity),
            new WorkArea(0, 0, double.PositiveInfinity, double.NaN));

        Assert.True(double.IsFinite(result.X));
        Assert.True(double.IsFinite(result.Y));
    }
}
