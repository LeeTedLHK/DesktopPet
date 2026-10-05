using System.Diagnostics;
using System.Windows.Threading;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

namespace DesktopPet.Behaviors;

public sealed class BehaviorManager : IDisposable
{
    private readonly PetWindow _window;
    private readonly PetStateMachine _stateMachine;
    private readonly ScreenBoundsService _screen;
    private readonly Func<AppSettings> _settings;
    private readonly Func<bool> _tired;
    private readonly Random _random = new();
    private readonly DispatcherTimer _decisionTimer;
    private readonly DispatcherTimer _movementTimer;
    private readonly DispatcherTimer _sleepTimer;
    private readonly DispatcherTimer _contextTimer;
    private readonly Stopwatch _movementClock = new();
    private double _targetX;
    private double _lastElapsed;
    private bool _suspended;
    private bool _disposed;
    private bool _nightSleep;
    private PetState? _timedAction;
    private DateTime _wakeUntil;
    private DateTime _nextLook;
    private System.Windows.Point? _lastCursor;

    public event Action<string>? SpeechRequested;

    public event Action? MovementFinished;
    public event Action? HomeSleepRequested;

    public BehaviorManager(PetWindow window, PetStateMachine stateMachine,
        ScreenBoundsService screen, Func<AppSettings> settings, Func<bool>? tired = null)
    {
        _window = window;
        _stateMachine = stateMachine;
        _screen = screen;
        _settings = settings;
        _tired = tired ?? (() => false);
        _decisionTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
        _decisionTimer.Tick += OnDecision;
        _movementTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1d / 30)
        };
        _movementTimer.Tick += OnMovement;
        _sleepTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
        _sleepTimer.Tick += OnSleep;
        _contextTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        { Interval = TimeSpan.FromMilliseconds(500) };
        _contextTimer.Tick += OnContext;
    }

    private bool CanSchedule => !_disposed && !_suspended && _stateMachine.IsVisible &&
        _stateMachine.AutoBehaviorEnabled && _stateMachine.State == PetState.Idle;

    public void Suspend(bool preserveSleep = false)
    {
        _suspended = true;
        if (preserveSleep && IsContinuingSleep)
        {
            _decisionTimer.Stop();
            _movementTimer.Stop();
            _contextTimer.Stop();
            return;
        }
        StopTimers();
    }

    public void Resume(TimeSpan? minimumDelay = null)
    {
        if (_disposed) return;
        _suspended = false;
        Refresh(minimumDelay);
    }

    public void Refresh(TimeSpan? minimumDelay = null)
    {
        if (!_disposed && IsContinuingSleep)
        {
            // Passive menu/settings changes keep the existing sleep and nap
            // deadline. Explicit interaction, hiding and disposal still reset it.
            _decisionTimer.Stop();
            _movementTimer.Stop();
            _contextTimer.Stop();
            if (!_suspended && _stateMachine.AutoBehaviorEnabled) _contextTimer.Start();
            return;
        }
        StopTimers();
        Schedule(minimumDelay);
        if (!_disposed && !_suspended && _stateMachine.IsVisible && _stateMachine.AutoBehaviorEnabled)
            _contextTimer.Start();
    }

    private bool IsContinuingSleep
    {
        get
        {
            if (!_stateMachine.IsVisible || _stateMachine.State != PetState.Sleeping) return false;
            if (_timedAction == PetState.Sleeping) return true;
            var settings = _settings();
            return _nightSleep && settings.NightSleepEnabled && CompanionPolicy.IsSleepTime(DateTime.Now.Hour,
                settings.NightSleepStartHour, settings.NightSleepEndHour);
        }
    }

    private void StopTimers()
    {
        _decisionTimer.Stop();
        _movementTimer.Stop();
        _sleepTimer.Stop();
        _contextTimer.Stop();
        _nightSleep = false;
        _timedAction = null;
        _movementClock.Reset();
        _stateMachine.CompleteWalking();
        _stateMachine.CompleteSleeping();
        _stateMachine.CancelNaturalAction();
    }

    private void Schedule(TimeSpan? minimumDelay = null)
    {
        if (!CanSchedule) return;
        var options = _settings();
        var delay = BehaviorPolicy.NextWait(_random, options.BehaviorFrequency) * PersonalityPolicy.WaitMultiplier(options.PetPersonality);
        if (minimumDelay.HasValue && delay < minimumDelay.Value) delay = minimumDelay.Value;
        _decisionTimer.Interval = delay;
        _decisionTimer.Start();
    }

    private void OnDecision(object? sender, EventArgs e)
    {
        _decisionTimer.Stop();
        if (!CanSchedule) return;
        var options = _settings();
        var action = PersonalityPolicy.NextAction(_random, options.PetPersonality, options.NaturalBehaviorsEnabled,
            options.GrowthEnabled && _tired());
        if (action == PetState.Idle)
        {
            Schedule();
            return;
        }

        if (action is PetState.Sleeping or PetState.Stretching or PetState.Yawning)
        {
            if (!_stateMachine.TryBeginNaturalAction(action))
            {
                Schedule();
                return;
            }

            _timedAction = action;
            _sleepTimer.Interval = action == PetState.Sleeping ? BehaviorPolicy.SleepDuration(_random) :
                _settings().EnableAnimations ? _window.ExpectedAnimationDuration(action) + TimeSpan.FromSeconds(1) :
                TimeSpan.FromSeconds(2.5);
            _sleepTimer.Start();
            return;
        }

        var direction = action == PetState.WalkingLeft ? -1 : 1;
        var position = _screen.Position(_window);
        _targetX = _screen.Clamp(new PetPoint(position.X +
            direction * BehaviorPolicy.NextDistance(_random), position.Y),
            new PetSize(_window.Width, _window.Height)).X;
        if (Math.Abs(_targetX - position.X) < 1 || !_stateMachine.TryBeginWalk(action))
        {
            Schedule();
            return;
        }

        _lastElapsed = 0;
        _movementClock.Restart();
        _movementTimer.Start();
    }

    private void OnMovement(object? sender, EventArgs e)
    {
        if (_disposed || _suspended || !_stateMachine.IsVisible || !_stateMachine.AutoBehaviorEnabled ||
            _stateMachine.State is not (PetState.WalkingLeft or PetState.WalkingRight))
        {
            Refresh();
            return;
        }

        var elapsed = _movementClock.Elapsed.TotalSeconds;
        var delta = Math.Clamp(elapsed - _lastElapsed, 0, 0.1);
        _lastElapsed = elapsed;
        var position = _screen.Position(_window);
        var remaining = _targetX - position.X;
        var step = BehaviorPolicy.PixelsPerSecond(_settings().MoveSpeed) * delta;
        var finished = Math.Abs(remaining) <= step;
        var nextX = finished ? _targetX : position.X + Math.Sign(remaining) * step;
        var point = _screen.Clamp(new PetPoint(nextX, position.Y),
            new PetSize(_window.Width, _window.Height));
        _screen.Move(_window, point);
        if (finished || Math.Abs(point.X - nextX) > 0.1)
        {
            _movementTimer.Stop();
            _movementClock.Reset();
            _stateMachine.CompleteWalking();
            MovementFinished?.Invoke();
            Schedule();
        }
    }

    private void OnSleep(object? sender, EventArgs e)
    {
        _sleepTimer.Stop();
        if (_timedAction is { } action) _stateMachine.CompleteTemporaryAction(action);
        _timedAction = null;
        Schedule();
    }

    public void DelayNightSleep()
    {
        _wakeUntil = DateTime.UtcNow.AddSeconds(15);
        _nextLook = DateTime.UtcNow.AddSeconds(12);
    }

    public void ActionCompleted()
    {
        _sleepTimer.Stop();
        _timedAction = null;
        Schedule(TimeSpan.FromSeconds(3));
    }

    public void StartHomeSleep()
    {
        if (_disposed) return;
        StopTimers();
        _suspended = false;
        if (!_stateMachine.TryBeginUserSleeping()) return;
        var settings = _settings();
        _nightSleep = _stateMachine.AutoBehaviorEnabled && settings.NightSleepEnabled &&
            CompanionPolicy.IsSleepTime(DateTime.Now.Hour, settings.NightSleepStartHour, settings.NightSleepEndHour);
        if (_stateMachine.AutoBehaviorEnabled) _contextTimer.Start();
        if (!_nightSleep) { _timedAction = PetState.Sleeping; _sleepTimer.Interval = TimeSpan.FromMinutes(1); _sleepTimer.Start(); }
    }

    public void PlayReminderAction()
    {
        if (!CanSchedule || !_settings().NaturalBehaviorsEnabled) return;
        if (!_stateMachine.TryBeginNaturalAction(PetState.Stretching)) return;
        _decisionTimer.Stop();
        _timedAction = PetState.Stretching;
        _sleepTimer.Interval = _settings().EnableAnimations ?
            _window.ExpectedAnimationDuration(PetState.Stretching) + TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(2.5);
        _sleepTimer.Start();
    }

    private void OnContext(object? sender, EventArgs e)
    {
        if (_disposed || _suspended || !_stateMachine.IsVisible || !_stateMachine.AutoBehaviorEnabled) return;
        var settings = _settings();
        var hasCursor = NativeWindowService.TryGetCursorPosition(out var cursor);
        var moved = hasCursor && _lastCursor.HasValue && (cursor - _lastCursor.Value).Length > 3;
        if (hasCursor) _lastCursor = cursor;
        var night = settings.NightSleepEnabled && CompanionPolicy.IsSleepTime(DateTime.Now.Hour,
            settings.NightSleepStartHour, settings.NightSleepEndHour);
        if (_nightSleep)
        {
            if (night && _stateMachine.State == PetState.Sleeping) return;
            _nightSleep = false;
            _stateMachine.CompleteSleeping();
            Schedule(TimeSpan.FromSeconds(3));
        }
        if (night && DateTime.UtcNow >= _wakeUntil && CanSchedule && settings.HomeEnabled && HomeSleepRequested is not null)
        {
            HomeSleepRequested.Invoke();
            return;
        }
        if (night && DateTime.UtcNow >= _wakeUntil && CanSchedule &&
            _stateMachine.TryBeginNaturalAction(PetState.Sleeping))
        {
            _decisionTimer.Stop();
            _sleepTimer.Stop();
            _timedAction = null;
            _nightSleep = true;
            SpeechRequested?.Invoke("夜深啦，我先睡一会儿。摸摸头就能叫醒我～");
            return;
        }
        if (!CanSchedule || night || !settings.CursorTrackingEnabled ||
            DateTime.UtcNow < _nextLook || !hasCursor) return;
        if (!moved) return;
        var local = _window.PointFromScreen(cursor);
        var direction = CompanionPolicy.LookDirection(new(_window.Width / 2, _window.Height / 2), new(local.X, local.Y));
        if (direction is not { } action || !_stateMachine.TryBeginNaturalAction(action)) return;
        _decisionTimer.Stop();
        _timedAction = action;
        _sleepTimer.Interval = TimeSpan.FromSeconds(2);
        _sleepTimer.Start();
        _nextLook = DateTime.UtcNow + PersonalityPolicy.LookCooldown(settings.PetPersonality);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTimers();
        _decisionTimer.Tick -= OnDecision;
        _movementTimer.Tick -= OnMovement;
        _sleepTimer.Tick -= OnSleep;
        _contextTimer.Tick -= OnContext;
        SpeechRequested = null;
    }
}
