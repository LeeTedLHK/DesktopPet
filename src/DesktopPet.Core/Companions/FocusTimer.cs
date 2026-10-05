namespace DesktopPet.Companions;

public enum FocusPhase { Stopped, Focus, Break, Completed }

public sealed class FocusTimer
{
    private DateTimeOffset _deadline;
    private DateTimeOffset _observed;
    private TimeSpan _remaining;
    private TimeSpan _duration;
    private TimeSpan _breakDuration;
    public FocusPhase Phase { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsActive => Phase is FocusPhase.Focus or FocusPhase.Break;
    public TimeSpan Duration => IsActive ? _duration : TimeSpan.Zero;
    public DateTimeOffset? EstimatedEndUtc => IsActive && !IsPaused ? _deadline.ToUniversalTime() : null;

    public double Progress(DateTimeOffset now)
    {
        if (Phase == FocusPhase.Completed) return 1;
        if (!IsActive) return 0;
        return Math.Clamp(1d - Remaining(now).Ticks / (double)_duration.Ticks, 0, 1);
    }

    public void Start(DateTimeOffset now, int focusMinutes, int breakMinutes)
    {
        _observed = now;
        _duration = TimeSpan.FromMinutes(Math.Clamp(focusMinutes, 1, 120));
        _breakDuration = TimeSpan.FromMinutes(Math.Clamp(breakMinutes, 1, 60));
        _deadline = now + _duration;
        _remaining = _duration;
        Phase = FocusPhase.Focus;
        IsPaused = false;
    }
    public TimeSpan Remaining(DateTimeOffset now)
    {
        if (!IsActive) return TimeSpan.Zero;
        if (IsPaused) return _remaining;
        now = Observe(now);
        return TimeSpan.FromTicks(Math.Clamp((_deadline - now).Ticks, 0, _duration.Ticks));
    }
    public FocusPhase? Tick(DateTimeOffset now)
    {
        if (!IsActive || IsPaused) return null;
        now = Observe(now);
        if (now < _deadline) return null;
        if (Phase == FocusPhase.Focus)
        {
            Phase = FocusPhase.Break;
            _duration = _breakDuration;
            _deadline = now + _duration;
        }
        else Phase = FocusPhase.Completed;
        return Phase;
    }
    public FocusPhase? Pause(DateTimeOffset now)
    {
        var changed = Tick(now);
        if (IsActive && !IsPaused) { _remaining = Remaining(now); IsPaused = true; }
        return changed;
    }
    public void Resume(DateTimeOffset now)
    {
        if (!IsActive || !IsPaused) return;
        now = Observe(now);
        _deadline = now + _remaining;
        IsPaused = false;
    }
    public void Stop() { Phase = FocusPhase.Stopped; IsPaused = false; }
    private DateTimeOffset Observe(DateTimeOffset now)
    {
        if (now > _observed) _observed = now;
        return _observed;
    }
}
