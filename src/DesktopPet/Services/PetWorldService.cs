using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Views;
using DesktopPet.Behaviors;

namespace DesktopPet.Services;

public sealed class PetWorldService : IDisposable
{
    private readonly PetWindow _pet;
    private readonly ScreenBoundsService _screen;
    private readonly PetStateMachine _state;
    private readonly Func<AppSettings> _settings;
    private readonly Window _toy;
    private readonly Window _home;
    private readonly Window _feedingBowl;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private ToySimulation? _simulation;
    private FetchSequence? _fetch;
    private PetPoint? _dropStart;
    private PetPoint? _deliveredToy;
    private ToyKind _kind;
    private bool _returningHome;
    private long _followStarted;
    private long _pausedAt;
    private bool _disposed;
    private double _lastElapsed;
    private double? _feedingAnchorX;
    public bool IsMoving => _timer.IsEnabled;
    public bool HasActivity => _fetch is not null || _returningHome || IsFollowingCursor;
    public bool IsPaused { get; private set; }
    public bool IsToyVisible => _toy.IsVisible;
    public bool IsHomeVisible => _home.IsVisible;
    public bool IsFollowingCursor { get; private set; }
    public FetchPhase? CurrentFetchPhase => _fetch?.Phase;
    public event Action<ToyKind>? ToyCaught;
    public event Action<FetchPhase>? FetchPhaseChanged;
    public event Action? HomeReached;
    public event Action? FollowFinished;

    public PetWorldService(PetWindow pet, ScreenBoundsService screen, PetStateMachine state, Func<AppSettings> settings)
    {
        _pet = pet; _screen = screen; _state = state; _settings = settings;
        _toy = PropWindow("DesktopPet 玩具", 32, 32);
        _home = PropWindow("DesktopPet 小窝", 210, 90);
        _feedingBowl = PropWindow("DesktopPet 食盆", 84, 32);
        _feedingBowl.Content = FeedingBowlVisualFactory.Create();
        _timer = new DispatcherTimer(DispatcherPriority.Background, pet.Dispatcher) { Interval = TimeSpan.FromSeconds(1d / 30) };
        _timer.Tick += OnTick;
    }

    private static Window PropWindow(string title, double width, double height)
    {
        var window = new Window { Title = title, Width = width, Height = height, AllowsTransparency = true,
            WindowStyle = WindowStyle.None, Background = Brushes.Transparent, ShowInTaskbar = false,
            ShowActivated = false, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.Manual,
            IsHitTestVisible = false };
        window.SourceInitialized += (_, _) => NativeWindowService.SetClickThrough(window, true);
        return window;
    }
    private PetSize PetSize => new(_pet.Width, _pet.Height);
    private WorkArea Area => _screen.ActivityWorkArea(PetSize);
    public PetPoint HomeTarget => WorldGeometry.HomePosition(_settings(), PetSize, Area);

