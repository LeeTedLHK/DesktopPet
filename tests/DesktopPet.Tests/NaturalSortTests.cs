using DesktopPet.Animations;

namespace DesktopPet.Tests;

public sealed class NaturalSortTests
{
    [Fact]
    public void Order_ComparesEmbeddedNumbersNumerically()
    {
        var ordered = NaturalSort.Order([
            "walk_10.png",
            "walk_2.png",
            "walk_1.png",
            "walk_final.png",
            "Walk_02.png",
        ]);

        Assert.Equal([
            "walk_1.png",
            "walk_2.png",
            "Walk_02.png",
            "walk_10.png",
            "walk_final.png",
        ], ordered);
    }
}
