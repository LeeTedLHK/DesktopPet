using System.Windows.Threading;
using DesktopPet.Companions;
using DesktopPet.Models;
using DesktopPet.Journal;

namespace DesktopPet.Services;

public sealed class CompanionService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly PetProfileService _profiles;
    private readonly CareTracker _care;
    private readonly ReminderScheduler _reminders = new();
    private readonly Func<bool> _visible;
    private readonly Func<bool> _sleeping;
    private readonly Func<bool> _canDeliver;
    private AppSettings _options;
    private DateTimeOffset _lastSave;
    private bool _disposed;
    public PetProfile Profile => _care.Profile with { Journal = Journal.Profile.Journal };
    public JournalTracker Journal { get; }
    public FocusTimer Focus { get; } = new();
    public bool IsQuietFocusActive => _options.FocusQuietEnabled && Focus.Phase == FocusPhase.Focus && !Focus.IsPaused;
    public DateTimeOffset? HealthSnoozedUntil => _reminders.HealthSnoozedUntil;
    public bool LastSaveSucceeded { get; private set; } = true;
    public event Action? Changed;
    public event Action<ReminderNotice>? ReminderRequested;
    public event Action<bool>? ProfileSaved;

    public CompanionService(Dispatcher dispatcher, PetProfileService profiles, AppSettings options,
        Func<bool> visible, Func<bool> sleeping, Func<bool> canDeliver)
    {
        _profiles = profiles;
        _options = options.Normalize();
        _visible = visible;
        _sleeping = sleeping;
        _canDeliver = canDeliver;
        var now = DateTimeOffset.UtcNow;
        _lastSave = now;
        _care = new(profiles.Load(), now);
        Journal = new(_care.Profile, _options.JournalEnabled, now.ToLocalTime());
        _reminders.Configure(_options, now);
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }
    private void OnTick(object? sender, EventArgs e) => Advance(DateTimeOffset.UtcNow);
    public void Advance(DateTimeOffset now)
    {
        if (_disposed) return;
        _care.Advance(now, _options.GrowthEnabled && _visible(), _sleeping());
        if (_visible()) Journal.Visit(now.ToLocalTime());
        QueueTransition(Focus.Tick(now), now);
        var notice = _reminders.Tick(now, _options.SpeechBubblesEnabled && _canDeliver(), !IsQuietFocusActive);
        if (notice is not null) ReminderRequested?.Invoke(notice);
        var saveInterval = LastSaveSucceeded ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(10);
        if (now - _lastSave >= saveInterval) Save(now);
        Changed?.Invoke();
    }
    public void UpdateSettings(AppSettings options)
    {
        if (_disposed) return;
        var now = DateTimeOffset.UtcNow;
        _care.Advance(now, _options.GrowthEnabled && _visible(), _sleeping());
        _options = options.Normalize();
        Journal.UpdateSettings(_options.JournalEnabled, now.ToLocalTime());
        _reminders.Configure(_options, now);
        Changed?.Invoke();
    }
    public void RecordInteraction(bool feeding, bool petting = true)
    {
        if (_disposed) return;
        var now = DateTimeOffset.UtcNow;
        _care.Advance(now, _options.GrowthEnabled && _visible(), _sleeping());
        if (feeding) _care.Feed(now, _options.GrowthEnabled);
        else _care.Pet(now, _options.GrowthEnabled);
        Journal.Record(feeding ? JournalAction.Feeding : petting ? JournalAction.Petting : JournalAction.Play, now.ToLocalTime());
        Save(now);
        Changed?.Invoke();
    }
    public void RecordHome()
    {
        if (_disposed) return;
        var now = DateTimeOffset.UtcNow;
        Journal.Record(JournalAction.Home, now.ToLocalTime());
        Save(now); Changed?.Invoke();
    }
    public void StartFocus()
    {
        if (_disposed || Focus.IsActive) return;
        _reminders.ClearFocus();
        Focus.Start(DateTimeOffset.UtcNow, _options.FocusMinutes, _options.BreakMinutes);
        Changed?.Invoke();
    }
    public void ToggleFocusPause()
    {
        if (_disposed || !Focus.IsActive) return;
        var now = DateTimeOffset.UtcNow;
        if (Focus.IsPaused) Focus.Resume(now);
        else QueueTransition(Focus.Pause(now), now);
        Changed?.Invoke();
    }
    public void StopFocus()
    {
        if (_disposed) return;
        Focus.Stop();
        _reminders.ClearFocus();
        Changed?.Invoke();
    }
    public void SnoozeHealthReminders(int minutes)
    {
        if (_disposed) return;
        _reminders.SnoozeHealth(DateTimeOffset.UtcNow, minutes);
        Changed?.Invoke();
    }
    public void ResumeHealthReminders()
    {
        if (_disposed) return;
        _reminders.ResumeHealth(DateTimeOffset.UtcNow);
        Changed?.Invoke();
    }
    private void QueueTransition(FocusPhase? phase, DateTimeOffset now)
    {
        if (phase == FocusPhase.Break)
        {
            Journal.Record(JournalAction.FocusCompleted, now.ToLocalTime());
            Save(now);
            _reminders.Enqueue(ReminderKind.FocusFinished, now);
        }
        else if (phase == FocusPhase.Completed) _reminders.Enqueue(ReminderKind.BreakFinished, now);
    }
    public void Save(DateTimeOffset? now = null)
    {
        _lastSave = now ?? DateTimeOffset.UtcNow;
        LastSaveSucceeded = _profiles.Save(Profile);
        ProfileSaved?.Invoke(LastSaveSucceeded);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        Focus.Stop();
        Save();
        Changed = null;
        ReminderRequested = null;
        ProfileSaved = null;
    }
}
