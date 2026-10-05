using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DesktopPet.Animations;
using DesktopPet.Models;
using DesktopPet.Services;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopPet.Views;

public partial class PetWindow : Window
{
    private const double BaseWidth = 160;
    private const double AnimationPadding = 8;
    private AnimationManager _animationManager;
    private BitmapSource _sizingSource;
    private readonly AppLogger _logger;
    private readonly Dictionary<BitmapSource, PixelHitMap> _hitMaps = new();
    private readonly DispatcherTimer _hitTimer;
    private HwndSource? _source;
    private bool _manualClickThrough;
    private bool _preciseHitTest = true;
    private bool _nativeClickThrough;
    private bool _allowClose;
    private bool _animationsDisposed;
    private bool _animationsEnabled = true;
    private double _animationSpeedMultiplier = 1d;
    private bool _powerSavingEnabled;
    private PetState _currentState = PetState.Idle;
    private bool _currentVisibility;
    public event Action? HideRequested;
    public event Action<AnimationKind>? AnimationCompleted;
    public BitmapSource? CurrentFrame => PetImage.Source as BitmapSource;
    public PetPoint CurrentMouthAnchor => _animationManager.CurrentMouthAnchor;
    public void UpdateFetchProgress(double progress) => _animationManager.SetPoseProgress(progress);

