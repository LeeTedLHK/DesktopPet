using System.Security;

namespace DesktopPet.Animations;

public sealed class AnimationLoader
{
    private static readonly AnimationKind[] Kinds = Enum.GetValues<AnimationKind>();
    private readonly string _assetRoot;
    private readonly Action<string> _log;

    public AnimationLoader(string assetRoot, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        _assetRoot = Path.GetFullPath(assetRoot);
        _log = log ?? (_ => { });
    }

    public IReadOnlyDictionary<AnimationKind, AnimationClip> Load()
    {
        var directFrames = Kinds.ToDictionary(kind => kind, ReadActionFrames);
        var idleFrames = directFrames[AnimationKind.Idle];
        var fallbackFrames = ReadFallbackFrames();
        var clips = new Dictionary<AnimationKind, AnimationClip>(Kinds.Length);

        foreach (var kind in Kinds)
        {
            var frames = directFrames[kind];
            var isFallback = false;
            if (frames.Count == 0 && kind is (AnimationKind.WalkLeft or AnimationKind.WalkRight) &&
                directFrames[AnimationKind.Walk].Count > 0)
            {
                frames = directFrames[AnimationKind.Walk];
                isFallback = true;
                _log($"Animation '{kind}' is missing; using generic Walk frames.");
            }

            if (frames.Count == 0 && idleFrames.Count > 0 && kind != AnimationKind.Idle)
            {
                frames = idleFrames;
                isFallback = true;
                _log($"Animation '{kind}' is missing; using Idle frames.");
            }

            if (frames.Count == 0 && fallbackFrames.Count > 0)
            {
                frames = fallbackFrames;
                isFallback = true;
                _log($"Animation '{kind}' is missing; using the default pet image.");
            }

            if (frames.Count == 0)
            {
                isFallback = true;
                _log($"Animation '{kind}' has no usable frames and no default pet image.");
            }

            clips[kind] = AnimationDefinition.CreateClip(kind, frames, isFallback);
        }

        return clips;
    }

    private IReadOnlyList<AnimationFrame> ReadActionFrames(AnimationKind kind)
    {
        var directory = Path.Combine(_assetRoot, kind.ToString());
        if (!Directory.Exists(directory))
        {
            _log($"Animation directory does not exist: {directory}");
            return [];
        }

        try
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (files.Length == 0)
            {
                _log($"Animation directory contains no PNG frames: {directory}");
                return [];
            }

            return NaturalSort.Order(files)
                .Select((path, index) => new AnimationFrame(path, index))
                .ToArray();
        }
        catch (Exception error) when (IsExpectedFileError(error))
        {
            _log($"Animation directory could not be read: {directory}; {error.Message}");
            return [];
        }
    }

    private IReadOnlyList<AnimationFrame> ReadFallbackFrames()
    {
        foreach (var fileName in new[] { "pet.png", "pet.jpg" })
        {
            var path = Path.Combine(_assetRoot, fileName);
            try
            {
                if (File.Exists(path)) return [new AnimationFrame(path, 0)];
            }
            catch (Exception error) when (IsExpectedFileError(error))
            {
                _log($"Default pet image could not be checked: {path}; {error.Message}");
            }
        }

        _log($"Default pet image does not exist under {_assetRoot}.");
        return [];
    }

    private static bool IsExpectedFileError(Exception error) =>
        error is ArgumentException or IOException or UnauthorizedAccessException or SecurityException;
}
