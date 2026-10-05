using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopPet.Models;

namespace DesktopPet.Services;

/// <summary>Exports current settings and care/diary snapshots without modifying their live files.</summary>
public static class DataBackupService
{
    private static readonly JsonSerializerOptions ProfileOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions SettingsOptions = new(ProfileOptions)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Create(string destination, AppSettings settings, PetProfile profile, DateTimeOffset createdUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(profile);
        var target = Path.GetFullPath(destination);
        if (!Path.GetExtension(target).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("数据备份必须保存为 ZIP 文件。", nameof(destination));
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".desktop-pet-backup-" + Guid.NewGuid().ToString("N") + ".tmp");
        var ownsTemporary = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                ownsTemporary = true;
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    Write(archive, "settings.json", JsonSerializer.SerializeToUtf8Bytes(settings.Normalize(), SettingsOptions));
                    Write(archive, "profile.json", JsonSerializer.SerializeToUtf8Bytes(profile.Normalize(), ProfileOptions));
                    Write(archive, "backup.json", JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        Format = "DesktopPetBackup", FormatVersion = 1, CreatedUtc = createdUtc.ToUniversalTime(),
                        ApplicationVersion = typeof(DataBackupService).Assembly.GetName().Version?.ToString(3),
                        Includes = new[] { "settings", "care", "journal" },
                        Excludes = new[] { "skins", "photos", "logs", "current-focus-session" }
                    }, ProfileOptions));
                }
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (ownsTemporary && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void Write(ZipArchive archive, string name, byte[] bytes)
    {
        using var output = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        output.Write(bytes);
    }
}
