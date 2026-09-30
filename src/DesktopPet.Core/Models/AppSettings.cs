namespace DesktopPet.Models;

public enum MovementSpeed
{
    Slow,
    Normal,
    Fast,
}

public enum BehaviorFrequency
{
    Low,
    Normal,
    High,
}

public sealed record AppSettings
{
    public bool Topmost { get; set; } = true;
    public bool AutoBehaviorEnabled { get; set; } = true;
    public bool ShowPetOnStartup { get; set; } = true;
    public double PetScale { get; set; } = 1d;
    public bool EnableAnimations { get; set; } = true;
    public double AnimationSpeedMultiplier { get; set; } = 1d;
    public MovementSpeed MoveSpeed { get; set; } = MovementSpeed.Normal;
    public BehaviorFrequency BehaviorFrequency { get; set; } = BehaviorFrequency.Normal;
    public double? LastPetX { get; set; }
    public double? LastPetY { get; set; }

    public AppSettings Normalize()
    {
        var scale = double.IsFinite(PetScale)
            ? Math.Clamp(PetScale, 0.8d, 2d)
            : 1d;
        var animationSpeed = double.IsFinite(AnimationSpeedMultiplier)
            ? Math.Clamp(AnimationSpeedMultiplier, 0.25d, 3d)
            : 1d;
        var hasCoordinatePair = LastPetX is double x && double.IsFinite(x)
            && LastPetY is double y && double.IsFinite(y);

        return this with
        {
            PetScale = scale,
            AnimationSpeedMultiplier = animationSpeed,
            MoveSpeed = Enum.IsDefined(MoveSpeed) ? MoveSpeed : MovementSpeed.Normal,
            BehaviorFrequency = Enum.IsDefined(BehaviorFrequency) ? BehaviorFrequency : BehaviorFrequency.Normal,
            LastPetX = hasCoordinatePair ? LastPetX : null,
            LastPetY = hasCoordinatePair ? LastPetY : null,
        };
    }
}
