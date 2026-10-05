using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopPet.Models;

namespace DesktopPet.Services;

public sealed class PetProfileService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private readonly AppLogger _logger;
    public string ProfilePath { get; }
    public PetProfileService(string directory, AppLogger logger)
    {
        ProfilePath = Path.Combine(directory, "profile.json");
        _logger = logger;
    }
    public PetProfile Load()
    {
        if (!File.Exists(ProfilePath)) return new();
        try
        {
            using var stream = File.OpenRead(ProfilePath);
            return (JsonSerializer.Deserialize<PetProfile>(stream, Options) ?? throw new JsonException("Null profile")).Normalize();
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            _logger.Error("Invalid pet profile; restoring defaults", error);
            try { File.Move(ProfilePath, Path.Combine(Path.GetDirectoryName(ProfilePath)!, $"profile.corrupt.{DateTime.UtcNow:yyyyMMddHHmmssfffffff}.json")); }
            catch (Exception backup) when (FileError(backup)) { _logger.Error("Cannot back up invalid profile", backup); }
            return new();
        }
        catch (Exception error) when (FileError(error)) { _logger.Error("Cannot load pet profile", error); return new(); }
    }
    public bool Save(PetProfile profile)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(ProfilePath)!, $"profile.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, profile.Normalize(), Options);
                stream.Flush(true);
            }
            File.Move(temporary, ProfilePath, true);
            return true;
        }
        catch (Exception error) when (FileError(error) || error is JsonException or NotSupportedException)
        { _logger.Error("Cannot save pet profile", error); return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (FileError(error)) { _logger.Error("Cannot clean profile temporary file", error); }
        }
    }
    private static bool FileError(Exception error) => error is IOException or UnauthorizedAccessException or SecurityException or ArgumentException;
}