    public void Refresh()
    {
        Cancel();
        RefreshAppearance();
    }
    public void RefreshAppearance()
    {
        _toy.Topmost = _settings().Topmost;
        RefreshFeedingBowl();
        if (_disposed || !_pet.IsVisible || !_settings().HomeEnabled) { _home.Hide(); return; }
        var area = Area;
        _home.Width = Math.Max(1, Math.Min(210, area.Width));
        _home.Height = Math.Max(1, Math.Min(90, area.Height));
        _home.Content = HomeArt(_settings().HomeTheme);
        _home.Topmost = _settings().Topmost;
        var target = HomeTarget;
        var point = ScreenGeometry.Clamp(new(target.X + (_pet.Width - _home.Width) / 2,
            target.Y + _pet.Height - _home.Height + 10), new(_home.Width, _home.Height), area);
        _home.Show();
        Place(_home, point);
    }
    public void RefreshFeedingBowl()
    {
        if (_disposed) return;
        if (!_pet.IsVisible || !_state.IsVisible || _state.State != PetState.Feeding)
        {
            _feedingAnchorX = null;
            _feedingBowl.Hide();
            return;
        }
        var area = Area;
        if (_feedingBowl.Owner is null) _feedingBowl.Owner = _pet;
        _feedingAnchorX ??= _pet.CurrentMouthAnchor.X;
        _feedingBowl.Width = Math.Max(1, Math.Min(_pet.Width * .30 * _settings().FeedingBowlScale,
            Math.Min(area.Width, area.Height * 84 / 32)));
        _feedingBowl.Height = Math.Max(1, _feedingBowl.Width * 32 / 84);
        _feedingBowl.Topmost = _settings().Topmost;
        var petPosition = _screen.Position(_pet);
        var point = ScreenGeometry.Clamp(new(petPosition.X + 4 + (_pet.Width - 8) * _feedingAnchorX.Value - _feedingBowl.Width / 2,
            petPosition.Y + 4 + (_pet.Height - 8) * .92 - _feedingBowl.Height),
            new(_feedingBowl.Width, _feedingBowl.Height), area);
        _feedingBowl.Show();
        Place(_feedingBowl, point);
    }
    public bool Throw(ToyKind kind)
    {
        if (_disposed || !_pet.IsVisible || !_settings().ToysEnabled || _state.State == PetState.Dragging) return false;
        Cancel();
        var area = Area;
        var position = ScreenGeometry.Clamp(_screen.Position(_pet) with { Y = area.Bottom - _pet.Height }, PetSize, area);
        _screen.Move(_pet, position);
        var direction = position.X + _pet.Width / 2 > area.Left + area.Width / 2 ? -1 : 1;
        if (!_state.TryBeginUserMovement(direction < 0 ? PetState.RunningLeft : PetState.RunningRight)) return false;
        _kind = kind;
        var toyScale = kind == ToyKind.Ball ? _settings().BallScale : _settings().FrisbeeScale;
        var diameter = PixelDiameter(ToySimulation.Size * Math.Max(0, _pet.Width - 8) / 160 * toyScale);
        FetchSequence CreateFetch(double size) => new(kind, position, PetSize, area, direction < 0,
            Math.Max(150, BehaviorPolicy.PixelsPerSecond(_settings().MoveSpeed) * 3.2),
            _settings().AnimationSpeedMultiplier, size, _pet.ExpectedAnimationDuration(PetState.Happy).TotalSeconds);
        _fetch = CreateFetch(diameter);
        // Transparent HWND creation rounds dimensions to whole physical pixels.
        // Apply that rounding to physics too, including a portrait skin's floor cap.
        var fittedDiameter = PixelDiameter(_fetch.Simulation.Diameter);
        if (Math.Abs(fittedDiameter - _fetch.Simulation.Diameter) > .000001)
            _fetch = CreateFetch(fittedDiameter);
        _simulation = _fetch.Simulation;
        _toy.Width = _toy.Height = _simulation.Diameter;
        _toy.Content = new Viewbox { Child = ToyVisualFactory.Create(kind), Stretch = Stretch.Uniform };
        _toy.Topmost = _settings().Topmost;
        _toy.Show();
        Place(_toy, _fetch.ToyPosition);
        Start();
        return true;
    }
    private double PixelDiameter(double diameter) => Math.Floor(diameter * _screen.CurrentMonitor.Scale + 1e-9)
        / _screen.CurrentMonitor.Scale;
    public bool ReturnHome()
    {
        if (_disposed || !_pet.IsVisible || !_settings().HomeEnabled || _state.State == PetState.Dragging) return false;
        Cancel();
        var target = HomeTarget;
        if (!_state.TryBeginUserMovement(target.X < _screen.Position(_pet).X ? PetState.WalkingLeft : PetState.WalkingRight)) return false;
        _returningHome = true;
        Start();
        return true;
    }
    public bool FollowCursor()
    {
        if (_disposed || !_pet.IsVisible || _state.State == PetState.Dragging) return false;
        Cancel();
        IsFollowingCursor = true;
        _followStarted = Stopwatch.GetTimestamp();
        Start();
        return true;
    }
    private void Start() { IsPaused = false; _lastElapsed = 0; _clock.Restart(); _timer.Start(); }
    public void Pause()
    {
        if (_disposed || !HasActivity || IsPaused) return;
        IsPaused = true;
        _pausedAt = Stopwatch.GetTimestamp();
        _timer.Stop();
        _clock.Stop();
    }
    public void Resume()
    {
        if (_disposed || !HasActivity || !IsPaused) return;
        if (IsFollowingCursor) _followStarted += Stopwatch.GetTimestamp() - _pausedAt;
        IsPaused = false;
        _lastElapsed = _clock.Elapsed.TotalSeconds;
        _clock.Start();
        _timer.Start();
    }
    private void Place(Window prop, PetPoint point) => NativeWindowService.Place(prop,
        _screen.CurrentMonitor.ToPixels(point), _screen.CurrentMonitor.Scale);
    private void OnTick(object? sender, EventArgs e)
    {
        if (IsPaused) return;
        var validState = _state.State is PetState.WalkingLeft or PetState.WalkingRight or
            PetState.RunningLeft or PetState.RunningRight ||
            (_fetch is not null && PetStateMachine.IsFetchAction(_state.State)) ||
            (IsFollowingCursor && _state.State is PetState.Idle or PetState.Paused);
        if (_disposed || !_pet.IsVisible || !validState) { Cancel(); return; }
        var elapsed = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(elapsed - _lastElapsed, 0, .1); _lastElapsed = elapsed;
        if (_fetch is not null) { AdvanceFetch(dt); return; }
        var position = _screen.Position(_pet);
        PetPoint target;
        if (IsFollowingCursor)
        {
            if (Stopwatch.GetElapsedTime(_followStarted).TotalSeconds >= 30)
            { Cancel(); FollowFinished?.Invoke(); return; }
            if (!NativeWindowService.TryGetCursorPosition(out var pixels)) { _state.CompleteWalking(); return; }
            var cursor = _screen.CurrentMonitor.ToDip(new(pixels.X, pixels.Y));
            var bounds = _screen.WorkArea;
            if (cursor.X < bounds.Left || cursor.X > bounds.Right || cursor.Y < bounds.Top || cursor.Y > bounds.Bottom)
            { _state.CompleteWalking(); return; }
            target = FollowPosition(cursor, position, PetSize, Area);
        }
        else if (_returningHome) target = HomeTarget;
        else { Cancel(); return; }
        var dx = target.X - position.X; var dy = target.Y - position.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (IsFollowingCursor && distance <= 2) { _state.CompleteWalking(); return; }
        var speed = Math.Max(100, BehaviorPolicy.PixelsPerSecond(_settings().MoveSpeed) * 1.8);
        var step = speed * dt;
        var arrived = distance <= Math.Max(step, 2);
        var next = arrived ? target : new PetPoint(position.X + dx / distance * step, position.Y + dy / distance * step);
        // Keep the current facing near a vertical target, avoiding a last-frame
        // direction flip when a bouncing toy sits directly above the pet.
        var left = dx < -2 || Math.Abs(dx) <= 2 &&
            _state.State is PetState.WalkingLeft or PetState.RunningLeft;
        _state.TryBeginUserMovement(left ? PetState.WalkingLeft : PetState.WalkingRight);
        _screen.Move(_pet, _screen.Clamp(next, PetSize));
        if (IsFollowingCursor) return;
        if (!arrived) return;
        Cancel();
        HomeReached?.Invoke();
    }

