using DesktopPet.Models;

namespace DesktopPet.Services;

public static class SettingsChangePolicy
{
    public static bool RequiresWorldReset(AppSettings previous, AppSettings next) =>
        previous.SelectedMonitorId != next.SelectedMonitorId || previous.SelectedSkinId != next.SelectedSkinId ||
        previous.PetScale != next.PetScale || previous.ActivityRange != next.ActivityRange ||
        previous.ActivityLeftPercent != next.ActivityLeftPercent || previous.ActivityTopPercent != next.ActivityTopPercent ||
        previous.ActivityWidthPercent != next.ActivityWidthPercent || previous.ActivityHeightPercent != next.ActivityHeightPercent ||
        previous.AnimationSpeedMultiplier != next.AnimationSpeedMultiplier || previous.MoveSpeed != next.MoveSpeed ||
        previous.AutoBehaviorEnabled != next.AutoBehaviorEnabled || previous.HomeEnabled != next.HomeEnabled ||
        previous.HomeXPercent != next.HomeXPercent || previous.HomeYPercent != next.HomeYPercent ||
        previous.ToysEnabled && !next.ToysEnabled;
}
