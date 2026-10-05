using DesktopPet.Animations;

namespace DesktopPet.Tests;

public sealed class AnimationLoaderTests
{
    [Fact]
    public void Load_NaturallySortsPngsAndIgnoresOtherFiles()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Idle"));
            File.WriteAllText(Path.Combine(root, "Idle", "idle_10.png"), "10");
            File.WriteAllText(Path.Combine(root, "Idle", "idle_2.png"), "2");
            File.WriteAllText(Path.Combine(root, "Idle", "idle_1.png"), "1");
            File.WriteAllText(Path.Combine(root, "Idle", "notes.txt"), "ignore");
            Directory.CreateDirectory(Path.Combine(root, "Idle", "nested"));
            File.WriteAllText(Path.Combine(root, "Idle", "nested", "nested.png"), "ignore");
            File.WriteAllText(Path.Combine(root, "pet.jpg"), "fallback");

            var idle = new AnimationLoader(root).Load()[AnimationKind.Idle];

            Assert.Equal(["idle_1.png", "idle_2.png", "idle_10.png"],
                idle.Frames.Select(frame => Path.GetFileName(frame.Path)));
            Assert.False(idle.IsFallback);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Load_MissingActionsFallBackToIdleWithRequestedPlaybackMode()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Idle"));
            File.WriteAllText(Path.Combine(root, "Idle", "idle_1.png"), "1");
            File.WriteAllText(Path.Combine(root, "pet.jpg"), "fallback");

            var clips = new AnimationLoader(root).Load();

            Assert.Equal(AnimationPlayback.Once, clips[AnimationKind.Happy].Playback);
            Assert.True(clips[AnimationKind.Happy].IsFallback);
            Assert.Equal(clips[AnimationKind.Idle].Frames.Select(frame => frame.Path),
                clips[AnimationKind.Happy].Frames.Select(frame => frame.Path));
            Assert.Equal(AnimationPlayback.Loop, clips[AnimationKind.Sleep].Playback);
            Assert.Equal(clips[AnimationKind.Idle].Frames.Select(frame => frame.Path),
                clips[AnimationKind.Sleep].Frames.Select(frame => frame.Path));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Load_UsesPetImageWhenIdleDirectoryIsMissing()
    {
        var root = CreateRoot();
        try
        {
            var fallbackPath = Path.Combine(root, "pet.jpg");
            File.WriteAllText(fallbackPath, "fallback");

            var clips = new AnimationLoader(root).Load();

            Assert.All(clips.Values, clip =>
            {
                var frame = Assert.Single(clip.Frames);
                Assert.Equal(fallbackPath, frame.Path);
                Assert.True(clip.IsFallback);
            });
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Load_UsesDedicatedDirectionalWalkClipsWhenPresent()
    {
        var root = CreateRoot();
        try
        {
            foreach (var (directory, prefix) in new[]
                     { ("WalkLeft", "walk_left"), ("WalkRight", "walk_right") })
            {
                Directory.CreateDirectory(Path.Combine(root, directory));
                File.WriteAllText(Path.Combine(root, directory, $"{prefix}_000.png"), "0");
                File.WriteAllText(Path.Combine(root, directory, $"{prefix}_001.png"), "1");
            }
            File.WriteAllText(Path.Combine(root, "pet.jpg"), "fallback");

            var clips = new AnimationLoader(root).Load();

            Assert.Equal(AnimationKind.WalkLeft, clips[AnimationKind.WalkLeft].Kind);
            Assert.False(clips[AnimationKind.WalkLeft].IsFallback);
            Assert.Equal(["walk_left_000.png", "walk_left_001.png"],
                clips[AnimationKind.WalkLeft].Frames.Select(frame => Path.GetFileName(frame.Path)));
            Assert.Equal(["walk_right_000.png", "walk_right_001.png"],
                clips[AnimationKind.WalkRight].Frames.Select(frame => Path.GetFileName(frame.Path)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Load_MissingDirectionalWalkFallsBackToGenericWalkBeforeIdle()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Walk"));
            File.WriteAllText(Path.Combine(root, "Walk", "walk_000.png"), "0");
            Directory.CreateDirectory(Path.Combine(root, "WalkLeft"));
            File.WriteAllText(Path.Combine(root, "WalkLeft", "broken.txt"), "ignore");
            File.WriteAllText(Path.Combine(root, "pet.jpg"), "fallback");

            var clips = new AnimationLoader(root).Load();

            Assert.True(clips[AnimationKind.WalkLeft].IsFallback);
            Assert.Equal(["walk_000.png"],
                clips[AnimationKind.WalkLeft].Frames.Select(frame => Path.GetFileName(frame.Path)));
            Assert.True(clips[AnimationKind.WalkRight].IsFallback);
            Assert.Equal(["walk_000.png"],
                clips[AnimationKind.WalkRight].Frames.Select(frame => Path.GetFileName(frame.Path)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Load_WithNoResourcesReturnsSafeEmptyClipsAndLogsTheFallback()
    {
        var messages = new List<string>();
        var root = CreateRoot();
        try
        {
            var clips = new AnimationLoader(root, messages.Add).Load();

            Assert.Equal(Enum.GetValues<AnimationKind>().Length, clips.Count);
            Assert.All(clips.Values, clip => Assert.Empty(clip.Frames));
            Assert.NotEmpty(messages);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"DesktopPet.AnimationTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
