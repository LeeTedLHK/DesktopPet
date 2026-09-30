using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DesktopPet.Tests.{Guid.NewGuid():N}");

    [Fact]
    public void MissingFileLoadsDefaults()
    {
        var service = CreateService();

        Assert.Equal(new AppSettings(), service.Load());
    }

    [Fact]
    public void SaveAndLoadRoundTripUsesReadableEnumNamesAndNormalizedValues()
    {
        var service = CreateService();
        var input = new AppSettings
        {
            Topmost = false,
            AutoBehaviorEnabled = false,
            ShowPetOnStartup = false,
            PetScale = 9,
            MoveSpeed = MovementSpeed.Fast,
            BehaviorFrequency = BehaviorFrequency.High,
            EnableAnimations = false,
            AnimationSpeedMultiplier = 2.25,
            LastPetX = 120.5,
            LastPetY = -20.25,
        };

        Assert.True(service.Save(input));
        var loaded = service.Load();

        Assert.False(loaded.Topmost);
        Assert.False(loaded.AutoBehaviorEnabled);
        Assert.False(loaded.ShowPetOnStartup);
        Assert.Equal(2, loaded.PetScale);
        Assert.Equal(MovementSpeed.Fast, loaded.MoveSpeed);
        Assert.Equal(BehaviorFrequency.High, loaded.BehaviorFrequency);
        Assert.False(loaded.EnableAnimations);
        Assert.Equal(2.25, loaded.AnimationSpeedMultiplier);
        Assert.Equal(120.5, loaded.LastPetX);
        Assert.Equal(-20.25, loaded.LastPetY);
        var json = File.ReadAllText(service.SettingsPath);
        Assert.Contains("\"MoveSpeed\": \"Fast\"", json);
        Assert.Contains("\"BehaviorFrequency\": \"High\"", json);
        Assert.Contains(Environment.NewLine, json);
    }

    [Theory]
    [InlineData("{ this is broken")]
    [InlineData("null")]
    public void InvalidConfigIsBackedUpThenDefaultsAreReturned(string content)
    {
        var service = CreateService();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(service.SettingsPath, content);

        var loaded = service.Load();

        Assert.Equal(new AppSettings(), loaded);
        Assert.False(File.Exists(service.SettingsPath));
        var backup = Assert.Single(Directory.GetFiles(_directory, "settings.corrupt.*.json"));
        Assert.Equal(content, File.ReadAllText(backup));
    }

    [Fact]
    public void LoadSanitizesInvalidNumericFieldsAndEnumValues()
    {
        var service = CreateService();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(service.SettingsPath,
            """
            {
              "PetScale": "NaN",
              "MoveSpeed": 123,
              "BehaviorFrequency": -10,
              "LastPetX": "Infinity",
              "LastPetY": 42
            }
            """);

        var loaded = service.Load();

        Assert.Equal(1, loaded.PetScale);
        Assert.Equal(MovementSpeed.Normal, loaded.MoveSpeed);
        Assert.Equal(BehaviorFrequency.Normal, loaded.BehaviorFrequency);
        Assert.Null(loaded.LastPetX);
        Assert.Null(loaded.LastPetY);
    }

    [Fact]
    public void LoadLegacyConfigUsesDefaultsForAnimationFields()
    {
        var service = CreateService();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(service.SettingsPath,
            "{ \"Topmost\": false, \"PetScale\": 1.2, \"MoveSpeed\": \"Slow\" }");

        var loaded = service.Load();

        Assert.False(loaded.Topmost);
        Assert.Equal(1.2, loaded.PetScale);
        Assert.Equal(MovementSpeed.Slow, loaded.MoveSpeed);
        Assert.True(loaded.EnableAnimations);
        Assert.Equal(1d, loaded.AnimationSpeedMultiplier);
    }

    [Fact]
    public void FailedSaveLeavesExistingGoodConfigurationIntactAndCleansTempFile()
    {
        var service = CreateService();
        Assert.True(service.Save(new AppSettings { PetScale = 1.25 }));

        using (File.Open(service.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(service.Save(new AppSettings { PetScale = 1.75 }));
        }

        Assert.Equal(1.25, service.Load().PetScale);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void SaveReturnsFalseWhenBaseDirectoryCannotBeCreated()
    {
        Directory.CreateDirectory(_directory);
        var occupiedPath = Path.Combine(_directory, "occupied");
        File.WriteAllText(occupiedPath, "file");
        var service = new SettingsService(occupiedPath, new AppLogger(_directory));

        Assert.False(service.Save(new AppSettings()));
    }

    private SettingsService CreateService() => new(_directory, new AppLogger(_directory));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
