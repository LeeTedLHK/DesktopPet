namespace DesktopPet.Animations;

public sealed record AnimationClip
{
    public AnimationClip(AnimationKind kind, IReadOnlyList<AnimationFrame> frames,
        double framesPerSecond, AnimationPlayback playback, bool isFallback = false)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }

        Kind = kind;
        Frames = frames;
        FramesPerSecond = framesPerSecond;
        Playback = playback;
        IsFallback = isFallback;
    }

    public AnimationKind Kind { get; }
    public IReadOnlyList<AnimationFrame> Frames { get; }
    public double FramesPerSecond { get; }
    public AnimationPlayback Playback { get; }
    public bool IsFallback { get; }
    public TimeSpan FrameInterval => TimeSpan.FromSeconds(1d / FramesPerSecond);
}