    public PetWindow(AppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        InitializeComponent();
        var assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pet");
        var fallback = LoadFallbackImage(assetRoot, logger);
        PetImage.Source = fallback;
        _animationManager = new AnimationManager(Dispatcher, SetFrame,
            assetRoot, fallback, logger, () => _animationSpeedMultiplier, () => _powerSavingEnabled);
        _animationManager.PlaybackCompleted += OnPlaybackCompleted;
        _sizingSource = _animationManager.ReferenceSource;
        _hitTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher) { Interval = TimeSpan.FromMilliseconds(20) };
        _hitTimer.Tick += OnHitTick;
        SourceInitialized += (_, _) =>
        {
            NativeWindowService.HideFromAltTab(this);
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(HitHook);
        };
        IsVisibleChanged += (_, _) => RefreshHitPolling();
    }

    public void SetPetScale(double scale, WorkArea? workArea = null)
    {
        var desiredWidth = BaseWidth * scale;
        var desiredHeight = desiredWidth * _sizingSource.PixelHeight / _sizingSource.PixelWidth;
        var area = workArea ?? new WorkArea(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        // Even unusual portrait resources must fit entirely within the primary work area.
        var fit = Math.Min(1, Math.Min((area.Width - AnimationPadding) / desiredWidth,
            (area.Height - AnimationPadding) / desiredHeight));
        fit = Math.Max(0.01, fit);
        PetImage.Width = desiredWidth * fit;
        PetImage.Height = desiredHeight * fit;
        Width = PetImage.Width + AnimationPadding;
        Height = PetImage.Height + AnimationPadding;
    }

    public void UpdateAnimation(PetState state, bool visible)
    {
        _currentState = state;
        _currentVisibility = visible;
        BreathingScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        WalkingOffset.BeginAnimation(TranslateTransform.YProperty, null);
        // Directional walking and running frames are authored for their direction.
        DirectionScale.ScaleX = 1;
        if (!visible || state == PetState.Dragging)
        {
            ClickScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ClickScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _animationManager.SetState(state, visible, _animationsEnabled);
            return;
        }

        // Breathing and weight shifts are present in the authored frames.
        _animationManager.SetState(state, visible, _animationsEnabled);
    }

    public void SetAnimationSettings(bool enabled, double speedMultiplier, bool powerSavingEnabled = false)
    {
        _animationsEnabled = enabled;
        _powerSavingEnabled = powerSavingEnabled;
        _animationSpeedMultiplier = double.IsFinite(speedMultiplier)
            ? Math.Clamp(speedMultiplier, 0.25d, 3d)
            : 1d;
        _animationManager.RefreshSpeed();
        UpdateAnimation(_currentState, _currentVisibility);
    }

    public void SetClickThrough(bool enabled)
    {
        _manualClickThrough = enabled;
        RefreshHitPolling();
    }

    public void SetPreciseHitTest(bool enabled) { _preciseHitTest = enabled; RefreshHitPolling(); }

    private void RefreshHitPolling()
    {
        if (_animationsDisposed) { _hitTimer.Stop(); return; }
        if (IsVisible && _preciseHitTest && !_manualClickThrough)
        {
            OnHitTick(null, EventArgs.Empty);
            _hitTimer.Start();
        }
        else
        {
            _hitTimer.Stop();
            SetNativeClickThrough(_manualClickThrough);
        }
    }

    private void SetNativeClickThrough(bool enabled)
    {
        NativeWindowService.SetClickThrough(this, enabled);
        _nativeClickThrough = enabled;
    }

    private void SetFrame(ImageSource source)
    {
        PetImage.Source = source;
        if (source is BitmapSource bitmap && !_hitMaps.ContainsKey(bitmap))
            _hitMaps[bitmap] = new PixelHitMap(bitmap);
    }

    public bool IsOpaqueAtScreenPoint(Point pixels)
    {
        if (PetImage.Source is not BitmapSource bitmap || PetImage.ActualWidth <= 0 || PetImage.ActualHeight <= 0) return false;
        var local = PetImage.PointFromScreen(pixels);
        var fit = Math.Min(PetImage.ActualWidth / bitmap.Width, PetImage.ActualHeight / bitmap.Height);
        var width = bitmap.Width * fit;
        var height = bitmap.Height * fit;
        var x = (local.X - (PetImage.ActualWidth - width) / 2) / width * bitmap.PixelWidth;
        var y = (local.Y - (PetImage.ActualHeight - height) / 2) / height * bitmap.PixelHeight;
        if (!_hitMaps.TryGetValue(bitmap, out var map)) _hitMaps[bitmap] = map = new PixelHitMap(bitmap);
        return map.Hit((int)Math.Floor(x), (int)Math.Floor(y));
    }

    private bool ShouldPass(Point pixels) => _manualClickThrough ||
        (_preciseHitTest && !PetSurface.IsMouseCaptured && !PetMenu.IsOpen && !IsOpaqueAtScreenPoint(pixels));

    private void OnHitTick(object? sender, EventArgs e)
    {
        if (!IsVisible || !NativeWindowService.TryGetCursorPosition(out var point)) return;
        var enabled = ShouldPass(point);
        if (_nativeClickThrough != enabled) SetNativeClickThrough(enabled);
    }

    private IntPtr HitHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x84) return IntPtr.Zero;
        var value = lParam.ToInt64();
        var point = new Point((short)(value & 0xffff), (short)((value >> 16) & 0xffff));
        handled = true;
        return new IntPtr(ShouldPass(point) ? -1 : 1);
    }

    public bool TryLoadSkin(string root, out string error)
    {
        try
        {
            long pixels = 0;
            foreach (var path in Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories))
            {
                var decoded = Decode(path);
                pixels += (long)decoded.PixelWidth * decoded.PixelHeight;
                if (pixels > 64L * 1024 * 1024) throw new InvalidDataException("皮肤解码后尺寸过大");
            }
            var idle = Directory.EnumerateFiles(Path.Combine(root, "Idle"), "*.png").Order().FirstOrDefault()
                ?? throw new InvalidDataException("皮肤需要 Idle 动画");
            var fallback = Decode(idle);
            var manager = new AnimationManager(Dispatcher, SetFrame, root, fallback, _logger,
                () => _animationSpeedMultiplier, () => _powerSavingEnabled);
            _animationManager.PlaybackCompleted -= OnPlaybackCompleted;
            _animationManager.Dispose();
            _animationManager = manager;
            _animationManager.PlaybackCompleted += OnPlaybackCompleted;
            _sizingSource = manager.ReferenceSource;
            _hitMaps.Clear();
            SetFrame(_sizingSource);
            UpdateAnimation(_currentState, _currentVisibility);
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.IO.FileFormatException)
        { error = "皮肤无法完整解码：" + ex.Message; _logger.Error(error, ex); return false; }
    }

    private static BitmapSource Decode(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public TimeSpan ExpectedAnimationDuration(PetState state) => _animationManager.ExpectedDuration(state);

    public void DisposeAnimations()
    {
        _animationsDisposed = true;
        _hitTimer.Stop();
        _hitTimer.Tick -= OnHitTick;
        _source?.RemoveHook(HitHook);
        _hitMaps.Clear();
        _animationManager.PlaybackCompleted -= OnPlaybackCompleted;
        _animationManager.Dispose();
    }

    public void PlayClickFeedback()
    {
        var animation = new DoubleAnimation(1, 0.96, TimeSpan.FromMilliseconds(110))
        {
            AutoReverse = true, FillBehavior = FillBehavior.Stop,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        ClickScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        ClickScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideRequested?.Invoke();
        }
        base.OnClosing(e);
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnPlaybackCompleted(AnimationKind kind) => AnimationCompleted?.Invoke(kind);

    private static BitmapSource LoadFallbackImage(string assetRoot, AppLogger logger)
    {
        foreach (var fileName in new[] { "pet.png", "pet.jpg" })
        {
            var path = Path.Combine(assetRoot, fileName);
            if (!File.Exists(path)) continue;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception error)
            {
                logger.Error($"Default pet image could not be decoded: {path}", error);
            }
        }

        logger.Error($"No usable default pet image exists under {assetRoot}.");
        var placeholder = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Pbgra32, null,
            new byte[] { 0, 0, 0, 0 }, 4);
        placeholder.Freeze();
        return placeholder;
    }
}
