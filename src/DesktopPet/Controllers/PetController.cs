using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Behaviors;
using DesktopPet.Companions;
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
    private readonly DispatcherTimer _interactionTimer;
    private readonly DispatcherTimer _challengeTimer;
    private readonly SpeechBubbleService _speech;
    private readonly PetWorldService _world;
    private readonly PetVisibility _visibility = new();
    private PetState? _interaction;
    private DateTime _lastFeed = DateTime.MinValue;
    private bool _fullscreen;
    private bool _clickedHead;
    private Point? _pointerOrigin;
    private PetPoint _windowOrigin;
    private bool _menuOpen;
    private bool _trayMenuOpen;
    private bool _disposed;
    private bool _settingsSaveSucceeded = true;
    private bool _savingAll;
    private bool _challengeActive;
    private int _challengeCaught;
    private bool _challengeRoundPending;
    private bool _quietFocusActive;
    public bool IsPlaying => _challengeActive || _world.HasActivity;
    public string PlayStatus { get; private set; } = "选一个玩法，陪我动一动吧～";
    public event Action? PlayStatusChanged;

    public AppSettings Settings { get; private set; }
    public bool IsPetVisible => _state.IsVisible;
    public bool WantsPetVisible => _visibility.RequestedVisible;
    public bool IsFullscreenSuppressed => _visibility.FullscreenSuppressed;
    public event Action<AppSettings>? SettingsChanged;
    public event Action? VisibilityChanged;
    public event Action<bool>? SettingsSaved;
    public CompanionService Companion { get; }
    public bool LastSaveSucceeded => _settingsSaveSucceeded && Companion.LastSaveSucceeded;
    public event Action? CompanionChanged;

    public PetController(PetWindow window, ScreenBoundsService screen, SettingsService settingsService,
        AppSettings settings, Action showSettings, Action exit, Action? showCompanion = null)
    {
        _window = window;
        _screen = screen;
        _settingsService = settingsService;
        Settings = settings.Normalize();
        _screen.Configure(() => Settings);
        Settings = Settings with { SelectedMonitorId = _screen.CurrentMonitor.Id };
        _state = new PetStateMachine(Settings.AutoBehaviorEnabled);
        _state.SetVisible(false);
        _state.StateChanged += OnStateChanged;
        _speech = new SpeechBubbleService(window, screen);
        _interactionTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
        _interactionTimer.Tick += OnInteractionTimeout;
        _challengeTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            { Interval = TimeSpan.FromSeconds(1.2) };
        _challengeTimer.Tick += OnChallengeRound;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveTimer.Tick += OnSaveTimer;
        var profileDirectory = Path.GetDirectoryName(settingsService.SettingsPath)!;
        Companion = new CompanionService(window.Dispatcher,
            new PetProfileService(profileDirectory, new AppLogger(profileDirectory)), Settings,
            () => _state.IsVisible, () => _state.State == PetState.Sleeping,
            () => _state.IsVisible && !_fullscreen && !_menuOpen && !_trayMenuOpen && _pointerOrigin is null &&
                _interaction is null && _state.State is PetState.Idle or PetState.Paused or PetState.Sleeping);
        Companion.Changed += OnCompanionChanged;
        Companion.ReminderRequested += DeliverReminder;
        Companion.ProfileSaved += OnProfileSaved;
        _behavior = new BehaviorManager(window, _state, screen, () => Settings,
            () => Companion.Profile.Energy < 25);
        _behavior.MovementFinished += ScheduleSave;
        _behavior.SpeechRequested += Say;
        _world = new PetWorldService(window, screen, _state, () => Settings);
        _world.ToyCaught += OnToyCaught;
        _world.FetchPhaseChanged += OnFetchPhaseChanged;
        _world.HomeReached += OnHomeReached;
        _world.FollowFinished += OnFollowFinished;
        _behavior.HomeSleepRequested += ReturnHome;

        _window.SetPetScale(Settings.PetScale, _screen.WorkArea);
        _window.SetAnimationSettings(Settings.EnableAnimations, Settings.AnimationSpeedMultiplier, Settings.PowerSavingEnabled);
        _window.Topmost = Settings.Topmost;
        _window.SetClickThrough(Settings.ClickThrough);
        _window.SetPreciseHitTest(Settings.PreciseHitTestEnabled);
        _screen.RestoreWindow(window);
        _screen.BoundsChanged += OnBoundsChanged;
        _screen.ActiveMonitorChanged += OnMonitorChanged;
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
        _window.ClickThroughItem.Click += (_, _) => ToggleClickThrough();
        _window.PetHeadItem.Click += (_, _) => PetHead();
        _window.FeedItem.Click += (_, _) => Feed();
        _window.CompanionItem.Click += (_, _) => (showCompanion ?? showSettings)();
        _window.FocusStartItem.Click += (_, _) => StartFocus();
        _window.FocusPauseItem.Click += (_, _) => ToggleFocusPause();
        _window.FocusStopItem.Click += (_, _) => StopFocus();
        _window.BallItem.Click += (_, _) => ThrowToy(ToyKind.Ball);
        _window.FrisbeeItem.Click += (_, _) => ThrowToy(ToyKind.Frisbee);
        _window.FollowItem.Click += (_, _) => FollowCursor();
        _window.ChallengeItem.Click += (_, _) => StartFetchChallenge();
        _window.StopPlayItem.Click += (_, _) => StopPlay();
        _window.PhotoItem.Click += (_, _) => SavePhoto();
        _window.HomeItem.Click += (_, _) => ReturnHome();
        _window.ResetItem.Click += (_, _) => ResetPosition();
        _window.SettingsItem.Click += (_, _) => showSettings();
        _window.HideItem.Click += (_, _) => HidePet();
        _window.ExitItem.Click += (_, _) => exit();
        RefreshMenu();
    }

    private PetSize WindowSize => new(_window.Width, _window.Height);

    private void ResumeBehavior(TimeSpan minimumDelay)
    {
        if (_disposed || IsPlaying) return;
        if (_menuOpen || _trayMenuOpen || _quietFocusActive) _behavior.Suspend(preserveSleep: true);
        else _behavior.Resume(minimumDelay);
    }

    public void ShowPet()
    {
        if (_disposed) return;
        _visibility.RequestVisible(true);
        ApplyVisibility();
    }

    public void HidePet()
    {
        if (_disposed) return;
        _visibility.RequestVisible(false);
        ApplyVisibility();
    }

    public void ToggleVisibility()
    {
        if (_visibility.RequestedVisible) HidePet();
        else ShowPet();
    }

    public void SetFullscreenSuppressed(bool fullscreen)
    {
        _fullscreen = fullscreen;
        _visibility.SetFullscreenSuppressed(Settings.FullscreenAutoHide && fullscreen);
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (_disposed) return;
        if (!_visibility.ShouldShow)
        {
            HideActualPet();
            return;
        }
        if (_state.IsVisible) { VisibilityChanged?.Invoke(); return; }
        ClampPosition();
        _window.Show();
        _state.SetVisible(true);
        _world.Refresh();
        _window.UpdateAnimation(_state.State, true);
        ResumeBehavior(TimeSpan.FromSeconds(3));
        VisibilityChanged?.Invoke();
    }

    private void HideActualPet()
    {
        if (_disposed) return;
        _interactionTimer.Stop();
        _interaction = null;
        _window.PetMenu.IsOpen = false;
        EndPointerInteraction(false);
        _speech.Hide();
        CancelPlay();
        _world.Hide();
        _behavior.Suspend();
        _state.SetVisible(false);
        _window.UpdateAnimation(_state.State, false);
        _window.Hide();
        SaveNow();
        VisibilityChanged?.Invoke();
    }

    public void ToggleAutoBehavior() =>
        ApplySettings(Settings with { AutoBehaviorEnabled = !Settings.AutoBehaviorEnabled });

    public void ToggleClickThrough() => ApplySettings(Settings with { ClickThrough = !Settings.ClickThrough });

    public void ResetPosition()
    {
        CancelPlay();
        ApplySettings(Settings with { ClickThrough = false });
        SetPosition(_screen.Restore(null, null, WindowSize));
        ShowPet();
        SaveNow();
    }

    public void SummonToCursor()
    {
        if (_disposed || !NativeWindowService.TryGetCursorPosition(out var pixels)) return;
        PrepareWorldAction();
        _behavior.DelayNightSleep();
        var cursorPixels = new PetPoint(pixels.X, pixels.Y);
        if (Settings.AllowCrossMonitorDrag)
            _screen.Select(MonitorGeometry.At(_screen.Monitors, cursorPixels).Id);
        var cursor = _screen.CurrentMonitor.ToDip(cursorPixels);
        SetPosition(_screen.Clamp(new(cursor.X + 20, cursor.Y + 20), WindowSize));
        ShowPet();
        ResumeBehavior(TimeSpan.FromSeconds(3));
        SaveNow();
    }

    private void OnMonitorChanged(string id)
    {
        CancelPlay();
        Settings = Settings with { SelectedMonitorId = id };
        _window.SetPetScale(Settings.PetScale, _screen.WorkArea);
        _world.Refresh();
        SettingsChanged?.Invoke(Settings);
    }

    public void PetHead() => Interact(PetState.Petting, "喜欢这样摸摸头，再来一下～");

    public void Feed()
    {
        if (!_state.IsVisible || _state.State == PetState.Dragging) return;
        if (DateTime.UtcNow - _lastFeed < TimeSpan.FromSeconds(8))
        {
            Say("刚刚吃过啦，让我慢慢嚼一会儿～");
            return;
        }
        if (Interact(PetState.Feeding, "开饭啦！谢谢你给我的小零食～")) _lastFeed = DateTime.UtcNow;
    }

    private bool Interact(PetState action, string text)
    {
        if (_disposed || !_state.IsVisible || _state.State == PetState.Dragging) return false;
        CancelPlay();
        _behavior.Suspend();
        _behavior.DelayNightSleep();
        _interactionTimer.Stop();
        _interaction = action;
        var started = _state.TryBeginInteraction(action);
        _window.PlayClickFeedback();
        Say(text);
        // A watchdog also completes static/missing/disabled animations and mid-action setting changes.
        if (started && _state.State == action)
        {
            _interactionTimer.Interval = Settings.EnableAnimations ?
                _window.ExpectedAnimationDuration(action) + TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(1.5);
            _interactionTimer.Start();
        }
        if (started) Companion.RecordInteraction(action == PetState.Feeding, action == PetState.Petting);
        if (!_menuOpen) ResumeBehavior(TimeSpan.FromSeconds(3));
        return started;
    }

    private void Say(string text)
    {
        if (Settings.SpeechBubblesEnabled && _state.IsVisible) _speech.Show(text);
    }

    public void StartFocus() => Companion.StartFocus();
    public void ToggleFocusPause() => Companion.ToggleFocusPause();
    public void StopFocus() => Companion.StopFocus();

    public void ThrowToy(ToyKind kind)
    {
        if (_disposed || !_state.IsVisible || _state.State == PetState.Dragging) return;
        if (!Settings.ToysEnabled) { Say("先在设置中开启玩具吧～"); return; }
        PrepareWorldAction();
        if (_world.Throw(kind))
        {
            PausePlayForMenu();
            SetPlayStatus(kind == ToyKind.Ball ? "正在追逐小球" : "正在追逐飞盘");
            Say(kind == ToyKind.Ball ? "小球飞出去啦，我来追！" : "飞盘转起来啦，等我接住～");
        }
        else ResumeBehavior(TimeSpan.FromSeconds(3));
    }

    public void ReturnHome()
    {
        if (_disposed || !_state.IsVisible || _state.State == PetState.Dragging) return;
        if (!Settings.HomeEnabled) { Say("先在设置里为我摆上小窝吧～"); return; }
        PrepareWorldAction();
        if (_world.ReturnHome()) { PausePlayForMenu(); SetPlayStatus("正在走回小窝"); Say("回我的小窝，舒舒服服休息一会儿～"); }
        else ResumeBehavior(TimeSpan.FromSeconds(3));
    }

    private void PrepareWorldAction()
    {
        CancelPlay();
        _interactionTimer.Stop();
        if (_interaction is {} action) _state.CompleteTemporaryAction(action);
        _interaction = null;
        _behavior.Suspend();
        _speech.Hide();
    }

    private void OnToyCaught(ToyKind kind)
    {
        if (_challengeActive)
        {
            _challengeCaught++;
            if (_challengeCaught < 3)
            {
                Companion.RecordInteraction(false, false);
                _state.TryBeginInteraction(PetState.Happy);
                SetPlayStatus($"接球挑战 {_challengeCaught}/3 · 稍歇一下，准备下一球");
                Say($"第 {_challengeCaught} 球接住了！下一球准备～");
                _challengeRoundPending = true;
                if (!_menuOpen && !_trayMenuOpen) _challengeTimer.Start();
                return;
            }
            _challengeActive = false;
            Interact(PetState.Happy, "三球都接到啦！今天的接球小冠军就是我～");
            SetPlayStatus("挑战完成 3/3 · 三球全中！");
            ScheduleSave();
            return;
        }
        Interact(PetState.Happy, kind == ToyKind.Ball ? "小球叼回来啦！再陪我玩一次吧～" : "飞盘叼回来啦，给我一个摸摸头吧～");
        SetPlayStatus(kind == ToyKind.Ball ? "小球叼回啦，随时可以再来一球～" : "飞盘叼回啦，随时可以再玩～");
        ScheduleSave();
    }

    private void OnFetchPhaseChanged(FetchPhase phase)
    {
        var text = phase switch
        {
            FetchPhase.Jumping => "跳起来接飞盘",
            FetchPhase.PickingUp => "低头把玩具捡起来",
            FetchPhase.Returning => "正在把玩具叼回给你",
            FetchPhase.Dropping => "到啦，轻轻放下玩具",
            FetchPhase.Celebrating => "玩具送到啦，摇摇尾巴等你再投",
            _ => "正在沿地面追逐玩具",
        };
        SetPlayStatus(_challengeActive ? $"接球挑战 {_challengeCaught}/3 · {text}" : text);
    }

    public void FollowCursor()
    {
        if (_disposed || !_state.IsVisible || _state.State == PetState.Dragging) return;
        PrepareWorldAction();
        if (_world.FollowCursor())
        {
            PausePlayForMenu();
            SetPlayStatus("散步中 · 跟随鼠标30秒，靠近后会停下来等你");
            Say("移动鼠标带我散步吧！我会在旁边等你～");
        }
        else ResumeBehavior(TimeSpan.FromSeconds(3));
    }

    private void OnFollowFinished()
    {
        SetPlayStatus("散步结束 · 谢谢你陪我走一走");
        ResumeBehavior(TimeSpan.FromSeconds(3));
        ScheduleSave();
    }

    public void StartFetchChallenge()
    {
        if (_disposed || !_state.IsVisible || _state.State == PetState.Dragging) return;
        if (!Settings.ToysEnabled) { Say("先在设置中开启玩具吧～"); return; }
        PrepareWorldAction();
        _challengeCaught = 0;
        _challengeActive = true;
        BeginChallengeRound();
    }

    private void OnChallengeRound(object? sender, EventArgs e)
    { _challengeTimer.Stop(); if (_challengeActive) BeginChallengeRound(); }

    private void BeginChallengeRound()
    {
        if (_disposed || !_state.IsVisible || _state.State == PetState.Dragging)
        { StopPlay(); return; }
        if (_menuOpen || _trayMenuOpen) { _challengeRoundPending = true; return; }
        _challengeRoundPending = false;
        _state.CompleteTemporaryAction(PetState.Happy);
        if (!_world.Throw(ToyKind.Ball)) { StopPlay(); return; }
        SetPlayStatus($"接球挑战 {_challengeCaught}/3 · 第 {_challengeCaught + 1} 球出发！");
        Say($"接球挑战，第 {_challengeCaught + 1} 球！");
    }

    private void CancelPlay()
    {
        var hadSession = IsPlaying;
        if (_challengeActive) _state.CompleteTemporaryAction(PetState.Happy);
        _challengeTimer.Stop();
        _challengeActive = false;
        _challengeRoundPending = false;
        _world.Cancel();
        if (hadSession) SetPlayStatus("玩法已结束，随时可以再来～");
    }

    public void StopPlay()
    {
        if (_disposed) return;
        CancelPlay();
        _state.CompleteTemporaryAction(PetState.Happy);
        ResumeBehavior(TimeSpan.FromSeconds(3));
        SetPlayStatus("玩法已结束，随时可以再来～");
        ScheduleSave();
    }

    private void SetPlayStatus(string text)
    { PlayStatus = text; RefreshMenu(); PlayStatusChanged?.Invoke(); }

    public void SavePhoto()
    {
        if (_disposed || _window.CurrentFrame is not { } frame) return;
        try
        {
            var files = PetPhotoService.Save(frame, Settings.PetName, PhotoDirectory);
            SetPlayStatus("拍立得和透明头像已保存到：" + Path.GetDirectoryName(files.PostcardPath));
            Say("这一刻的陪伴，已经帮你收藏好啦～");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { SetPlayStatus("照片保存失败：" + ex.Message); }
    }

    public string PhotoDirectory
    {
        get
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(pictures)) pictures = Path.GetDirectoryName(_settingsService.SettingsPath)!;
            return Path.Combine(pictures, "DesktopPet");
        }
    }

    public void OpenPhotoFolder()
    {
        if (!Directory.Exists(PhotoDirectory)) { SetPlayStatus("先保存一张拍立得，就可以打开相册啦～"); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PhotoDirectory) { UseShellExecute=true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        { SetPlayStatus("相册打开失败：" + ex.Message); }
    }

    private void OnHomeReached()
    {
        _behavior.StartHomeSleep();
        if (_quietFocusActive || _menuOpen || _trayMenuOpen) _behavior.Suspend(preserveSleep: true);
        SetPlayStatus("已经回到小窝，舒舒服服休息～");
        Companion.RecordHome();
        Say("小窝软软的，我先眯一会儿～");
        ScheduleSave();
    }

    public void SayAchievement(string id)
    {
        var text = Companion.Journal.UnlockedDialogue(id);
        if (text is not null) Say(text);
    }
    public void SetTrayMenuOpen(bool open)
    {
        if (_disposed) return;
        _trayMenuOpen = open;
        UpdateMenuPause();
    }
    private void PausePlayForMenu()
    {
        if (!_menuOpen && !_trayMenuOpen) return;
        _world.Pause();
        _challengeTimer.Stop();
        _window.UpdateAnimation(_state.State, false);
    }
    private void UpdateMenuPause()
    {
        if (_menuOpen || _trayMenuOpen)
        {
            if (IsPlaying) PausePlayForMenu();
            else _behavior.Suspend(preserveSleep: true);
            return;
        }
        if (!_state.IsVisible) return;
        if (IsPlaying)
        {
            _world.Resume();
            if (_challengeActive && _challengeRoundPending)
            {
                if (_challengeCaught == 0) BeginChallengeRound();
                else _challengeTimer.Start();
            }
            _window.UpdateAnimation(_state.State, true);
        }
        else ResumeBehavior(TimeSpan.FromSeconds(3));
    }
    private void DeliverReminder(ReminderNotice notice)
    {
        Say(notice.Text);
        if (notice.Kind != ReminderKind.Water) _behavior.PlayReminderAction();
    }
    private void OnCompanionChanged()
    {
        if (_disposed) return;
        var quiet = Companion.IsQuietFocusActive;
        if (quiet != _quietFocusActive)
        {
            _quietFocusActive = quiet;
            if (quiet) _speech.Hide();
            ResumeBehavior(TimeSpan.FromSeconds(3));
        }
        RefreshMenu();
        CompanionChanged?.Invoke();
    }
    private void OnProfileSaved(bool saved)
    {
        if (!_savingAll) SettingsSaved?.Invoke(_settingsSaveSucceeded && saved);
    }

    private void OnInteractionTimeout(object? sender, EventArgs e)
    {
        _interactionTimer.Stop();
        if (_interaction is { } action) _state.CompleteTemporaryAction(action);
        _interaction = null;
        _behavior.ActionCompleted();
    }

    public void ApplySettings(AppSettings settings)
    {
        if (_disposed) return;
        var next = settings.Normalize();
        var resetWorld = SettingsChangePolicy.RequiresWorldReset(Settings, next);
        var monitorChanged = next.SelectedMonitorId != Settings.SelectedMonitorId;
        if (resetWorld) CancelPlay();
        Settings = next;
        _screen.Select(Settings.SelectedMonitorId);
        Companion.UpdateSettings(Settings);
        _window.Topmost = Settings.Topmost;
        EndPointerInteraction(false);
        _window.SetClickThrough(Settings.ClickThrough);
        _window.SetPreciseHitTest(Settings.PreciseHitTestEnabled);
        _speech.Hide();
        if (!IsPlaying) _behavior.Refresh();
        if (resetWorld) _window.SetPetScale(Settings.PetScale, _screen.WorkArea);
        _window.SetAnimationSettings(Settings.EnableAnimations, Settings.AnimationSpeedMultiplier, Settings.PowerSavingEnabled);
        if (monitorChanged) SetPosition(_screen.Restore(null, null, WindowSize));
        else ClampPosition();
        _state.SetAutoBehaviorEnabled(Settings.AutoBehaviorEnabled);
        if (!Settings.EnableAnimations && _interaction is { } interaction)
        {
            _interactionTimer.Stop();
            _interaction = null;
            _state.CompleteTemporaryAction(interaction);
        }
        else if (_interaction is { } activeInteraction && _state.State == activeInteraction)
        {
            _interactionTimer.Stop();
            _interactionTimer.Interval = _window.ExpectedAnimationDuration(activeInteraction) + TimeSpan.FromSeconds(1);
            _interactionTimer.Start();
        }
        ResumeBehavior(TimeSpan.FromSeconds(3));
        _window.UpdateAnimation(_state.State, _state.IsVisible && !(IsPlaying && (_menuOpen || _trayMenuOpen)));
        if (resetWorld) _world.Refresh();
        else _world.RefreshAppearance();
        RefreshMenu();
        ScheduleSave();
        SettingsChanged?.Invoke(Settings);
        _visibility.SetFullscreenSuppressed(Settings.FullscreenAutoHide && _fullscreen);
        ApplyVisibility();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_disposed || _menuOpen) return;
        CancelPlay();
        _behavior.Suspend();
        _interactionTimer.Stop();
        if (_interaction is { } interrupted) _state.CompleteTemporaryAction(interrupted);
        _interaction = null;
        _pointerOrigin = ScreenPoint(e);
        _clickedHead = e.GetPosition(_window.PetSurface).Y <= _window.Height * 0.55;
        _windowOrigin = _screen.Pixels(_window);
        if (!_window.PetSurface.CaptureMouse())
        {
            _pointerOrigin = null;
            ResumeBehavior(TimeSpan.FromSeconds(3));
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
        if (_state.State != PetState.Dragging)
        {
            _state.BeginDrag();
            Say("带我去哪里呀？我的小爪子悬空啦～");
        }
        _screen.DragTo(_window, new PetPoint(_windowOrigin.X + delta.X, _windowOrigin.Y + delta.Y), current);
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
        return _window.PointToScreen(e.GetPosition(_window));
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
            _behavior.DelayNightSleep();
            Say("在这里陪你也很舒服～");
            ScheduleSave();
        }
        else if (allowClick)
        {
            if (_clickedHead) PetHead();
            else Interact(PetState.Happy, "你来找我玩啦，开心！");
        }
        if (!_menuOpen && !_disposed) ResumeBehavior(TimeSpan.FromSeconds(3));
    }

    private void OnMenuOpened(object? sender, RoutedEventArgs e)
    {
        _menuOpen = true;
        EndPointerInteraction(false);
        UpdateMenuPause();
        RefreshMenu();
    }

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        _menuOpen = false;
        if (!_disposed) UpdateMenuPause();
    }

    private void RefreshMenu()
    {
        _window.PauseItem.Header = Settings.AutoBehaviorEnabled ? "暂停自动行为" : "恢复自动行为";
        _window.TopmostItem.IsChecked = Settings.Topmost;
        _window.ClickThroughItem.IsChecked = Settings.ClickThrough;
        _window.Title = $"DesktopPet · {Settings.PetName}";
        _window.FocusStartItem.IsEnabled = !Companion.Focus.IsActive;
        _window.FocusPauseItem.IsEnabled = Companion.Focus.IsActive;
        _window.FocusPauseItem.Header = Companion.Focus.IsPaused ? "继续专注 / 休息" : "暂停专注 / 休息";
        _window.FocusStopItem.IsEnabled = Companion.Focus.IsActive;
        _window.BallItem.IsEnabled = Settings.ToysEnabled;
        _window.FrisbeeItem.IsEnabled = Settings.ToysEnabled;
        _window.ChallengeItem.IsEnabled = Settings.ToysEnabled;
        _window.StopPlayItem.IsEnabled = IsPlaying;
        _window.HomeItem.IsEnabled = Settings.HomeEnabled;
    }

    private void OnStateChanged(PetState state)
    {
        _window.UpdateAnimation(state, _state.IsVisible && !(IsPlaying && (_menuOpen || _trayMenuOpen)));
        _world?.RefreshFeedingBowl();
    }

    private void OnAnimationCompleted(AnimationKind kind)
    {
        var action = kind switch
        {
            AnimationKind.Happy => PetState.Happy,
            AnimationKind.Petting => PetState.Petting,
            AnimationKind.Feeding => PetState.Feeding,
            AnimationKind.Stretching => PetState.Stretching,
            AnimationKind.Yawning => PetState.Yawning,
            _ => (PetState?)null
        };
        if (action is not { } completed || _state.State != completed) return;
        _state.CompleteTemporaryAction(completed);
        if (_interaction == completed)
        {
            _interactionTimer.Stop();
            _interaction = null;
        }
        _behavior.ActionCompleted();
    }

    private void OnBoundsChanged()
    {
        EndPointerInteraction(false);
        CancelPlay();
        if (!_menuOpen) ResumeBehavior(TimeSpan.FromSeconds(3));
        else _behavior.Refresh();
        _window.SetPetScale(Settings.PetScale, _screen.WorkArea);
        ClampPosition();
        _world.Refresh();
        SettingsChanged?.Invoke(Settings);
        ScheduleSave();
    }

    private void SetPosition(PetPoint point) => _screen.Move(_window, point);

    private void ClampPosition() => _screen.ClampWindow(_window);

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
        var pixels = _screen.Pixels(_window);
        Settings = Settings with { LastPetX = pixels.X, LastPetY = pixels.Y,
            SavedPositionUsesPixels = true, SelectedMonitorId = _screen.CurrentMonitor.Id };
        _savingAll = true;
        try
        {
            _settingsSaveSucceeded = _settingsService.Save(Settings);
            Companion.Save();
        }
        finally { _savingAll = false; }
        SettingsSaved?.Invoke(LastSaveSucceeded);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Companion.Dispose();
        Companion.Changed -= OnCompanionChanged;
        Companion.ReminderRequested -= DeliverReminder;
        Companion.ProfileSaved -= OnProfileSaved;
        EndPointerInteraction(false);
        _behavior.Dispose();
        _challengeTimer.Stop();
        _challengeActive = false;
        _challengeTimer.Tick -= OnChallengeRound;
        _world.FollowFinished -= OnFollowFinished;
        _world.Dispose();
        _behavior.HomeSleepRequested -= ReturnHome;
        _behavior.SpeechRequested -= Say;
        _interactionTimer.Stop();
        _interactionTimer.Tick -= OnInteractionTimeout;
        _speech.Dispose();
        SaveNow();
        _window.UpdateAnimation(_state.State, false);
        _saveTimer.Tick -= OnSaveTimer;
        _screen.BoundsChanged -= OnBoundsChanged;
        _screen.ActiveMonitorChanged -= OnMonitorChanged;
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