    private void AdvanceFetch(double seconds)
    {
        if (_fetch is null || IsPaused) return;
        var valid = _state.State is PetState.RunningLeft or PetState.RunningRight || PetStateMachine.IsFetchAction(_state.State);
        if (_disposed || !_pet.IsVisible || !valid) { Cancel(); return; }
        var previous = _fetch.Phase;
        _fetch.Advance(seconds);
        if (_fetch.IsComplete)
        {
            var kind = _kind;
            Cancel();
            ToyCaught?.Invoke(kind);
            return;
        }
        var left = _fetch.FacingLeft;
        var pose = _fetch.Phase switch
        {
            FetchPhase.Jumping => left ? PetState.JumpingLeft : PetState.JumpingRight,
            FetchPhase.PickingUp => left ? PetState.PickingUpLeft : PetState.PickingUpRight,
            FetchPhase.Returning => left ? PetState.CarryingLeft : PetState.CarryingRight,
            FetchPhase.Dropping => left ? PetState.DroppingLeft : PetState.DroppingRight,
            FetchPhase.Celebrating => PetState.Celebrating,
            _ => left ? PetState.RunningLeft : PetState.RunningRight,
        };
        if (PetStateMachine.IsFetchAction(pose)) _state.TryBeginFetchAction(pose);
        else _state.TryBeginUserMovement(pose);
        _screen.Move(_pet, _fetch.PetPosition);
        if (_fetch.Phase is FetchPhase.Jumping or FetchPhase.PickingUp or FetchPhase.Dropping or FetchPhase.Celebrating)
            _pet.UpdateFetchProgress(_fetch.PhaseProgress);
        var toyPosition = _fetch.ToyPosition;
        var mouth = _pet.CurrentMouthAnchor;
        var diameter = _fetch.Simulation.Diameter;
        var toyScale = diameter / ToySimulation.Size;
        var petPosition = _screen.Position(_pet);
        var mouthToy = ScreenGeometry.Clamp(new(petPosition.X + 4 + (_pet.Width - 8) * mouth.X - diameter / 2 + (left ? -5 : 5) * toyScale,
            Math.Min(_fetch.GroundLine - diameter, petPosition.Y + 4 + (_pet.Height - 8) * mouth.Y - diameter / 4)),
            new(diameter, diameter), Area);
        if (_fetch.ToyIsAttached) toyPosition = mouthToy;
        if (_fetch.Phase == FetchPhase.Dropping)
        {
            if (_fetch.PhaseProgress < .5) toyPosition = mouthToy;
            else
            {
                _dropStart ??= mouthToy;
                _deliveredToy ??= ScreenGeometry.Clamp(new(_dropStart.Value.X,
                    _fetch.GroundLine - ToyVisualFactory.GroundContactY(_kind, diameter)),
                    new(diameter, diameter), Area);
                var drop = Math.Clamp((_fetch.PhaseProgress - .5) * 2, 0, 1);
                toyPosition = new(_dropStart.Value.X, _dropStart.Value.Y + (_deliveredToy.Value.Y - _dropStart.Value.Y) * drop * drop);
            }
        }
        if (_fetch.Phase == FetchPhase.Celebrating && _deliveredToy is { } delivered) toyPosition = delivered;
        Place(_toy, toyPosition);
        if (_toy.Content is FrameworkElement art) art.RenderTransform = new RotateTransform(
            _fetch.ToyIsAttached || _fetch.Phase is FetchPhase.Dropping or FetchPhase.Celebrating ? 0 : _fetch.ToyAngle,
            diameter / 2, diameter / 2);
        if (_fetch.Phase != previous) FetchPhaseChanged?.Invoke(_fetch.Phase);
    }
    public void Cancel()
    {
        IsPaused = false;
        _pausedAt = 0;
        _timer.Stop(); _clock.Reset(); _fetch?.Cancel(); _fetch = null; _dropStart = null; _deliveredToy = null;
        _simulation = null; _returningHome = false; IsFollowingCursor = false;
        _toy.Hide(); _state.CompleteWalking(); _state.CompleteFetchAction();
    }
    public void Hide() { Cancel(); _home.Hide(); _feedingBowl.Hide(); _feedingAnchorX = null; }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Hide(); _timer.Tick -= OnTick; _toy.Close(); _home.Close(); _feedingBowl.Close();
        ToyCaught = null; FetchPhaseChanged = null; HomeReached = null; FollowFinished = null;
    }
    public static PetPoint FollowPosition(PetPoint cursor, PetPoint position, PetSize size, WorkArea area)
    {
        var dx = cursor.X - (position.X + size.Width / 2);
        var dy = cursor.Y - (position.Y + size.Height / 2);
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var radius = Math.Sqrt(size.Width * size.Width + size.Height * size.Height) / 2 + 24;
        if (!double.IsFinite(distance) || distance <= radius + 4) return position;
        return ScreenGeometry.Clamp(new(position.X + dx / distance * (distance - radius),
            position.Y + dy / distance * (distance - radius)), size, area);
    }
    private static FrameworkElement HomeArt(HomeTheme theme)
    {
        return PetHomeVisualFactory.Create(theme);
    }
}
