namespace DesktopPet.Services;

// Pixel origin is preserved as the virtual DIP origin; only local offsets are scaled.
// This avoids dividing negative/global coordinates by the destination monitor's DPI.
public sealed record MonitorInfo(string Id, string Name, WorkArea PixelBounds, WorkArea PixelWorkArea, double DpiScale, bool Primary)
{
    public double Scale => double.IsFinite(DpiScale) && DpiScale is >= .5 and <= 8 ? DpiScale : 1;
    public WorkArea DipWorkArea => new(PixelBounds.Left + (PixelWorkArea.Left - PixelBounds.Left) / Scale,
        PixelBounds.Top + (PixelWorkArea.Top - PixelBounds.Top) / Scale, PixelWorkArea.Width / Scale, PixelWorkArea.Height / Scale);
    public PetPoint ToDip(PetPoint pixels) => new(PixelBounds.Left + (pixels.X - PixelBounds.Left) / Scale,
        PixelBounds.Top + (pixels.Y - PixelBounds.Top) / Scale);
    public PetPoint ToPixels(PetPoint dip) => new(PixelBounds.Left + (dip.X - PixelBounds.Left) * Scale,
        PixelBounds.Top + (dip.Y - PixelBounds.Top) * Scale);
    public string DisplayName => $"{Name}{(Primary ? " · 主屏" : "")} · {Scale * 100:0}%";
}

public static class MonitorGeometry
{
    public static MonitorInfo Resolve(IReadOnlyList<MonitorInfo> monitors, string? preferred, PetPoint? saved = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(monitors.Count);
        var selected = monitors.FirstOrDefault(m => m.Id == preferred);
        if (selected is not null) return selected;
        if (string.IsNullOrEmpty(preferred) && saved is { } point && double.IsFinite(point.X) && double.IsFinite(point.Y))
            return At(monitors, point);
        return monitors.FirstOrDefault(m => m.Primary) ?? monitors[0];
    }
    public static MonitorInfo At(IReadOnlyList<MonitorInfo> monitors, PetPoint point)
    {
        ArgumentOutOfRangeException.ThrowIfZero(monitors.Count);
        return monitors.OrderBy(m => Distance(m.PixelBounds, point)).ThenByDescending(m => m.Primary).First();
    }
    private static double Distance(WorkArea area, PetPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return double.MaxValue;
        var x = Math.Max(0, Math.Max(area.Left - point.X, point.X - area.Right));
        var y = Math.Max(0, Math.Max(area.Top - point.Y, point.Y - area.Bottom));
        return x * x + y * y;
    }
}
