using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Models;
using DesktopPet.Services;
using System.Text.Json;

namespace DesktopPet.Animations;

public sealed class AnimationManager : IDisposable
{
    private readonly Action<ImageSource> _setFrame;
    private readonly BitmapSource _fallbackSource;
    private readonly AppLogger _logger;
    private readonly Func<double> _speedMultiplier;
    private readonly Func<bool> _powerSavingEnabled;
    private readonly DispatcherTimer _timer;
    private readonly IReadOnlyDictionary<AnimationKind, AnimationClip> _clips;
    private readonly Dictionary<string, BitmapSource?> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<AnimationKind, PetPoint[]> _mouthAnchors = new();
    private AnimationClip? _currentClip;
    private AnimationKind _currentKind;
    private int _frameIndex;
    private bool _hasCurrentClip;
    private AnimationKind _requestedKind;
    private bool _hasRequest;
    private bool _requestedPlay;
    private AnimationKind? _pendingTarget;
    private bool _disposed;

    public AnimationManager(Dispatcher dispatcher, Action<ImageSource> setFrame,
        string assetRoot, BitmapSource fallbackSource, AppLogger logger,
        Func<double> speedMultiplier, Func<bool>? powerSavingEnabled = null)
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
        _powerSavingEnabled = powerSavingEnabled ?? (() => false);
        _imageCache["<fallback>"] = fallbackSource;
        _clips = LoadClips(assetRoot);
        LoadMouthAnchors(assetRoot);
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _timer.Tick += OnTick;
    }

    public event Action<AnimationKind>? PlaybackCompleted;

    public TimeSpan ExpectedDuration(PetState state)
    {
        var kind = AnimationDefinition.ForState(state);
        if (!_clips.TryGetValue(kind, out var clip)) return TimeSpan.Zero;
        var multiplier = _speedMultiplier();
        if (!double.IsFinite(multiplier)) multiplier = 1;
        return TimeSpan.FromSeconds(clip.Frames.Count / clip.FramesPerSecond / Math.Clamp(multiplier, 0.25, 3));
    }

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
        var shouldPlay = visible && enabled && state != PetState.Paused;
        var kind = AnimationDefinition.ForState(state);
        var previous = _requestedKind;
        var sameRequest = _hasRequest && kind == previous;
        var previousPlay = _requestedPlay;
        _hasRequest = true;
        _requestedKind = kind;
        _requestedPlay = shouldPlay;

        if (!visible || !enabled)
        {
            _pendingTarget = null;
            _timer.Stop();
            if (!_hasCurrentClip || _currentKind != kind) SelectClip(kind);
            return;
        }

        // Repeated world ticks must not restart a presentation join. A changed
        // request replaces its destination; hiding and disabling always clear it.
        if (sameRequest && _pendingTarget == kind)
        {
            ConfigureTimer();
            return;
        }

        var transition = !sameRequest && _hasCurrentClip && (previousPlay || shouldPlay)
            ? MovementTransitions.ForChange(previous, kind) : null;
        _pendingTarget = null;
        if (transition is { } join && _clips.TryGetValue(join, out var authored) &&
            !authored.IsFallback && authored.Frames.Any(frame => GetImage(frame.Path) is not null))
        {
            _pendingTarget = kind;
            SelectClip(join);
            ConfigureTimer();
            return;
        }

        if (!_hasCurrentClip || _currentKind != kind) SelectClip(kind);
        if (shouldPlay) ConfigureTimer(); else _timer.Stop();
    }

    private void SelectClip(AnimationKind kind)
    {
        if (!_clips.TryGetValue(kind, out var clip))
        {
            _logger.Info($"Animation clip '{kind}' was not loaded; using the fallback image.");
            clip = AnimationDefinition.CreateClip(kind, [], isFallback: true);
        }
        _currentKind = kind; _currentClip = clip; _frameIndex = 0; _hasCurrentClip = true;
        ShowCurrentFrame();
    }

    public void Stop(bool showStableFrame = true)
    {
        if (_disposed) return;
        _pendingTarget = null;
        _requestedPlay = false;
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
                FinishCurrentClip();
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
        if (_powerSavingEnabled() && _currentClip.Playback == AnimationPlayback.Loop)
        {
            var minimum = _currentKind switch
            {
                AnimationKind.Idle => TimeSpan.FromMilliseconds(250),
                AnimationKind.Sleep => TimeSpan.FromMilliseconds(500),
                _ => TimeSpan.Zero,
            };
            if (interval < minimum) interval = minimum;
        }
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
            FinishCurrentClip();
            return;
        }

        _frameIndex++;
        if (_frameIndex >= _currentClip.Frames.Count)
        {
            _frameIndex = 0;
        }
        ShowCurrentFrame();
    }

    private void FinishCurrentClip()
    {
        if (_pendingTarget is { } target)
        {
            _pendingTarget = null;
            SelectClip(target);
            if (_requestedPlay) ConfigureTimer();
            return;
        }
        if (_currentClip is not null) PlaybackCompleted?.Invoke(_currentClip.Kind);
    }

    public AnimationKind CurrentKind => _currentKind;
    public int CurrentFrameIndex => _frameIndex;
    public void SetPoseProgress(double progress)
    {
        if (_disposed || !_requestedPlay || _pendingTarget is not null || _currentClip is null || _currentClip.Frames.Count == 0 ||
            !double.IsFinite(progress) || _currentKind is not (AnimationKind.JumpLeft or AnimationKind.JumpRight or
                AnimationKind.PickupLeft or AnimationKind.PickupRight or AnimationKind.DropLeft or AnimationKind.DropRight or AnimationKind.Happy)) return;
        _timer.Stop();
        var frame = Math.Min(_currentClip.Frames.Count - 1, (int)(Math.Clamp(progress, 0, 1) * _currentClip.Frames.Count));
        if (frame == _frameIndex) return;
        _frameIndex = frame;
        ShowCurrentFrame();
    }
    public PetPoint CurrentMouthAnchor
    {
        get
        {
            if (_currentClip is { IsFallback: false } && _mouthAnchors.TryGetValue(_currentKind, out var anchors) &&
                _frameIndex < anchors.Length) return anchors[_frameIndex];
            if (_requestedKind == AnimationKind.Feeding) return new(.30, .55);
            return new(_requestedKind.ToString().EndsWith("Left", StringComparison.Ordinal) ? .22 : .78, .46);
        }
    }

    private void LoadMouthAnchors(string assetRoot)
    {
        var path = Path.Combine(assetRoot, "mouth-anchors.json");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 128 * 1024) return;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Object) return;
            foreach (var action in actions.EnumerateObject())
            {
                if (!Enum.TryParse<AnimationKind>(action.Name, out var kind) || action.Value.ValueKind != JsonValueKind.Array ||
                    !_clips.TryGetValue(kind, out var clip) || action.Value.GetArrayLength() != clip.Frames.Count) continue;
                var anchors = new List<PetPoint>();
                foreach (var frame in action.Value.EnumerateArray())
                {
                    if (frame.ValueKind != JsonValueKind.Object ||
                        !frame.TryGetProperty("x", out var x) || !frame.TryGetProperty("y", out var y) ||
                        x.ValueKind != JsonValueKind.Number || y.ValueKind != JsonValueKind.Number ||
                        !x.TryGetDouble(out var px) || !y.TryGetDouble(out var py) ||
                        !double.IsFinite(px) || !double.IsFinite(py) || px < 0 || px > 1 || py < 0 || py > 1) break;
                    anchors.Add(new(px, py));
                }
                if (anchors.Count == clip.Frames.Count && anchors.Count > 0)
                    _mouthAnchors[kind] = anchors.ToArray();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { _logger.Info($"Optional mouth anchors unavailable: {error.Message}"); }
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
