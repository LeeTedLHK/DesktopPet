using DesktopPet.Models;

namespace DesktopPet.Behaviors;

public static class PersonalityPolicy
{
    public static double WaitMultiplier(PetPersonality personality) => personality switch
    { PetPersonality.Lively => .7, PetPersonality.Quiet => 1.8, _ => 1 };
    public static TimeSpan LookCooldown(PetPersonality personality) =>
        TimeSpan.FromSeconds(personality == PetPersonality.Clingy ? 6 : 12);
    public static PetState NextAction(Random random, PetPersonality personality, bool natural, bool tired)
    {
        if (tired && random.Next(100) < 55) return PetState.Sleeping;
        if (personality == PetPersonality.Default)
            return natural ? CompanionPolicy.NextNaturalAction(random) : BehaviorPolicy.NextIdleAction(random);
        var roll = random.Next(100);
        var walks = personality switch { PetPersonality.Lively => 75, PetPersonality.Quiet => 15, _ => 35 };
        if (roll < walks) return random.Next(2) == 0 ? PetState.WalkingLeft : PetState.WalkingRight;
        if (natural && roll < walks + 8) return PetState.Stretching;
        if (natural && roll < walks + 16) return PetState.Yawning;
        if (roll >= 95) return PetState.Sleeping;
        return PetState.Idle;
    }
}
