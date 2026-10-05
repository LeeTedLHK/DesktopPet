using DesktopPet.Models;

namespace DesktopPet.Services;

public static class ActivityAreaPolicy
{
    public static WorkArea Resolve(AppSettings settings, PetSize size, WorkArea screen)
    {
        settings = settings.Normalize();
        var bounds = settings.ActivityRange switch
        {
            ActivityRange.Bottom => new WorkArea(screen.Left, screen.Bottom - screen.Height * .25, screen.Width, screen.Height * .25),
            ActivityRange.Corner => new WorkArea(screen.Right - screen.Width * .3, screen.Bottom - screen.Height * .3, screen.Width * .3, screen.Height * .3),
            ActivityRange.Custom => new WorkArea(screen.Left + screen.Width * settings.ActivityLeftPercent / 100,
                screen.Top + screen.Height * settings.ActivityTopPercent / 100,
                screen.Width * settings.ActivityWidthPercent / 100, screen.Height * settings.ActivityHeightPercent / 100),
            _ => screen
        };
        var width = Math.Min(screen.Width, Math.Max(size.Width, bounds.Width));
        var height = Math.Min(screen.Height, Math.Max(size.Height, bounds.Height));
        var left = Math.Clamp(bounds.Left, screen.Left, screen.Right - width);
        var top = Math.Clamp(bounds.Top, screen.Top, screen.Bottom - height);
        return new(left, top, width, height);
    }
}
