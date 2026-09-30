using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Behaviors;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

namespace DesktopPet.Controllers;

public sealed class PetController : IDisposable
{
    private readonly PetWindow _window;
    private readonly ScreenBoundsService _screen;
    private readonly SettingsService _settingsService;
    private readonly PetStateMachine _state;
    private readonly BehaviorManager _behavior;
    private readonly DispatcherTimer _saveTimer;
    private Point? _pointerOrigin;
    private PetPoint _windowOrigin;
    private bool _menuOpen;
    private bool _disposed;

    public AppSettings Settings { get; private set; }
    public bool IsPetVisible => _state.IsVisible;
    public event Action<AppSettings>? SettingsChanged;
    public event Action? VisibilityChanged;
    public event Action<bool>? SettingsSaved;

    public PetController(PetWindow window, ScreenBoundsService screen, SettingsService settingsService,
        AppSettings settings, Action showSettings, Action exit)
    {
        _window = window;
        _screen = screen;
        _settingsService = settingsService;
        Settings = settings.Normalize();
        _state = new PetStateMachine(Settings.AutoBehaviorEnabled);
        _state.SetVisible(false);
        _state.StateChanged += OnStateChanged;
        _behavior = new BehaviorManager(window, _state, screen, () => Settings);
        _behavior.MovementFinished += ScheduleSave;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveTimer.Tick += OnSaveTimer;

        _window.SetPetScale(Settings.PetScale);
        _window.SetAnimationSettings(Settings.EnableAnimations, Settings.AnimationSpeedMultiplier);
        _window.Topmost = Settings.Topmost;
        SetPosition(_screen.Restore(Settings.LastPetX, Settings.LastPetY, WindowSize));
        _screen.BoundsChanged += OnBoundsChanged;
        _window.PetSurface.MouseLeftButtonDown += OnMouseDown;
        _window.PetSurface.MouseMove += OnMouseMove;
        _window.PetSurface.MouseLeftButtonUp += OnMouseUp;
        _window.PetSurface.LostMouseCapture += OnLostMouseCapture;
        _window.HideRequested += HidePet;
        _window.AnimationCompleted += OnAnimationCompleted;
        _window.PetMenu.Opened += OnMenuOpened;
        _window.PetMenu.Closed += OnMenuClosed;
        _window.PauseItem.Click += (_, _) => ToggleAutoBehavior();
        _window.TopmostItem.Click += (_, _) => ApplySettings(Settings with { Topmost = !Settings.Topmost });
        _window.SettingsItem.Click += (_, _) => showSettings();
        _window.HideItem.Click += (_, _) => HidePet();
        _window.ExitItem.Click += (_, _) => exit();
        RefreshMenu();
    }

    private PetSize WindowSize => new(_window.Width, _window.Height);

    public void ShowPet()
    {
        if (_disposed) return;
        ClampPosition();
        _window.Show();
        _state.SetVisible(true);
        _window.UpdateAnimation(_state.State, true);
        _behavior.Resume(TimeSpan.FromSeconds(3));
        VisibilityChanged?.Invoke();
    }

    public void HidePet()
    {
        if (_disposed) return;
        EndPointerInteraction(false);
        _behavior.Suspend();
        _state.SetVisible(false);
        _window.UpdateAnimation(_state.State, false);
        _window.Hide();
        SaveNow();
        VisibilityChanged?.Invoke();
    }

    public void ToggleAutoBehavior() =>
        ApplySettings(Settings with { AutoBehaviorEnabled = !Settings.AutoBehaviorEnabled });

