namespace DesktopPet.Animations;

/// <summary>Presentation-only movement joins; behavior and fetch states stay independent.</summary>
public static class MovementTransitions
{
    public static bool IsMovement(AnimationKind kind) => kind is AnimationKind.WalkLeft or AnimationKind.WalkRight or
        AnimationKind.RunLeft or AnimationKind.RunRight or AnimationKind.CarryLeft or AnimationKind.CarryRight;

    public static bool IsLeft(AnimationKind kind) => kind is AnimationKind.WalkLeft or AnimationKind.RunLeft or AnimationKind.CarryLeft;

    public static AnimationKind? ForChange(AnimationKind from, AnimationKind to)
    {
        if (from == to) return null;
        if (from == AnimationKind.Idle && IsMovement(to))
            return IsLeft(to) ? AnimationKind.StartLeft : AnimationKind.StartRight;
        if (IsMovement(from) && to == AnimationKind.Idle)
            return IsLeft(from) ? AnimationKind.StopLeft : AnimationKind.StopRight;
        if (IsMovement(from) && IsMovement(to) && IsLeft(from) != IsLeft(to))
            return IsLeft(to) ? AnimationKind.TurnLeft : AnimationKind.TurnRight;
        if (from is AnimationKind.PickupLeft or AnimationKind.PickupRight or AnimationKind.JumpLeft or AnimationKind.JumpRight &&
            to is AnimationKind.CarryLeft or AnimationKind.CarryRight &&
            from.ToString().EndsWith("Left", StringComparison.Ordinal) != IsLeft(to))
            return IsLeft(to) ? AnimationKind.TurnLeft : AnimationKind.TurnRight;
        return null;
    }
}
