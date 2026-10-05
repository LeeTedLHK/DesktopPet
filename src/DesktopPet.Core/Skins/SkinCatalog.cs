using System.IO.Compression;
using System.Security;
using System.Text.Json;
using DesktopPet.Animations;

namespace DesktopPet.Skins;

/// <summary>Owns independent PNG skin copies. Invalid or missing IDs resolve to the built-in skin.</summary>
public sealed class SkinCatalog
{
    public const string DefaultId = "default";
    private const int MaxFrames = 5000;
    private const long MaxFrameBytes = 8 * 1024 * 1024;
    private const long MaxTotalBytes = 128 * 1024 * 1024;
    private const long MaxManifestBytes = 64 * 1024;
    private const long MaxAnchorsBytes = 128 * 1024;
    private const long MaxDecodedPixels = 64L * 1024 * 1024;
    private static readonly AnimationKind[] Kinds = Enum.GetValues<AnimationKind>();
    private readonly string _defaultRoot;
    private readonly string _userRoot;
    private readonly Action<string> _log;

    public SkinCatalog(string defaultRoot, string userRoot, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(userRoot);
        _defaultRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(defaultRoot));
        _userRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userRoot));
        _log = log ?? (_ => { });
    }

    public IReadOnlyList<SkinInfo> List()
    {
        var imported = new List<SkinInfo>();
        try
        {
            if (Directory.Exists(_userRoot))
            {
                RejectLinkedAncestors(_userRoot);
                foreach (var directory in Directory.EnumerateDirectories(_userRoot))
                {
                    var id = Path.GetFileName(directory);
                    if (!Guid.TryParseExact(id, "N", out _)) continue;
                    var info = TryReadInstalled(id, directory);
                    if (info is not null) imported.Add(info);
                }
            }
        }
        catch (Exception error) when (IsExpectedError(error))
        {
            _log($"Skin catalog could not be read: {error.Message}");
        }
        return new[] { ReadDefault() }.Concat(imported.OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(info => info.Id, StringComparer.Ordinal)).ToArray();
    }

    /// <summary>Returns the built-in skin for an unknown, invalid, removed, or corrupt imported ID.</summary>
    public SkinInfo Resolve(string id)
    {
        if (Guid.TryParseExact(id, "N", out var guid))
        {
            var normalized = guid.ToString("N");
            var info = TryReadInstalled(normalized, Path.Combine(_userRoot, normalized));
            if (info is not null) return info;
        }
        return ReadDefault();
    }

    public SkinInfo ImportFolder(string path, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var source = Path.GetFullPath(path);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException($"Skin folder does not exist: {source}");
        var files = InspectFolder(source);
        return Install(files, name, Path.GetFileName(Path.TrimEndingDirectorySeparator(source)));
    }

    public SkinInfo ImportZip(string path, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var source = Path.GetFullPath(path);
        RejectLinkedAncestors(source);
        using var stream = File.OpenRead(source);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        if (archive.Entries.Count > MaxFrames + Kinds.Length + 3)
            throw new InvalidDataException("Skin archive has too many frame or directory entries.");
        var entries = archive.Entries.Select(entry =>
        {
            var parts = ParseArchivePath(entry.FullName);
            var isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 ||
                unixType is not (0 or 0x4000 or 0x8000) || (unixType == 0x4000 && !isDirectory))
                throw new InvalidDataException("Skin ZIP contains a link or unsupported entry type.");
            if (isDirectory && entry.Length != 0) throw new InvalidDataException("Skin ZIP directory contains data.");
            return new ArchiveItem(entry, parts, isDirectory);
        }).ToArray();
        if (entries.Length == 0) throw new InvalidDataException("Skin ZIP is empty.");
        // A wrapper may itself be named Idle, skin.json, or mouth-anchors.json.
        // Classify by usable root file shapes, rather than the first path segment alone.
        var rootLayout = entries.Any(item => !item.IsDirectory &&
            ((item.Parts.Length == 1 && IsMetadata(item.Parts[0])) ||
             (item.Parts.Length == 2 && IsAction(item.Parts[0]) &&
              Path.GetExtension(item.Parts[1]).Equals(".png", StringComparison.OrdinalIgnoreCase))));
        var wrapper = rootLayout ? null : entries[0].Parts[0];
        if (wrapper is not null && entries.Any(item => !item.Parts[0].Equals(wrapper, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Skin ZIP must contain action folders at root or one enclosing folder.");
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<ImportFile>();
        foreach (var item in entries)
        {
            var parts = wrapper is null ? item.Parts : item.Parts[1..];
            if (parts.Length == 0 && item.IsDirectory) continue;
            if (item.IsDirectory)
            {
                if (parts.Length != 1 || !IsAction(parts[0])) throw new InvalidDataException("Unknown skin action directory.");
                continue;
            }
            var relative = CanonicalFile(parts);
            if (!destinations.Add(relative)) throw new InvalidDataException("Skin ZIP contains duplicate frame destinations.");
            files.Add(new ImportFile(relative, item.Entry.Length, item.Entry.Open));
        }
        CheckLimits(files);
        return Install(files, name, wrapper ?? Path.GetFileNameWithoutExtension(source));
    }

    /// <summary>Exports the resolved skin as a reimportable ZIP, preserving any existing destination on failure.</summary>
    public void ExportZip(string id, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var info = Resolve(id);
        var sourceRoot = Path.TrimEndingDirectorySeparator(NormalizeExportPath(info.RootPath));
        var target = NormalizeExportPath(destination);
        ValidateSegment(Path.GetFileName(target));
        var sourcePrefix = Path.EndsInDirectorySeparator(sourceRoot) ? sourceRoot : sourceRoot + Path.DirectorySeparatorChar;
        if (target.Equals(sourceRoot, StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Skin export destination cannot be inside its source skin folder.");
        if (!Path.GetExtension(target).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Skin export destination must have a .zip extension.");
        RejectLinkedAncestors(target);
        var files = InspectExportFolder(sourceRoot, info.Name);
        CheckLimits(files);
        ValidateFrames(sourceRoot, files);

        var parent = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        RejectLinkedAncestors(parent);
        var temporary = Path.Combine(parent, ".skin-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        var ownsTemporary = false;
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    long totalBytes = 0;
                    long pixels = 0;
                    var buffer = new byte[81920];
                    foreach (var file in files)
                    {
                        using var input = file.Open();
                        // File inputs disallow writers while these checks and the copy run.
                        // Repeat PNG validation under that lock to detect source changes after preflight.
                        if (!IsMetadata(file.RelativePath))
                        {
                            pixels += PngHeaderValidator.Validate(Path.Combine(sourceRoot, file.RelativePath));
                            if (pixels > MaxDecodedPixels)
                                throw new InvalidDataException("Skin aggregate decoded pixel count exceeds 64 Mi-pixels.");
                        }
                        using var entry = archive.CreateEntry(file.RelativePath.Replace('\\', '/'), CompressionLevel.Optimal).Open();
                        long copied = 0;
                        int count;
                        while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            copied += count;
                            totalBytes += count;
                            CheckFileLength(file.RelativePath, copied);
                            if (totalBytes > MaxTotalBytes)
                                throw new InvalidDataException("Skin total decompressed size is too large.");
                            entry.Write(buffer, 0, count);
                        }
                        if (copied != file.Length)
                            throw new InvalidDataException("Skin source changed size while being exported.");
                    }
                }
                output.Flush(flushToDisk: true);
            }
            RejectLinkedAncestors(target);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (ownsTemporary && File.Exists(temporary))
            {
                try
                {
                    RejectLinkedAncestors(temporary);
                    File.Delete(temporary);
                }
                catch (Exception error) when (IsExpectedError(error)) { _log($"Skin export cleanup failed: {error.Message}"); }
            }
        }
    }

    private SkinInfo Install(IReadOnlyList<ImportFile> files, string? requestedName, string fallbackName)
    {
        CheckLimits(files);
        RejectLinkedAncestors(_userRoot);
        Directory.CreateDirectory(_userRoot);
        RejectLinkedAncestors(_userRoot);
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(_userRoot, ".import-" + id);
        var destination = Path.Combine(_userRoot, id);
        Directory.CreateDirectory(staging);
        try
        {
            long totalBytes = 0;
            var buffer = new byte[81920];
            foreach (var file in files)
            {
                var target = Path.Combine(staging, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = file.Open();
                using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                long copied = 0;
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    copied += count; totalBytes += count;
                    CheckFileLength(file.RelativePath, copied);
                    if (totalBytes > MaxTotalBytes) throw new InvalidDataException("Skin total decompressed size is too large.");
                    output.Write(buffer, 0, count);
                }
                if (copied != file.Length) throw new InvalidDataException("Skin source changed size while being copied.");
            }
            ValidateFrames(staging, files);
            var metadataName = ReadManifest(staging);
            var displayName = NormalizeName(requestedName ?? metadataName ?? fallbackName);
            var metadata = JsonSerializer.SerializeToUtf8Bytes(new { name = displayName });
            var previousMetadataBytes = files.Where(file => IsManifest(file.RelativePath)).Sum(file => file.Length);
            if (totalBytes - previousMetadataBytes + metadata.Length > MaxTotalBytes)
                throw new InvalidDataException("Skin total decompressed size including installed metadata exceeds 128 MB.");
            File.WriteAllBytes(Path.Combine(staging, "skin.json"), metadata);
            Directory.Move(staging, destination);
            return Describe(id, displayName, destination);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                try
                {
                    // Only this call's generated, validated staging tree is eligible for removal.
                    if (Path.GetDirectoryName(staging) == _userRoot && Path.GetFileName(staging) == ".import-" + id)
                    {
                        RejectLinkedAncestors(staging);
                        Directory.Delete(staging, recursive: true);
                    }
                }
                catch (Exception error) when (IsExpectedError(error)) { _log($"Skin staging cleanup failed: {error.Message}"); }
            }
        }
    }

    private static IReadOnlyList<ImportFile> InspectFolder(string root)
    {
        RejectLinkedAncestors(root);
        var files = new List<ImportFile>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            RejectReparse(entry);
            var itemName = Path.GetFileName(entry);
            ValidateSegment(itemName);
            if (Directory.Exists(entry))
            {
                if (!IsAction(itemName)) throw new InvalidDataException($"Unknown skin action folder: {itemName}");
                foreach (var child in Directory.EnumerateFileSystemEntries(entry))
                {
                    RejectReparse(child);
                    if (Directory.Exists(child)) throw new InvalidDataException("Nested skin action folders are not supported.");
                    Add(child, [itemName, Path.GetFileName(child)]);
                }
            }
            else Add(entry, [itemName]);
        }
        CheckLimits(files);
        return files;

        void Add(string source, string[] parts)
        {
            var relative = CanonicalFile(parts);
            if (!destinations.Add(relative)) throw new InvalidDataException("Skin contains duplicate frame destinations.");
            files.Add(new ImportFile(relative, new FileInfo(source).Length, () => OpenSource(source)));
            if (files.Count > MaxFrames + 2) throw new InvalidDataException("Skin has too many frames.");
        }
    }

    private static IReadOnlyList<ImportFile> InspectExportFolder(string root, string name)
    {
        RejectLinkedAncestors(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Skin folder does not exist: {root}");
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new { name = NormalizeName(name) });
        var files = new List<ImportFile> { new("skin.json", metadata.Length, () => new MemoryStream(metadata, writable: false)) };
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "skin.json" };
        foreach (var kind in Kinds)
        {
            var directory = Path.Combine(root, kind.ToString());
            if (!Directory.Exists(directory)) continue;
            RejectReparse(directory);
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)) continue;
                Add(path, [kind.ToString(), Path.GetFileName(path)]);
            }
        }
        var anchors = Path.Combine(root, "mouth-anchors.json");
        if (File.Exists(anchors) || Directory.Exists(anchors))
        {
            RejectReparse(anchors);
            if (Directory.Exists(anchors)) throw new InvalidDataException("Skin mouth anchors must be a file.");
            Add(anchors, ["mouth-anchors.json"]);
        }
        return files;

        void Add(string source, string[] parts)
        {
            RejectReparse(source);
            var relative = CanonicalFile(parts);
            if (!destinations.Add(relative)) throw new InvalidDataException("Skin contains duplicate frame destinations.");
            files.Add(new ImportFile(relative, new FileInfo(source).Length, () => OpenSource(source)));
            if (files.Count > MaxFrames + 2) throw new InvalidDataException("Skin has too many frames.");
        }
    }

    private SkinInfo? TryReadInstalled(string id, string root)
    {
        if (!Directory.Exists(root)) return null;
        try
        {
            var files = InspectFolder(root);
            ValidateFrames(root, files);
            return Describe(id, ReadManifest(root) ?? id, root);
        }
        catch (Exception error) when (IsExpectedError(error))
        {
            _log($"Imported skin '{id}' was skipped: {error.Message}");
            return null;
        }
    }

    private SkinInfo ReadDefault()
    {
        var info = Describe(DefaultId, "默认比熊", _defaultRoot);
        if (info.PreviewPath is not null) return info;
        foreach (var name in new[] { "pet.png", "pet.jpg" })
        {
            var path = Path.Combine(_defaultRoot, name);
            if (File.Exists(path)) return info with { PreviewPath = path };
        }
        return info;
    }

    private static SkinInfo Describe(string id, string name, string root)
    {
        var frames = Kinds.ToDictionary(kind => kind, kind => ReadFramePaths(root, kind));
        return new SkinInfo(id, name, root, frames[AnimationKind.Idle].FirstOrDefault(),
            Array.AsReadOnly(Kinds.Where(kind => frames[kind].Count == 0).ToArray()));
    }

    private static IReadOnlyList<string> ReadFramePaths(string root, AnimationKind kind)
    {
        var directory = Path.Combine(root, kind.ToString());
        try
        {
            return Directory.Exists(directory) ? NaturalSort.Order(Directory.EnumerateFiles(directory)
                .Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))) : [];
        }
        catch (Exception error) when (IsExpectedError(error)) { return []; }
    }

    private static void ValidateFrames(string root, IReadOnlyList<ImportFile> files)
    {
        if (!files.Any(file => Path.GetDirectoryName(file.RelativePath) == AnimationKind.Idle.ToString()))
            throw new InvalidDataException("Skin requires at least one usable Idle PNG frame.");
        long pixels = 0;
        foreach (var file in files.Where(file => !IsMetadata(file.RelativePath)))
        {
            pixels += PngHeaderValidator.Validate(Path.Combine(root, file.RelativePath));
            if (pixels > MaxDecodedPixels) throw new InvalidDataException("Skin aggregate decoded pixel count exceeds 64 Mi-pixels.");
        }
    }

    private static void CheckLimits(IReadOnlyList<ImportFile> files)
    {
        if (files.Count(file => !IsMetadata(file.RelativePath)) > MaxFrames)
            throw new InvalidDataException("Skin has too many frames (maximum 5000).");
        long total = 0;
        foreach (var file in files)
        {
            CheckFileLength(file.RelativePath, file.Length);
            total += file.Length;
            if (total > MaxTotalBytes) throw new InvalidDataException("Skin total decompressed size exceeds 128 MB.");
        }
    }

    private static void CheckFileLength(string relative, long length)
    {
        var (maximum, description) = IsManifest(relative) ? (MaxManifestBytes, "manifest") :
            IsAnchors(relative) ? (MaxAnchorsBytes, "mouth anchors") : (MaxFrameBytes, "frame");
        if (length < 0 || length > maximum)
            throw new InvalidDataException($"Skin {description} is too large (maximum {maximum} bytes).");
    }

    private static string? ReadManifest(string root)
    {
        var path = Path.Combine(root, "skin.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("skin.json must contain a JSON object.");
            string? name = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                    !property.Name.Equals("displayName", StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("skin.json display name must be a string.");
                name = NormalizeName(property.Value.GetString()!);
            }
            return name;
        }
        catch (JsonException error) { throw new InvalidDataException("skin.json contains malformed JSON.", error); }
    }

    private static string NormalizeName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl))
            throw new InvalidDataException("Skin display name must contain 1–100 characters without control characters.");
        return name;
    }

    private static string[] ParseArchivePath(string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] is '/' or '\\') throw new InvalidDataException("Absolute or empty ZIP path.");
        var normalized = name.Replace('\\', '/');
        if (normalized.EndsWith('/')) normalized = normalized[..^1];
        var parts = normalized.Split('/');
        foreach (var part in parts) ValidateSegment(part);
        return parts;
    }

    private static string CanonicalFile(string[] parts)
    {
        foreach (var part in parts) ValidateSegment(part);
        if (parts.Length == 1 && IsManifest(parts[0])) return "skin.json";
        if (parts.Length == 1 && IsAnchors(parts[0])) return "mouth-anchors.json";
        if (parts.Length != 2 || !Enum.TryParse<AnimationKind>(parts[0], true, out var kind) ||
            !IsAction(parts[0]) || !Path.GetExtension(parts[1]).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only skin.json, mouth-anchors.json, and PNG frames in known action folders can be imported.");
        return Path.Combine(kind.ToString(), parts[1]);
    }

    private static void ValidateSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') ||
            segment.Any(character => character < 32 || "<>:\"/\\|?*".Contains(character)))
            throw new InvalidDataException("Unsafe skin path component.");
        var stem = segment.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9'))
            throw new InvalidDataException("Windows device names are not valid skin paths.");
    }

    private static bool IsManifest(string path) => path.Equals("skin.json", StringComparison.OrdinalIgnoreCase);
    private static bool IsAnchors(string path) => path.Equals("mouth-anchors.json", StringComparison.OrdinalIgnoreCase);
    private static bool IsMetadata(string path) => IsManifest(path) || IsAnchors(path);
    private static bool IsAction(string name) => Kinds.Any(kind => name.Equals(kind.ToString(), StringComparison.OrdinalIgnoreCase));
    private static string NormalizeExportPath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path[8..];
        else if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            path = path[4..];
            if (!Path.IsPathFullyQualified(path))
                throw new InvalidDataException("Unsupported skin export device path.");
        }
        if (path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported skin export device path.");
        return Path.GetFullPath(path);
    }
    private static Stream OpenSource(string path)
    {
        RejectLinkedAncestors(path);
        return File.OpenRead(path);
    }
    private static void RejectLinkedAncestors(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current)) RejectReparse(current);
    }
    private static void RejectReparse(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Skin paths cannot contain symbolic links, junctions, or reparse points.");
    }
    private static bool IsExpectedError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException or ArgumentException;
    private sealed record ImportFile(string RelativePath, long Length, Func<Stream> Open);
    private sealed record ArchiveItem(ZipArchiveEntry Entry, string[] Parts, bool IsDirectory);
}
