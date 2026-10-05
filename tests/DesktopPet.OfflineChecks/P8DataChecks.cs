using System.IO.Compression;
using System.Text.Json;
using DesktopPet.Journal;
using DesktopPet.Models;
using DesktopPet.Services;

internal static class P8DataChecks
{
    public static void Run(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "DesktopPet.P8Data." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var now = DateTimeOffset.Parse("2026-10-04T04:00:00Z");
            var settings = new AppSettings { PetName = "团团", AnimationSpeedMultiplier = .63,
                PetPersonality = PetPersonality.Clingy, SelectedSkinId = Guid.NewGuid().ToString("N") };
            var profile = new PetProfile { Affection = 42, Fullness = 88, Energy = 67, UpdatedUtc = now,
                LastBondUtc = now.AddMinutes(-3), Journal = new JournalState { PlayCount = 7, FeedingCount = 3,
                    VisitedDays = [new(2026, 10, 3), new(2026, 10, 4)],
                    Entries = [new(now, new(2026, 10, 4), JournalEntryKind.Play, "小球叼回来了")],
                    LastActions = [new(JournalAction.Play, now)] } };
            var settingsBefore = JsonSerializer.Serialize(settings);
            var profileBefore = JsonSerializer.Serialize(profile);
            var destination = Path.Combine(root, "backup.zip");
            DataBackupService.Create(destination, settings, profile, now);
            using (var zip = ZipFile.OpenRead(destination))
            {
                check(zip.Entries.Select(entry => entry.FullName).Order().SequenceEqual(
                    new[] { "backup.json", "profile.json", "settings.json" }), "P8 backup contains only the promised three entries");
                var restored = Path.Combine(root, "restored"); Directory.CreateDirectory(restored);
                zip.ExtractToDirectory(restored);
                var logger = new AppLogger(restored);
                var restoredSettings = new SettingsService(restored, logger).Load();
                var restoredProfile = new PetProfileService(restored, logger).Load();
                check(restoredSettings.PetName == settings.PetName && restoredSettings.AnimationSpeedMultiplier == .63 &&
                    restoredSettings.PetPersonality == settings.PetPersonality && restoredSettings.SelectedSkinId == settings.SelectedSkinId,
                    "P8 backup settings remain readable by the application with custom values intact");
                check(restoredProfile.Affection == 42 && restoredProfile.Fullness == 88 && restoredProfile.Energy == 67 &&
                    restoredProfile.UpdatedUtc == now && restoredProfile.LastBondUtc == profile.LastBondUtc,
                    "P8 backup preserves care values and timestamps");
                check(JsonSerializer.Serialize(restoredProfile.Journal) == JsonSerializer.Serialize(profile.Journal),
                    "P8 backup preserves full diary, counts, visited days and action cooldowns");
                using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(restored, "backup.json")));
                check(manifest.RootElement.GetProperty("Format").GetString() == "DesktopPetBackup" &&
                    manifest.RootElement.GetProperty("FormatVersion").GetInt32() == 1 &&
                    manifest.RootElement.GetProperty("CreatedUtc").GetDateTimeOffset() == now,
                    "P8 backup identifies its format and creation time");
            }
            check(JsonSerializer.Serialize(settings) == settingsBefore && JsonSerializer.Serialize(profile) == profileBefore,
                "P8 backup does not mutate live settings or profile");
            DataBackupService.Create(destination, settings with { AnimationSpeedMultiplier = double.NaN },
                profile with { Affection = double.NaN, Energy = 900 }, now);
            using (var zip = ZipFile.OpenRead(destination))
            {
                using var settingsStream = zip.GetEntry("settings.json")!.Open();
                using var settingsJson = JsonDocument.Parse(settingsStream);
                using var profileStream = zip.GetEntry("profile.json")!.Open();
                using var profileJson = JsonDocument.Parse(profileStream);
                check(settingsJson.RootElement.GetProperty("AnimationSpeedMultiplier").GetDouble() == 1 &&
                    profileJson.RootElement.GetProperty("Affection").GetDouble() == 20 &&
                    profileJson.RootElement.GetProperty("Energy").GetDouble() == 100,
                    "P8 backup normalizes invalid numeric values into readable JSON");
            }
            var protectedPath = Path.Combine(root, "settings.json"); File.WriteAllText(protectedPath, "keep me");
            var wrongExtensionRejected = false;
            try { DataBackupService.Create(protectedPath, settings, profile, now); }
            catch (ArgumentException) { wrongExtensionRejected = true; }
            check(wrongExtensionRejected && File.ReadAllText(protectedPath) == "keep me",
                "P8 backup cannot overwrite a configuration file instead of a ZIP");
            var originalZip = File.ReadAllBytes(destination);
            using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var failedSafely = false;
                try { DataBackupService.Create(destination, settings, profile, now); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failedSafely = true; }
                check(failedSafely && File.ReadAllBytes(destination).SequenceEqual(originalZip),
                    "P8 failed backup replacement preserves the original archive");
            }
            check(!Directory.EnumerateFiles(root, ".desktop-pet-backup-*").Any(),
                "P8 failed and successful backups remove their own temporary files");
        }
        finally { Directory.Delete(root, true); }
    }
}