    public void ApplySettings(AppSettings settings)
    {
        if (_disposed) return;
        Settings = settings.Normalize();
        _window.Topmost = Settings.Topmost;
        _behavior.Refresh();
        _window.SetPetScale(Settings.PetScale);
        _window.SetAnimationSettings(Settings.EnableAnimations, Settings.AnimationSpeedMultiplier);
        ClampPosition();
        _state.SetAutoBehaviorEnabled(Settings.AutoBehaviorEnabled);
        _behavior.Refresh(TimeSpan.FromSeconds(3));
        _window.UpdateAnimation(_state.State, _state.IsVisible);
        RefreshMenu();
        ScheduleSave();
        SettingsChanged?.Invoke(Settings);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_disposed || _menuOpen) return;
        _behavior.Suspend();
        _pointerOrigin = ScreenPoint(e);
        _windowOrigin = new PetPoint(_window.Left, _window.Top);
        if (!_window.PetSurface.CaptureMouse())
        {
            _pointerOrigin = null;
            _behavior.Resume(TimeSpan.FromSeconds(3));
        }
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_pointerOrigin is not { } origin) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndPointerInteraction(false);
            return;
        }
        var current = ScreenPoint(e);
        var delta = current - origin;
        if (_state.State != PetState.Dragging && delta.Length < 5) return;
        _state.BeginDrag();
        SetPosition(_screen.Clamp(new PetPoint(_windowOrigin.X + delta.X, _windowOrigin.Y + delta.Y), WindowSize));
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_pointerOrigin is null) return;
        EndPointerInteraction(true);
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e) => EndPointerInteraction(false);

    private Point ScreenPoint(MouseEventArgs e)
    {
        var pixelPoint = _window.PointToScreen(e.GetPosition(_window));
        var dpi = VisualTreeHelper.GetDpi(_window);
        return new Point(pixelPoint.X / dpi.DpiScaleX, pixelPoint.Y / dpi.DpiScaleY);
    }

    private void EndPointerInteraction(bool allowClick)
    {
        if (_pointerOrigin is null) return;
        var dragged = _state.State == PetState.Dragging;
        _pointerOrigin = null; // ReleaseMouseCapture synchronously raises LostMouseCapture.
        if (_window.PetSurface.IsMouseCaptured) _window.PetSurface.ReleaseMouseCapture();
        if (dragged)
        {
            ClampPosition();
            _state.EndDrag();
            ScheduleSave();
        }
        else if (allowClick)
        {
            if (Settings.EnableAnimations) _state.TryBeginHappy();
            _window.PlayClickFeedback();
        }
        if (!_menuOpen && !_disposed) _behavior.Resume(TimeSpan.FromSeconds(3));
    }

    private void OnMenuOpened(object? sender, RoutedEventArgs e)
    {
        _menuOpen = true;
        EndPointerInteraction(false);
        _behavior.Suspend();
        RefreshMenu();
    }

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        _menuOpen = false;
        if (!_disposed && _state.IsVisible) _behavior.Resume(TimeSpan.FromSeconds(3));
    }

    private void RefreshMenu()
    {
        _window.PauseItem.Header = Settings.AutoBehaviorEnabled ? "暂停自动行为" : "恢复自动行为";
        _window.TopmostItem.IsChecked = Settings.Topmost;
    }

    private void OnStateChanged(PetState state) => _window.UpdateAnimation(state, _state.IsVisible);

    private void OnAnimationCompleted(AnimationKind kind)
    {
        if (kind == AnimationKind.Happy) _state.CompleteHappy();
    }

    private void OnBoundsChanged()
    {
        EndPointerInteraction(false);
        _behavior.Refresh();
        _window.SetPetScale(Settings.PetScale);
        ClampPosition();
        ScheduleSave();
    }

    private void SetPosition(PetPoint point)
    {
        _window.Left = point.X;
        _window.Top = point.Y;
    }

    private void ClampPosition() => SetPosition(_screen.Clamp(new PetPoint(_window.Left, _window.Top), WindowSize));

    private void ScheduleSave()
    {
        if (_disposed) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTimer(object? sender, EventArgs e) => SaveNow();

    public void SaveNow()
    {
        _saveTimer.Stop();
        Settings = Settings with { LastPetX = _window.Left, LastPetY = _window.Top };
        SettingsSaved?.Invoke(_settingsService.Save(Settings));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        EndPointerInteraction(false);
        _behavior.Dispose();
        SaveNow();
        _window.UpdateAnimation(_state.State, false);
        _saveTimer.Tick -= OnSaveTimer;
        _screen.BoundsChanged -= OnBoundsChanged;
        _state.StateChanged -= OnStateChanged;
        _window.PetSurface.MouseLeftButtonDown -= OnMouseDown;
        _window.PetSurface.MouseMove -= OnMouseMove;
        _window.PetSurface.MouseLeftButtonUp -= OnMouseUp;
        _window.PetSurface.LostMouseCapture -= OnLostMouseCapture;
        _window.PetMenu.Opened -= OnMenuOpened;
        _window.PetMenu.Closed -= OnMenuClosed;
        _window.HideRequested -= HidePet;
        _window.AnimationCompleted -= OnAnimationCompleted;
        _window.DisposeAnimations();
    }
}
