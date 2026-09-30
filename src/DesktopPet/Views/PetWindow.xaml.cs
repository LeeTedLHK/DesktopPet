using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DesktopPet.Animations;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.Views;

public partial class PetWindow : Window
{
    private const double BaseWidth = 160;
    private const double AnimationPadding = 8;
    private readonly AnimationManager _animationManager;
    private readonly BitmapSource _sizingSource;
    private bool _allowClose;
    private bool _animationsEnabled = true;
    private double _animationSpeedMultiplier = 1d;
    private PetState _currentState = PetState.Idle;
    private bool _currentVisibility;
    public event Action? HideRequested;
    public event Action<AnimationKind>? AnimationCompleted;

    public PetWindow(AppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        InitializeComponent();
        var assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pet");
        var fallback = LoadFallbackImage(assetRoot, logger);
        PetImage.Source = fallback;
        _animationManager = new AnimationManager(Dispatcher, source => PetImage.Source = source,
            assetRoot, fallback, logger, () => _animationSpeedMultiplier);
        _animationManager.PlaybackCompleted += OnPlaybackCompleted;
        _sizingSource = _animationManager.ReferenceSource;
        SourceInitialized += (_, _) => NativeWindowService.HideFromAltTab(this);
    }

    public void SetPetScale(double scale)
    {
        var desiredWidth = BaseWidth * scale;
        var desiredHeight = desiredWidth * _sizingSource.PixelHeight / _sizingSource.PixelWidth;
        var area = SystemParameters.WorkArea;
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
        // Directional WalkLeft/WalkRight assets are already authored for their direction.
        DirectionScale.ScaleX = 1;
        if (!visible || state == PetState.Dragging)
        {
            ClickScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ClickScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _animationManager.SetState(state, visible, _animationsEnabled);
            return;
        }

        if (state is PetState.WalkingLeft or PetState.WalkingRight)
        {
            WalkingOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -1.2,
                TimeSpan.FromMilliseconds(300))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        }
        else if (state is not (PetState.Happy or PetState.Sleeping))
        {
            BreathingScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.012,
                TimeSpan.FromSeconds(1.6))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        }

        _animationManager.SetState(state, visible, _animationsEnabled);
    }

    public void SetAnimationSettings(bool enabled, double speedMultiplier)
    {
        _animationsEnabled = enabled;
        _animationSpeedMultiplier = double.IsFinite(speedMultiplier)
            ? Math.Clamp(speedMultiplier, 0.25d, 3d)
            : 1d;
        _animationManager.RefreshSpeed();
        UpdateAnimation(_currentState, _currentVisibility);
    }

    public void DisposeAnimations()
    {
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
