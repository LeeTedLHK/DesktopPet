using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.Animations;

public sealed class AnimationManager : IDisposable
{
    private readonly Action<ImageSource> _setFrame;
    private readonly BitmapSource _fallbackSource;
    private readonly AppLogger _logger;
    private readonly Func<double> _speedMultiplier;
    private readonly DispatcherTimer _timer;
    private readonly IReadOnlyDictionary<AnimationKind, AnimationClip> _clips;
    private readonly Dictionary<string, BitmapSource?> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    private AnimationClip? _currentClip;
    private AnimationKind _currentKind;
    private int _frameIndex;
    private bool _hasCurrentClip;
    private bool _disposed;

    public AnimationManager(Dispatcher dispatcher, Action<ImageSource> setFrame,
        string assetRoot, BitmapSource fallbackSource, AppLogger logger,
        Func<double> speedMultiplier)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(setFrame);
        ArgumentNullException.ThrowIfNull(fallbackSource);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(speedMultiplier);

        _setFrame = setFrame;
        _fallbackSource = fallbackSource;
        _logger = logger;
        _speedMultiplier = speedMultiplier;
        _imageCache["<fallback>"] = fallbackSource;
        _clips = LoadClips(assetRoot);
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _timer.Tick += OnTick;
    }

    public event Action<AnimationKind>? PlaybackCompleted;

    public BitmapSource ReferenceSource
    {
        get
        {
            if (_clips.TryGetValue(AnimationKind.Idle, out var idle))
            {
                foreach (var frame in idle.Frames)
                {
                    var source = GetImage(frame.Path);
                    if (source is not null) return source;
                }
            }

            return _fallbackSource;
        }
    }

    public void SetState(PetState state, bool visible, bool enabled)
    {
        if (_disposed) return;

        var shouldPlay = visible && enabled && state is not (PetState.Dragging or PetState.Paused);
        var kind = AnimationDefinition.ForState(state);
        if (!_clips.TryGetValue(kind, out var clip))
        {
            _logger.Info($"Animation clip '{kind}' was not loaded; using the fallback image.");
            clip = AnimationDefinition.CreateClip(kind, [], isFallback: true);
        }

        if (!shouldPlay)
        {
            _timer.Stop();
            if (!_hasCurrentClip || _currentKind != kind)
            {
                _currentKind = kind;
                _currentClip = clip;
                _frameIndex = 0;
                _hasCurrentClip = true;
                ShowCurrentFrame();
            }
            return;
        }

        var sameClip = _hasCurrentClip && _currentKind == kind;
        if (!sameClip)
        {
            _currentKind = kind;
            _currentClip = clip;
            _frameIndex = 0;
            _hasCurrentClip = true;
            ShowCurrentFrame();
        }
        else if (!ReferenceEquals(_currentClip, clip))
        {
            _currentClip = clip;
        }

        ConfigureTimer();
    }

    public void Stop(bool showStableFrame = true)
    {
        if (_disposed) return;
        _timer.Stop();
        if (showStableFrame) ShowCurrentFrame();
    }

    public void RefreshSpeed()
    {
        if (_disposed || !_timer.IsEnabled) return;
        ConfigureTimer();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        PlaybackCompleted = null;
        _imageCache.Clear();
    }

    private IReadOnlyDictionary<AnimationKind, AnimationClip> LoadClips(string assetRoot)
    {
        try
        {
            return new AnimationLoader(assetRoot, message => _logger.Info(message)).Load();
        }
        catch (Exception error)
        {
            _logger.Error("Animations could not be loaded; using the default image.", error);
            return Enum.GetValues<AnimationKind>()
                .ToDictionary(kind => kind,
                    kind => AnimationDefinition.CreateClip(kind, [], isFallback: true));
        }
    }

    private void ConfigureTimer()
    {
        if (_currentClip is null)
        {
            _timer.Stop();
            return;
        }

        if (_currentClip.Frames.Count == 0)
        {
            _timer.Stop();
            if (_currentClip.Playback == AnimationPlayback.Once)
            {
                PlaybackCompleted?.Invoke(_currentClip.Kind);
            }
            return;
        }

        if (_currentClip.Frames.Count == 1 && _currentClip.Playback == AnimationPlayback.Loop)
        {
            _timer.Stop();
            return;
        }

        var multiplier = _speedMultiplier();
        if (!double.IsFinite(multiplier)) multiplier = 1d;
        multiplier = Math.Clamp(multiplier, 0.25d, 3d);
        var interval = TimeSpan.FromTicks(Math.Max(1,
            (long)(_currentClip.FrameInterval.Ticks / multiplier)));
        _timer.Interval = interval < TimeSpan.FromMilliseconds(15)
            ? TimeSpan.FromMilliseconds(15)
            : interval;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed || _currentClip is null || _currentClip.Frames.Count == 0)
        {
            _timer.Stop();
            return;
        }

        if (_currentClip.Playback == AnimationPlayback.Once &&
            _frameIndex >= _currentClip.Frames.Count - 1)
        {
            _timer.Stop();
            ShowCurrentFrame();
            PlaybackCompleted?.Invoke(_currentClip.Kind);
            return;
        }

        _frameIndex++;
        if (_frameIndex >= _currentClip.Frames.Count)
        {
            _frameIndex = 0;
        }
        ShowCurrentFrame();
    }

    private void ShowCurrentFrame()
    {
        if (_currentClip is null || _currentClip.Frames.Count == 0)
        {
            _setFrame(_fallbackSource);
            return;
        }

        var frameIndex = Math.Clamp(_frameIndex, 0, _currentClip.Frames.Count - 1);
        var source = GetImage(_currentClip.Frames[frameIndex].Path) ?? _fallbackSource;
        _setFrame(source);
    }

    private BitmapSource? GetImage(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        if (_imageCache.TryGetValue(normalizedPath, out var cached)) return cached;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(normalizedPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            _imageCache[normalizedPath] = bitmap;
            return bitmap;
        }
        catch (Exception error)
        {
            _logger.Error($"Animation frame could not be decoded: {normalizedPath}", error);
            _imageCache[normalizedPath] = null;
            return null;
        }
    }
}
