using DesktopPet.Models;

namespace DesktopPet.Companions;

public enum ReminderKind { Water, Move, Rest, FocusFinished, BreakFinished }
public sealed record ReminderNotice(ReminderKind Kind, DateTimeOffset CreatedUtc)
{
    public string Text => Kind switch
    {
        ReminderKind.Water => "喝口水吧，我陪你一起歇一下～",
        ReminderKind.Move => "坐了一会儿啦，起来走动、伸个懒腰吧～",
        ReminderKind.Rest => "让眼睛休息一会儿，看看远处吧～",
        ReminderKind.FocusFinished => "这一轮专注完成啦！现在休息一下，我陪着你～",
        _ => "休息结束啦，准备好了再开始下一轮专注吧～"
    };
}

public sealed class ReminderScheduler
{
    private readonly Dictionary<ReminderKind, (int Minutes, DateTimeOffset Due)> _schedule = [];
    private readonly Dictionary<ReminderKind, ReminderNotice> _pending = [];
    private DateTimeOffset? _lastDelivered;
    public int PendingCount => _pending.Count;
    public DateTimeOffset? HealthSnoozedUntil { get; private set; }
    public void Configure(AppSettings settings, DateTimeOffset now)
    {
        ExpireSnooze(now);
        settings = settings.Normalize();
        Set(ReminderKind.Water, settings.WaterReminderEnabled, settings.WaterReminderMinutes, now);
        Set(ReminderKind.Move, settings.MoveReminderEnabled, settings.MoveReminderMinutes, now);
        Set(ReminderKind.Rest, settings.RestReminderEnabled, settings.RestReminderMinutes, now);
    }
    private void Set(ReminderKind kind, bool enabled, int minutes, DateTimeOffset now)
    {
        if (!enabled) { _schedule.Remove(kind); _pending.Remove(kind); return; }
        if (_schedule.TryGetValue(kind, out var old) && old.Minutes == minutes) return;
        _pending.Remove(kind);
        var due = now.AddMinutes(minutes);
        if (HealthSnoozedUntil is { } until && due < until) due = until;
        _schedule[kind] = (minutes, due);
    }
    public void Enqueue(ReminderKind kind, DateTimeOffset now)
    {
        if (kind is ReminderKind.FocusFinished or ReminderKind.BreakFinished) ClearFocus();
        _pending[kind] = new(kind, now);
    }
    public void ClearFocus()
    { _pending.Remove(ReminderKind.FocusFinished); _pending.Remove(ReminderKind.BreakFinished); }
    public void SnoozeHealth(DateTimeOffset now, int minutes)
    {
        if (minutes is not (15 or 30 or 60)) throw new ArgumentOutOfRangeException(nameof(minutes));
        var until = now.ToUniversalTime().AddMinutes(minutes);
        HealthSnoozedUntil = until;
        ClearHealth();
        foreach (var (kind, timer) in _schedule.ToArray())
            if (timer.Due < until) _schedule[kind] = (timer.Minutes, until);
    }
    public void ResumeHealth(DateTimeOffset now)
    {
        HealthSnoozedUntil = null;
        ClearHealth();
        // Resume from a normal interval rather than replaying a suppressed deadline.
        foreach (var (kind, timer) in _schedule.ToArray())
            _schedule[kind] = (timer.Minutes, now.AddMinutes(timer.Minutes));
    }
    private void ClearHealth()
    { _pending.Remove(ReminderKind.Water); _pending.Remove(ReminderKind.Move); _pending.Remove(ReminderKind.Rest); }
    private void ExpireSnooze(DateTimeOffset now)
    {
        if (HealthSnoozedUntil is { } until && now >= until) HealthSnoozedUntil = null;
    }
    public ReminderNotice? Tick(DateTimeOffset now, bool canDisplay, bool healthAllowed = true)
    {
        ExpireSnooze(now);
        var healthSuppressed = !healthAllowed || HealthSnoozedUntil is not null;
        if (healthSuppressed) ClearHealth();
        foreach (var (kind, timer) in _schedule.ToArray())
        {
            if (now < timer.Due) continue;
            // Missed intervals are coalesced, and expiry uses the original deadline.
            if (!healthSuppressed) _pending[kind] = new(kind, timer.Due);
            _schedule[kind] = (timer.Minutes, now.AddMinutes(timer.Minutes));
        }
        foreach (var (kind, notice) in _pending.ToArray())
            if (now - notice.CreatedUtc >= TimeSpan.FromMinutes(10)) _pending.Remove(kind);
        if (!canDisplay || (_lastDelivered is { } last && now - last < TimeSpan.FromMinutes(2))) return null;
        var next = _pending.Values.OrderByDescending(n => n.Kind is ReminderKind.FocusFinished or ReminderKind.BreakFinished)
            .ThenBy(n => n.CreatedUtc).ThenBy(n => n.Kind).FirstOrDefault();
        if (next is null) return null;
        _pending.Remove(next.Kind);
        _lastDelivered = now;
        return next;
    }
}
