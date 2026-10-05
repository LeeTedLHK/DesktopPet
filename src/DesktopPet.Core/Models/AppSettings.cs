using System.Globalization;

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

public enum PetPersonality { Default, Lively, Quiet, Clingy }
public enum ActivityRange { Full, Bottom, Corner, Custom }
public enum HomeTheme { Cozy, Mint, Rose }

public sealed record AppSettings
{
    public string SelectedMonitorId { get; set; } = "";
    public bool AllowCrossMonitorDrag { get; set; } = true;
    public bool SavedPositionUsesPixels { get; set; }
    public string SelectedSkinId { get; set; } = "default";
    public bool StartWithWindows { get; set; }
    public bool PreciseHitTestEnabled { get; set; } = true;
    public bool ToysEnabled { get; set; } = true;
    public double BallScale { get; set; } = 1d;
    public double FrisbeeScale { get; set; } = 1d;
    public double FeedingBowlScale { get; set; } = 1d;
    public bool PowerSavingEnabled { get; set; }
    public bool HomeEnabled { get; set; }
    public double HomeXPercent { get; set; } = 85;
    public double HomeYPercent { get; set; } = 90;
    public HomeTheme HomeTheme { get; set; }
    public bool JournalEnabled { get; set; } = true;
    public bool GrowthEnabled { get; set; } = true;
    public string PetName { get; set; } = "团团";
    public PetPersonality PetPersonality { get; set; }
    public ActivityRange ActivityRange { get; set; }
    public double ActivityLeftPercent { get; set; }
    public double ActivityTopPercent { get; set; }
    public double ActivityWidthPercent { get; set; } = 100;
    public double ActivityHeightPercent { get; set; } = 100;
    public bool WaterReminderEnabled { get; set; }
    public bool MoveReminderEnabled { get; set; }
    public bool RestReminderEnabled { get; set; }
    public int WaterReminderMinutes { get; set; } = 45;
    public int MoveReminderMinutes { get; set; } = 60;
    public int RestReminderMinutes { get; set; } = 30;
    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public bool FocusQuietEnabled { get; set; }
    public bool Topmost { get; set; } = true;
    public bool AutoBehaviorEnabled { get; set; } = true;
    public bool ShowPetOnStartup { get; set; } = true;
    public double PetScale { get; set; } = 1d;
    public bool EnableAnimations { get; set; } = true;
    public bool FullscreenAutoHide { get; set; } = true;
    public bool ClickThrough { get; set; }
    public bool GlobalHotkeysEnabled { get; set; } = true;
    public bool SpeechBubblesEnabled { get; set; } = true;
    public bool NaturalBehaviorsEnabled { get; set; } = true;
    public bool CursorTrackingEnabled { get; set; } = true;
    public bool NightSleepEnabled { get; set; } = true;
    public int NightSleepStartHour { get; set; } = 22;
    public int NightSleepEndHour { get; set; } = 7;
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
        var left = Percent(ActivityLeftPercent, 0, 90, 0);
        var top = Percent(ActivityTopPercent, 0, 90, 0);
        var name = string.IsNullOrWhiteSpace(PetName) ? "团团" : PetName.Trim();
        var nameCharacters = StringInfo.ParseCombiningCharacters(name);

        return this with
        {
            SelectedMonitorId = SelectedMonitorId?.Trim() ?? "",
            SelectedSkinId = Guid.TryParseExact(SelectedSkinId, "N", out var skin) ? skin.ToString("N") : "default",
            BallScale = Percent(BallScale, .6d, 1.5d, 1d),
            FrisbeeScale = Percent(FrisbeeScale, .6d, 1.5d, 1d),
            FeedingBowlScale = Percent(FeedingBowlScale, .6d, 1.5d, 1d),
            HomeXPercent = Percent(HomeXPercent, 0, 100, 85),
            HomeYPercent = Percent(HomeYPercent, 0, 100, 90),
            HomeTheme = Enum.IsDefined(HomeTheme) ? HomeTheme : HomeTheme.Cozy,
            PetName = nameCharacters.Length > 20 ? name[..nameCharacters[20]] : name,
            PetPersonality = Enum.IsDefined(PetPersonality) ? PetPersonality : PetPersonality.Default,
            ActivityRange = Enum.IsDefined(ActivityRange) ? ActivityRange : ActivityRange.Full,
            ActivityLeftPercent = left,
            ActivityTopPercent = top,
            ActivityWidthPercent = Percent(ActivityWidthPercent, 10, 100 - left, 100 - left),
            ActivityHeightPercent = Percent(ActivityHeightPercent, 10, 100 - top, 100 - top),
            WaterReminderMinutes = Math.Clamp(WaterReminderMinutes, 15, 120),
            MoveReminderMinutes = Math.Clamp(MoveReminderMinutes, 15, 120),
            RestReminderMinutes = Math.Clamp(RestReminderMinutes, 15, 120),
            FocusMinutes = Math.Clamp(FocusMinutes, 1, 120),
            BreakMinutes = Math.Clamp(BreakMinutes, 1, 60),
            PetScale = scale,
            AnimationSpeedMultiplier = animationSpeed,
            NightSleepStartHour = NightSleepStartHour is >= 0 and <= 23 ? NightSleepStartHour : 22,
            NightSleepEndHour = NightSleepEndHour is >= 0 and <= 23 ? NightSleepEndHour : 7,
            MoveSpeed = Enum.IsDefined(MoveSpeed) ? MoveSpeed : MovementSpeed.Normal,
            BehaviorFrequency = Enum.IsDefined(BehaviorFrequency) ? BehaviorFrequency : BehaviorFrequency.Normal,
            LastPetX = hasCoordinatePair ? LastPetX : null,
            LastPetY = hasCoordinatePair ? LastPetY : null,
        };
    }

    private static double Percent(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
