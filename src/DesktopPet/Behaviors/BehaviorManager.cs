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
    private readonly Random _random = new();
    private readonly DispatcherTimer _decisionTimer;
    private readonly DispatcherTimer _movementTimer;
    private readonly DispatcherTimer _sleepTimer;
    private readonly Stopwatch _movementClock = new();
    private double _targetX;
    private double _lastElapsed;
    private bool _suspended;
    private bool _disposed;

    public event Action? MovementFinished;

    public BehaviorManager(PetWindow window, PetStateMachine stateMachine,
        ScreenBoundsService screen, Func<AppSettings> settings)
    {
        _window = window;
        _stateMachine = stateMachine;
        _screen = screen;
        _settings = settings;
        _decisionTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
        _decisionTimer.Tick += OnDecision;
        _movementTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1d / 30)
        };
        _movementTimer.Tick += OnMovement;
        _sleepTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
        _sleepTimer.Tick += OnSleep;
    }

    private bool CanSchedule => !_disposed && !_suspended && _stateMachine.IsVisible &&
        _stateMachine.AutoBehaviorEnabled && _stateMachine.State == PetState.Idle;

    public void Suspend()
    {
        _suspended = true;
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
        StopTimers();
        Schedule(minimumDelay);
    }

    private void StopTimers()
    {
        _decisionTimer.Stop();
        _movementTimer.Stop();
        _sleepTimer.Stop();
        _movementClock.Reset();
        _stateMachine.CompleteWalking();
        _stateMachine.CompleteSleeping();
    }

    private void Schedule(TimeSpan? minimumDelay = null)
    {
        if (!CanSchedule) return;
        var delay = BehaviorPolicy.NextWait(_random, _settings().BehaviorFrequency);
        if (minimumDelay.HasValue && delay < minimumDelay.Value) delay = minimumDelay.Value;
        _decisionTimer.Interval = delay;
        _decisionTimer.Start();
    }

    private void OnDecision(object? sender, EventArgs e)
    {
        _decisionTimer.Stop();
        if (!CanSchedule) return;
        var action = BehaviorPolicy.NextIdleAction(_random);
        if (action == PetState.Idle)
        {
            Schedule();
            return;
        }

        if (action == PetState.Sleeping)
        {
            if (!_stateMachine.TryBeginSleeping())
            {
                Schedule();
                return;
            }

            _sleepTimer.Interval = BehaviorPolicy.SleepDuration(_random);
            _sleepTimer.Start();
            return;
        }

        var direction = action == PetState.WalkingLeft ? -1 : 1;
        _targetX = _screen.Clamp(new PetPoint(_window.Left +
            direction * BehaviorPolicy.NextDistance(_random), _window.Top),
            new PetSize(_window.Width, _window.Height)).X;
        if (Math.Abs(_targetX - _window.Left) < 1 || !_stateMachine.TryBeginWalk(action))
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
            StopTimers();
            return;
        }

        var elapsed = _movementClock.Elapsed.TotalSeconds;
        var delta = Math.Clamp(elapsed - _lastElapsed, 0, 0.1);
        _lastElapsed = elapsed;
        var remaining = _targetX - _window.Left;
        var step = BehaviorPolicy.PixelsPerSecond(_settings().MoveSpeed) * delta;
        var finished = Math.Abs(remaining) <= step;
        var nextX = finished ? _targetX : _window.Left + Math.Sign(remaining) * step;
        var point = _screen.Clamp(new PetPoint(nextX, _window.Top),
            new PetSize(_window.Width, _window.Height));
        _window.Left = point.X;
        _window.Top = point.Y;
        if (finished || Math.Abs(point.X - nextX) > 0.1)
        {
            StopTimers();
            MovementFinished?.Invoke();
            Schedule();
        }
    }

    private void OnSleep(object? sender, EventArgs e)
    {
        _sleepTimer.Stop();
        if (_disposed || _suspended || !_stateMachine.IsVisible ||
            !_stateMachine.AutoBehaviorEnabled || _stateMachine.State != PetState.Sleeping)
        {
            _stateMachine.CompleteSleeping();
            Schedule();
            return;
        }

        _stateMachine.CompleteSleeping();
        Schedule();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTimers();
        _decisionTimer.Tick -= OnDecision;
        _movementTimer.Tick -= OnMovement;
        _sleepTimer.Tick -= OnSleep;
    }
}
