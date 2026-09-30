using DesktopPet.Models;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _baseDirectory;
    private readonly AppLogger _logger;

    public SettingsService(string baseDirectory, AppLogger logger)
    {
        _baseDirectory = baseDirectory;
        _logger = logger;
        SettingsPath = Path.Combine(baseDirectory, "settings.json");
    }

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopPet");

    public string SettingsPath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = File.OpenRead(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions)
                ?? throw new JsonException("The settings document contained null.");
            return settings.Normalize();
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            _logger.Error("Settings are invalid; restoring defaults.", error);
            BackupCorruptSettings();
            return new AppSettings();
        }
        catch (Exception error) when (IsExpectedFileError(error))
        {
            _logger.Error("Settings could not be loaded; restoring defaults.", error);
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        string? temporaryPath = null;
        try
        {
            ArgumentNullException.ThrowIfNull(settings);
            Directory.CreateDirectory(_baseDirectory);
            temporaryPath = Path.Combine(_baseDirectory, $"settings.{Guid.NewGuid():N}.tmp");

            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings.Normalize(), JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
            temporaryPath = null;
            return true;
        }
        catch (Exception error) when (IsExpectedSaveError(error))
        {
            _logger.Error("Settings could not be saved.", error);
            return false;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    private void BackupCorruptSettings()
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfffffff");
            var backupPath = Path.Combine(_baseDirectory, $"settings.corrupt.{timestamp}.json");
            File.Move(SettingsPath, backupPath);
        }
        catch (Exception error) when (IsExpectedFileError(error))
        {
            _logger.Error("The invalid settings file could not be backed up.", error);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static bool IsExpectedSaveError(Exception error) =>
        error is ArgumentException or IOException or UnauthorizedAccessException or SecurityException
            or JsonException or NotSupportedException;

    private static bool IsExpectedFileError(Exception error) =>
        error is ArgumentException or IOException or UnauthorizedAccessException or SecurityException;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup after a failed atomic save.
        }
    }
}
