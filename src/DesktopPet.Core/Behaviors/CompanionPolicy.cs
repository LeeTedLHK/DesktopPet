using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.Behaviors;

public static class CompanionPolicy
{
    public static bool IsSleepTime(int hour, int startHour, int endHour)
    {
        if (hour is < 0 or > 23 || startHour is < 0 or > 23 || endHour is < 0 or > 23 ||
            startHour == endHour) return false;
        return startHour < endHour ? hour >= startHour && hour < endHour :
            hour >= startHour || hour < endHour;
    }

    public static bool IsFullscreen(WorkArea window, WorkArea monitor)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0 || window.Width <= 0 || window.Height <= 0 ||
            !double.IsFinite(window.Left) || !double.IsFinite(window.Top) ||
            !double.IsFinite(window.Width) || !double.IsFinite(window.Height)) return false;
        const double tolerance = 2;
        return window.Left <= monitor.Left + tolerance && window.Top <= monitor.Top + tolerance &&
            window.Right >= monitor.Right - tolerance && window.Bottom >= monitor.Bottom - tolerance;
    }

    public static PetState? LookDirection(PetPoint petCenter, PetPoint cursor)
    {
        var x = cursor.X - petCenter.X;
        var y = cursor.Y - petCenter.Y;
        if (!double.IsFinite(x) || !double.IsFinite(y) || x * x + y * y > 350 * 350 ||
            Math.Abs(x) < 30) return null;
        return x < 0 ? PetState.LookingLeft : PetState.LookingRight;
    }

    public static PetState NextNaturalAction(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.Next(100) switch
        {
            < 12 => PetState.Stretching,
            < 24 => PetState.Yawning,
            < 29 => PetState.Sleeping,
            _ => BehaviorPolicy.NextAction(random)
        };
    }
}
