using DesktopPet.Models;

namespace DesktopPet.Behaviors;

public static class BehaviorPolicy
{
    public static TimeSpan NextWait(Random random, BehaviorFrequency frequency)
    {
        ArgumentNullException.ThrowIfNull(random);
        var (minimum, maximum) = frequency switch
        {
            BehaviorFrequency.Low => (10, 25),
            BehaviorFrequency.High => (3, 8),
            _ => (5, 15),
        };
        return TimeSpan.FromSeconds(random.Next(minimum, maximum + 1));
    }

    public static double NextDistance(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return 50d + (random.NextDouble() * 150d);
    }

    public static TimeSpan SleepDuration(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return TimeSpan.FromSeconds(random.Next(5, 13));
    }

    public static PetState NextAction(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.Next(3) switch
        {
            0 => PetState.Idle,
            1 => PetState.WalkingLeft,
            _ => PetState.WalkingRight,
        };
    }

    public static PetState NextIdleAction(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.Next(100) < 5 ? PetState.Sleeping : NextAction(random);
    }

    public static double PixelsPerSecond(MovementSpeed speed) => speed switch
    {
        MovementSpeed.Slow => 25d,
        MovementSpeed.Fast => 75d,
        _ => 45d,
    };
}
